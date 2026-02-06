using System;
using System.Collections.Generic;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Network.Cyclone.Components;
using FDP.Toolkit.Replication.Components;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone.Topics;

namespace ModuleHost.Network.Cyclone.Translators
{
    public class EntityMasterTranslator : IDescriptorTranslator
    {
        private readonly NetworkEntityMap _entityMap;
        private readonly NodeIdMapper _nodeMapper;
        private readonly TypeIdMapper _typeMapper;
        
        public string TopicName => "SST_EntityMaster";

        public EntityMasterTranslator(NetworkEntityMap entityMap, NodeIdMapper nodeMapper, TypeIdMapper typeMapper)
        {
            _entityMap = entityMap;
            _nodeMapper = nodeMapper;
            _typeMapper = typeMapper;
        }

        public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, ISimulationView view)
        {
            foreach (var sample in reader.TakeSamples())
            {
                if (sample.InstanceState != DdsInstanceState.Alive) continue;
                if (sample.Data is not EntityMasterTopic topic) continue;

                // Map Owner
                int ownerNodeId = _nodeMapper.GetOrRegisterInternalId(topic.OwnerId);

                // Ignore loopback
                if (ownerNodeId == _nodeMapper.LocalNodeId) continue;
                
                // Map Type (Reuse TypeIdMapper just to cache the mapping, though we use ulong component)
                // This ensures the generic TypeIdMapper is aware of this type if other systems need it.
                // _typeMapper.GetInternalId(topic.DisTypeValue); 

                // Check if we already have this entity
                if (_entityMap.TryGet(topic.EntityId, out var existingEntity))
                {
                    // Update existing
                    if (view.HasComponent<NetworkOwnership>(existingEntity))
                    {
                        var ownership = view.GetComponentRO<NetworkOwnership>(existingEntity);
                        if (ownership.PrimaryOwnerId != ownerNodeId)
                        {
                            // Create copy with new owner
                            var newOwnership = new NetworkOwnership
                            {
                                PrimaryOwnerId = ownerNodeId,
                                LocalNodeId = ownership.LocalNodeId
                            };
                            cmd.SetComponent(existingEntity, newOwnership);
                        }
                    }
                }
                else
                {
                    // Create new PROXY entity
                    var newEntity = cmd.CreateEntity();
                    
                    cmd.AddComponent(newEntity, new NetworkIdentity { Value = topic.EntityId });
                    
                    cmd.AddComponent(newEntity, new NetworkSpawnRequest 
                    { 
                        DisType = topic.DisTypeValue, 
                        OwnerId = (ulong)ownerNodeId 
                    });

                    cmd.AddComponent(newEntity, new NetworkOwnership 
                    { 
                        PrimaryOwnerId = ownerNodeId,
                        LocalNodeId = _nodeMapper.LocalNodeId 
                    });

                    _entityMap.Register(topic.EntityId, newEntity);
                }
            }
        }

        public void ScanAndPublish(ISimulationView view, IDataWriter writer)
        {
            // Iterate all entities that have Identity, SpawnRequest and Ownership
            // We only publish if we are the owner
            
            var query = view.Query()
                .With<NetworkIdentity>()
                .With<NetworkSpawnRequest>()
                .With<NetworkOwnership>()
                .Build();

            foreach(var entity in query)
            {
                    // Check ownership
                    ref readonly var ownership = ref view.GetComponentRO<NetworkOwnership>(entity);
                    if (ownership.PrimaryOwnerId != ownership.LocalNodeId)
                        continue;

                    ref readonly var identity = ref view.GetComponentRO<NetworkIdentity>(entity);
                    ref readonly var spawn = ref view.GetComponentRO<NetworkSpawnRequest>(entity);
                    
                    var topic = new EntityMasterTopic
                    {
                        EntityId = identity.Value,
                        OwnerId = _nodeMapper.GetExternalId(ownership.LocalNodeId),
                        DisTypeValue = spawn.DisType,
                        Flags = 0 
                    };
                    
                    writer.Write(topic);
            }
        }
    }
}
