using System;
using System.Collections.Generic;
using System.Numerics;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone.Topics;

namespace ModuleHost.Network.Cyclone.Translators
{
    /// <summary>
    /// Translates EntityStateTopic (DDS) to/from Core's Position, Velocity, and other state components.
    /// Performs critical mapping:
    /// - NetworkAppId (DDS) -> int OwnerNodeId (Core) via NodeIdMapper (for ghost entities)
    /// </summary>
    public class EntityStateTranslator : IDescriptorTranslator
    {
        public string TopicName => "SST_EntityState";
        
        private readonly NodeIdMapper _nodeMapper;
        private readonly int _localInternalId;
        private readonly Dictionary<long, Entity> _networkIdToEntity;
        
        public EntityStateTranslator(
            NodeIdMapper nodeMapper,
            int localInternalId,
            Dictionary<long, Entity> networkIdToEntity)
        {
            _nodeMapper = nodeMapper ?? throw new ArgumentNullException(nameof(nodeMapper));
            _localInternalId = localInternalId;
            _networkIdToEntity = networkIdToEntity ?? throw new ArgumentNullException(nameof(networkIdToEntity));
        }
        
        public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, ISimulationView view)
        {
            // Cast for direct repository access
            var repo = view as EntityRepository;
            if (repo == null)
            {
                throw new InvalidOperationException(
                    "EntityStateTranslator requires direct EntityRepository access. " +
                    "NetworkGateway must run with ExecutionPolicy.Synchronous().");
            }
            
            foreach (var sample in reader.TakeSamples())
            {
                if (sample.InstanceState == DdsInstanceState.NotAliveDisposed)
                {
                    // State disposal doesn't destroy entity (Master does that)
                    continue;
                }
                
                if (sample.Data is not EntityStateTopic topic)
                {
                    if (sample.InstanceState == DdsInstanceState.Alive)
                        Console.Error.WriteLine($"[EntityStateTranslator] Unexpected sample type: {sample.Data?.GetType().Name}");
                    continue;
                }
                
                // Find or create entity
                Entity entity;
                
                if (!_networkIdToEntity.TryGetValue(topic.EntityId, out entity) || !view.IsAlive(entity))
                {
                    // Entity doesn't exist - create as Ghost (State-first scenario)
                    entity = repo.CreateEntity();
                    
                    // Set to Ghost immediately
                    repo.SetLifecycleState(entity, EntityLifecycle.Ghost);
                    
                    // Set NetworkIdentity
                    repo.AddComponent(entity, new NetworkIdentity { Value = topic.EntityId });
                    
                    // Add to mapping
                    _networkIdToEntity[topic.EntityId] = entity;
                    
                    // Note: We don't know the owner from EntityState alone
                    // EntityMaster will set it when it arrives
                }
                
                // Update position
                var position = new Position
                {
                    Value = new Vector3(
                        (float)topic.PositionX,
                        (float)topic.PositionY,
                        (float)topic.PositionZ)
                };
                
                if (repo.HasComponent<Position>(entity))
                {
                    repo.SetComponent(entity, position);
                }
                else
                {
                    repo.AddComponent(entity, position);
                }
                
                // Update velocity
                var velocity = new Velocity
                {
                    Value = new Vector3(
                        topic.VelocityX,
                        topic.VelocityY,
                        topic.VelocityZ)
                };
                
                if (repo.HasComponent<Velocity>(entity))
                {
                    repo.SetComponent(entity, velocity);
                }
                else
                {
                    repo.AddComponent(entity, velocity);
                }
                
                // Update orientation (if you have an Orientation component)
                // For now, we'll skip it since it's not in the NetworkComponents we saw
                
                // Update network target for smoothing
                if (!repo.HasComponent<NetworkTarget>(entity))
                {
                    repo.AddComponent(entity, new NetworkTarget
                    {
                        Value = position.Value,
                        Timestamp = topic.Timestamp
                    });
                }
                else
                {
                    var target = repo.GetComponent<NetworkTarget>(entity);
                    target.Value = position.Value;
                    target.Timestamp = topic.Timestamp;
                    repo.SetComponent(entity, target);
                }
            }
        }

        public void ScanAndPublish(ISimulationView view, IDataWriter writer)
        {
            // Egress: Scan Core entities and publish to DDS
            // Only publish entities we own
            
            var query = view.Query()
                .With<NetworkIdentity>()
                .With<Position>()
                .With<Velocity>()
                .With<NetworkOwnership>()
                .Build();
                
            foreach (var entity in query)
            {
                var ownership = view.GetComponentRO<NetworkOwnership>(entity);
                
                // Only publish if we own it
                if (ownership.PrimaryOwnerId != _localInternalId)
                    continue;
                
                var netId = view.GetComponentRO<NetworkIdentity>(entity);
                var position = view.GetComponentRO<Position>(entity);
                var velocity = view.GetComponentRO<Velocity>(entity);
                
                // Write to DDS
                writer.Write(new EntityStateTopic
                {
                    EntityId = netId.Value,
                    PositionX = position.Value.X,
                    PositionY = position.Value.Y,
                    PositionZ = position.Value.Z,
                    VelocityX = velocity.Value.X,
                    VelocityY = velocity.Value.Y,
                    VelocityZ = velocity.Value.Z,
                    OrientationX = 0, // TODO: Get from Orientation component if available
                    OrientationY = 0,
                    OrientationZ = 0,
                    OrientationW = 1, // Identity quaternion
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }
        }
    }
}
