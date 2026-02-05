using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Network.Cyclone.Components;
using FDP.Toolkit.Replication.Components;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone.Topics;
using ModuleHost.Network.Cyclone.Translators;
using ModuleHost.Network.Cyclone.Tests.Mocks;

namespace ModuleHost.Network.Cyclone.Tests.Integration
{
    public class MockDataWriter : IDataWriter
    {
        public List<object> WrittenSamples = new List<object>();
        public string TopicName => "MockTopic";

        public void Write(object sample)
        {
            WrittenSamples.Add(sample);
        }

        public void Dispose(long networkEntityId) { }
        public void Dispose() { }
    }

    public class ReliableInitializationScenarios
    {
        [Fact]
        public void Translator_Restoration_SmokeTest()
        {
            // 1. Setup Environment
            var repo = new EntityRepository();
            var view = (ISimulationView)repo;
            var cmd = view.GetCommandBuffer();
            
            // Register Components
            repo.RegisterComponent<NetworkIdentity>();
            repo.RegisterComponent<NetworkSpawnRequest>();
            repo.RegisterComponent<NetworkOwnership>();
            repo.RegisterComponent<NetworkPosition>();
            repo.RegisterComponent<NetworkVelocity>();
            repo.RegisterComponent<NetworkOrientation>();

            var entityMap = new NetworkEntityMap();
            var nodeMapper = new NodeIdMapper(0, 1); // Domain 0, Instance 1
            var typeMapper = new TypeIdMapper();

            var masterTranslator = new EntityMasterTranslator(entityMap, nodeMapper, typeMapper);
            var stateTranslator = new EntityStateTranslator(entityMap);

            // 2. Simulate Ingress: Entity Master (Spawn)
            long netEntityId = 999;
            ulong disType = 55;
            int ownerNodeId = 1; // Local
            
            var masterCdr = new MockDataReader(new MockDataSample 
            { 
                Data = new EntityMasterTopic 
                { 
                    EntityId = netEntityId,
                    DisTypeValue = disType,
                    OwnerId = nodeMapper.GetExternalId(ownerNodeId)
                }
            });

            masterTranslator.PollIngress(masterCdr, cmd, repo);
            
            // Execution Phase
            ((EntityCommandBuffer)cmd).Playback(repo); // Execute creation

            // FIX: Map now contains a placeholder entity (because CommandBuffer deferred creation).
            // We need to resolve it to the real entity for subsequent tests.
            // In a real loop, a system would maintain this map or the translator would handle remapping.
            // For this smoke test, we verify the placeholder is there, then swap it for the real entity found in repo.
            Assert.True(entityMap.TryGet(netEntityId, out var placeholderEntity));
            Assert.True(placeholderEntity.Index < 0); // Is placeholder

            // Find the real entity
            var query = ((ISimulationView)repo).Query().With<NetworkIdentity>().Build();
            Entity realEntity = default;
            foreach(var e in query) {
                if (repo.GetComponentRO<NetworkIdentity>(e).Value == netEntityId) {
                    realEntity = e;
                    break;
                }
            }
            Assert.True(realEntity.Index >= 0);

            // Update Map for next steps
            entityMap.Register(netEntityId, realEntity); // Overwrites placeholder (ConcurrentDictionary)

            // 3. Verify Spawn
            Assert.True(entityMap.TryGet(netEntityId, out var entity));
            Assert.Equal(realEntity, entity);
            Assert.True(repo.HasComponent<NetworkIdentity>(entity));
            Assert.Equal(netEntityId, repo.GetComponentRO<NetworkIdentity>(entity).Value);
            
            Assert.True(repo.HasComponent<NetworkSpawnRequest>(entity));
            Assert.Equal(disType, repo.GetComponentRO<NetworkSpawnRequest>(entity).DisType);

            Assert.True(repo.HasComponent<NetworkOwnership>(entity));
            Assert.Equal(ownerNodeId, repo.GetComponentRO<NetworkOwnership>(entity).PrimaryOwnerId);

            // 4. Simulate Ingress: Entity State (Position/Vel)
            var stateCdr = new MockDataReader(new MockDataSample
            {
                Data = new EntityStateTopic
                {
                    EntityId = netEntityId,
                    PositionX = 100, PositionY = 200, PositionZ = 300,
                    VelocityX = 1, VelocityY = 0, VelocityZ = 0,
                    OrientationX = 0, OrientationY = 0, OrientationZ = 0, OrientationW = 1
                }
            });

            stateTranslator.PollIngress(stateCdr, cmd, repo);
            ((EntityCommandBuffer)cmd).Playback(repo);

            // 5. Verify State
            Assert.True(repo.HasComponent<NetworkPosition>(entity));
            var pos = repo.GetComponentRO<NetworkPosition>(entity);
            Assert.Equal(100f, pos.Value.X);
            Assert.Equal(200f, pos.Value.Y);
            Assert.Equal(300f, pos.Value.Z);

            Assert.True(repo.HasComponent<NetworkVelocity>(entity));
            var vel = repo.GetComponentRO<NetworkVelocity>(entity).Value;
            Assert.Equal(1f, vel.X);
        }

        [Fact]
        public void Egress_ScanAndPublish_SmokeTest()
        {
             // 1. Setup Environment
            var repo = new EntityRepository();
            var view = (ISimulationView)repo;
            var cmd = view.GetCommandBuffer();
            
            repo.RegisterComponent<NetworkIdentity>();
            repo.RegisterComponent<NetworkSpawnRequest>();
            repo.RegisterComponent<NetworkOwnership>();
            repo.RegisterComponent<NetworkPosition>();
            repo.RegisterComponent<NetworkVelocity>();
            repo.RegisterComponent<NetworkOrientation>();

            var entityMap = new NetworkEntityMap();
            var nodeMapper = new NodeIdMapper(0, 1); // Domain 0, Instance 1
            var typeMapper = new TypeIdMapper();

            var masterTranslator = new EntityMasterTranslator(entityMap, nodeMapper, typeMapper);
            var stateTranslator = new EntityStateTranslator(entityMap);

            // 2. Create Local Entity
            var entity = repo.CreateEntity();
            repo.AddComponent(entity, new NetworkIdentity { Value = 888 });
            repo.AddComponent(entity, new NetworkSpawnRequest { DisType = 12, OwnerId = 1 });
            repo.AddComponent(entity, new NetworkOwnership { PrimaryOwnerId = 1, LocalNodeId = 1 });
            repo.AddComponent(entity, new NetworkPosition { Value = new Vector3(10, 20, 30) });
            repo.AddComponent(entity, new NetworkVelocity { Value = new Vector3(0,1,0) });
            repo.AddComponent(entity, new NetworkOrientation { Value = Quaternion.Identity });

            // 3. Scan Egress
            var masterWriter = new MockDataWriter();
            masterTranslator.ScanAndPublish(repo, masterWriter);

            // Verify Master Topic
            Assert.Single(masterWriter.WrittenSamples);
            var masterTopic = (EntityMasterTopic)masterWriter.WrittenSamples[0];
            Assert.Equal(888, masterTopic.EntityId);
            Assert.Equal(12ul, masterTopic.DisTypeValue);

            // 4. Scan State
            var stateWriter = new MockDataWriter();
            stateTranslator.ScanAndPublish(repo, stateWriter);

            // Verify State Topic
            Assert.Single(stateWriter.WrittenSamples);
            var stateTopic = (EntityStateTopic)stateWriter.WrittenSamples[0];
            Assert.Equal(888, stateTopic.EntityId);
            Assert.Equal(10, stateTopic.PositionX);
        }
    }
}

