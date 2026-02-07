using System;
using System.Collections.Generic;
using Fdp.Kernel;
using Fdp.Interfaces;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Network.Cyclone.Components;
using FDP.Toolkit.Replication.Components;
using ModuleHost.Network.Cyclone.Services;
using FDP.Toolkit.Replication.Services;
using ModuleHost.Network.Cyclone.Topics;
using FDP.Kernel.Logging;

using NetworkEntityMap = FDP.Toolkit.Replication.Services.NetworkEntityMap;
using IDescriptorTranslator = Fdp.Interfaces.IDescriptorTranslator;
using IDataReader = Fdp.Interfaces.IDataReader;
using IDataWriter = Fdp.Interfaces.IDataWriter;

namespace ModuleHost.Network.Cyclone.Translators
{
    public class EntityMasterTranslator : IDescriptorTranslator
    {
        private readonly NetworkEntityMap _entityMap;
        private readonly NodeIdMapper _nodeMapper;
        private readonly TypeIdMapper _typeMapper;
        
        public string TopicName => "SST_EntityMaster";
        public long DescriptorOrdinal => -1;

        public EntityMasterTranslator(NetworkEntityMap entityMap, NodeIdMapper nodeMapper, TypeIdMapper typeMapper)
        {
            _entityMap = entityMap;
            _nodeMapper = nodeMapper;
            _typeMapper = typeMapper;
        }

        public void ApplyToEntity(Entity entity, object data, EntityRepository repo) { }


        public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, ISimulationView view)
        {
            foreach (var sample in reader.TakeSamples())
            {
                if (sample.InstanceState != Fdp.Interfaces.NetworkInstanceState.Alive)
                {
                    if (sample.Data is EntityMasterTopic disposalTopic)
                    {
                         FdpLog<EntityMasterTranslator>.Info($"Processing NotAlive for EntityId {disposalTopic.EntityId} (Mapped: {_entityMap.TryGetEntity(disposalTopic.EntityId, out _)})");
                        if (_entityMap.TryGetEntity(disposalTopic.EntityId, out var entityToDestroy))
                        {
                            cmd.DestroyEntity(entityToDestroy);
                            _entityMap.Unregister(disposalTopic.EntityId, 0);
                        }
                    }
                    else 
                    {
                         FdpLog<EntityMasterTranslator>.Warn($"Received NotAlive but Data is {sample.Data?.GetType().Name ?? "null"}");
                    }
                    continue;
                }

                if (sample.Data is not EntityMasterTopic topic) continue;

                if (topic.Flags == 0xDEAD)
                {
                    if (_entityMap.TryGetEntity(topic.EntityId, out var entityToDestroy))
                    {
                        FdpLog<EntityMasterTranslator>.Info($"Received Death Note for {topic.EntityId}. Mapped to {entityToDestroy}. Destroying...");
                        cmd.DestroyEntity(entityToDestroy);
                        _entityMap.Unregister(topic.EntityId, 0);
                    }
                    else
                    {
                         FdpLog<EntityMasterTranslator>.Warn($"Received Death Note for {topic.EntityId} but it was not found in EntityMap.");
                    }
                    continue;
                }

                // Map Owner
                int ownerNodeId = _nodeMapper.GetOrRegisterInternalId(topic.OwnerId);

                // Ignore loopback
                if (ownerNodeId == _nodeMapper.LocalNodeId) continue;
                
                // Map Type (Reuse TypeIdMapper just to cache the mapping, though we use ulong component)
                // This ensures the generic TypeIdMapper is aware of this type if other systems need it.
                // _typeMapper.GetInternalId(topic.DisTypeValue); 

                // Check if we already have this entity
                if (_entityMap.TryGetEntity(topic.EntityId, out var existingEntity))
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

                    if (view.HasComponent<NetworkAuthority>(existingEntity))
                    {
                        var auth = view.GetComponentRO<NetworkAuthority>(existingEntity);
                        if (auth.PrimaryOwnerId != ownerNodeId)
                        {
                             cmd.SetComponent(existingEntity, new NetworkAuthority(ownerNodeId, auth.LocalNodeId));
                        }
                    }
                    else
                    {
                         cmd.AddComponent(existingEntity, new NetworkAuthority(ownerNodeId, _nodeMapper.LocalNodeId));
                    }
                }
                else
                {
                    // Create new PROXY entity
                    var repo = view as EntityRepository;
                    if (repo == null) 
                    {
                         FdpLog<EntityMasterTranslator>.Error("Cannot create proxy: View is not EntityRepository");
                         continue;
                    }

                    var newEntity = repo.CreateEntity();
                    
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

                    cmd.AddComponent(newEntity, new NetworkAuthority(ownerNodeId, _nodeMapper.LocalNodeId));

                    _entityMap.Register(topic.EntityId, newEntity);
                    FdpLog<EntityMasterTranslator>.Info($"Created Proxy Entity {newEntity} for NetID {topic.EntityId}");
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
