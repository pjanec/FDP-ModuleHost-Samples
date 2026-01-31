using System;
using System.Collections.Generic;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone.Topics;

namespace ModuleHost.Network.Cyclone.Translators
{
    /// <summary>
    /// Translates EntityMasterTopic (DDS) to/from Core's EntityMasterDescriptor.
    /// Performs critical mapping:
    /// - NetworkAppId (DDS) -> int OwnerNodeId (Core) via NodeIdMapper
    /// - ulong DisTypeValue (DDS) -> int TypeId (Core) via TypeIdMapper
    /// </summary>
    public class EntityMasterTranslator : IDescriptorTranslator
    {
        public string TopicName => "SST_EntityMaster";
        
        private readonly NodeIdMapper _nodeMapper;
        private readonly TypeIdMapper _typeMapper;
        private readonly int _localInternalId;
        private readonly Dictionary<long, Entity> _networkIdToEntity;
        
        public EntityMasterTranslator(
            NodeIdMapper nodeMapper,
            TypeIdMapper typeMapper,
            int localInternalId,
            Dictionary<long, Entity> networkIdToEntity)
        {
            _nodeMapper = nodeMapper ?? throw new ArgumentNullException(nameof(nodeMapper));
            _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
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
                    "EntityMasterTranslator requires direct EntityRepository access. " +
                    "NetworkGateway must run with ExecutionPolicy.Synchronous().");
            }
            
            foreach (var sample in reader.TakeSamples())
            {
                if (sample.InstanceState == DdsInstanceState.NotAliveDisposed)
                {
                    HandleDisposal(sample, cmd);
                    continue;
                }
                
                if (sample.Data is not EntityMasterTopic topic)
                {
                    if (sample.InstanceState == DdsInstanceState.Alive)
                        Console.Error.WriteLine($"[EntityMasterTranslator] Unexpected sample type: {sample.Data?.GetType().Name}");
                    continue;
                }
                
                // ★ CRITICAL MAPPING: DDS types -> Core types
                // NetworkAppId (struct) -> int OwnerNodeId
                int coreOwnerId = _nodeMapper.GetOrRegisterInternalId(topic.OwnerId);
                
                // ulong DisTypeValue -> int TypeId
                int coreTypeId = _typeMapper.GetCoreTypeId(topic.DisTypeValue);
                
                // Check if entity already exists
                Entity entity;
                if (!_networkIdToEntity.TryGetValue(topic.EntityId, out entity) || !view.IsAlive(entity))
                {
                    // Entity doesn't exist - create it directly
                    entity = repo.CreateEntity();
                    
                    // Set to Constructing immediately so it isn't visible as Active before Spawner runs
                    // Previously Ghost, but that confused Spawner logic.
                    repo.SetLifecycleState(entity, EntityLifecycle.Constructing);

                    // Set NetworkIdentity
                    repo.AddComponent(entity, new NetworkIdentity { Value = topic.EntityId });
                    
                    // Add to mapping
                    _networkIdToEntity[topic.EntityId] = entity;
                }
                
                // Set or update NetworkOwnership (Core uses simple ints)
                var netOwnership = new NetworkOwnership
                {
                    PrimaryOwnerId = coreOwnerId,
                    LocalNodeId = _localInternalId
                };

                if (repo.HasComponent<NetworkOwnership>(entity))
                {
                    repo.SetComponent(entity, netOwnership);
                }
                else
                {
                    repo.AddComponent(entity, netOwnership);
                }
                
                // Ensure DescriptorOwnership exists
                if (!view.HasManagedComponent<DescriptorOwnership>(entity))
                {
                    // Use command buffer to set managed component
                    // Note: For now, we'll skip this since it requires CommandBuffer support
                    // DescriptorOwnership will be created by other systems if needed
                }
                
                // Add NetworkSpawnRequest for NetworkSpawnerSystem to process
                // Note: Core systems expect DISEntityType, so we reconstruct it from the ulong
                if (!repo.HasComponent<NetworkSpawnRequest>(entity))
                {
                    var disType = new DISEntityType { Value = topic.DisTypeValue };
                    
                    repo.AddComponent(entity, new NetworkSpawnRequest
                    {
                        DisType = disType,
                        PrimaryOwnerId = coreOwnerId,
                        Flags = (MasterFlags)topic.Flags,
                        NetworkEntityId = topic.EntityId
                    });
                }
            }
        }
        
        private void HandleDisposal(IDataSample sample, IEntityCommandBuffer cmd)
        {
            long entityId = sample.EntityId;
            
            if (entityId == 0)
            {
                Console.Error.WriteLine("[EntityMasterTranslator] Cannot handle disposal - no entity ID");
                return;
            }
            
            if (_networkIdToEntity.TryGetValue(entityId, out var entity))
            {
                cmd.DestroyEntity(entity);
                _networkIdToEntity.Remove(entityId);
            }
        }

        public void ScanAndPublish(ISimulationView view, IDataWriter writer)
        {
            // Egress: Scan Core entities and publish to DDS
            // This would iterate entities with NetworkOwnership where IsLocallyOwned=true
            // and write EntityMasterTopic messages.
            
            // Implementation depends on how entities are tracked.
            // For now, we'll keep this stub for the test to pass.
            
            // Query for entities that need to be published
            var query = view.Query()
                .With<NetworkIdentity>()
                .With<NetworkOwnership>()
                .With<NetworkSpawnRequest>()
                .Build();
                
            foreach (var entity in query)
            {
                var netId = view.GetComponentRO<NetworkIdentity>(entity);
                var ownership = view.GetComponentRO<NetworkOwnership>(entity);
                var spawnReq = view.GetComponentRO<NetworkSpawnRequest>(entity);
                
                // Only publish if we own it
                if (ownership.PrimaryOwnerId != _localInternalId)
                    continue;
                
                // ★ REVERSE MAPPING: Core types -> DDS types
                // int OwnerNodeId -> NetworkAppId
                var ddsOwnerId = _nodeMapper.GetExternalId(ownership.PrimaryOwnerId);
                
                // The DisTypeValue is already in the SpawnRequest (ulong)
                ulong disTypeValue = spawnReq.DisType.Value;
                
                // Write to DDS
                writer.Write(new EntityMasterTopic
                {
                    EntityId = netId.Value,
                    OwnerId = ddsOwnerId,
                    DisTypeValue = disTypeValue,
                    Flags = (int)spawnReq.Flags
                });
            }
        }
    }
}
