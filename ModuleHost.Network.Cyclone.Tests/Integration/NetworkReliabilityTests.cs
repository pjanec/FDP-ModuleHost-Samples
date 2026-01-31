using System;
using Xunit;
using Moq;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone.Modules;
using System.Collections.Generic;
using System.Linq;
using Fdp.Kernel;
using ModuleHost.Core.Network;
using ModuleHost.Core.Network.Systems;
using ModuleHost.Network.Cyclone.Translators;
using ModuleHost.Network.Cyclone.Tests.Mocks;
using ModuleHost.Network.Cyclone.Topics;
using ModuleHost.Core.Abstractions;

namespace ModuleHost.Network.Cyclone.Tests.Integration
{
    public class NetworkReliabilityTests
    {
        [Fact]
        public void Reliability_PacketLoss_10Percent_EntityEventuallyComplete()
        {
            using var repo = new EntityRepository();
            RegisterComponents(repo);
            
            var networkIdToEntity = new Dictionary<long, Entity>();
            var translator = new EntityMasterTranslator(new NodeIdMapper(1, 1), new TypeIdMapper(), 1, networkIdToEntity);
            
            // Generate 100 entity creation packets
            var packets = new List<IDataSample>();
            for (int i = 0; i < 100; i++)
            {
                packets.Add(new DataSample
                {
                    Data = new EntityMasterTopic { EntityId = i, OwnerId = new NetworkAppId { AppDomainId = 1, AppInstanceId = 0 }, DisTypeValue = new DISEntityType { Kind = 1 }.Value },
                    InstanceState = DdsInstanceState.Alive,
                    EntityId = i
                });
            }
            
            // Drop 10% randomly
            var rand = new Random(42);
            var receivedPackets = packets.Where(p => rand.NextDouble() > 0.1).ToList();
            
            // Process
            var reader = new MockDataReader(receivedPackets.ToArray());
            var cmd = ((ISimulationView)repo).GetCommandBuffer();
            translator.PollIngress(reader, cmd, repo);
            ((EntityCommandBuffer)cmd).Playback(repo);
            
            // Should have ~90 entities
            Assert.InRange(networkIdToEntity.Count, 80, 95);
            
            // Re-send missing packets (simulate reliable retry or eventual arrival)
            var missingPackets = packets.Except(receivedPackets).ToList();
            var retryReader = new MockDataReader(missingPackets.ToArray());
            
            translator.PollIngress(retryReader, cmd, repo);
            ((EntityCommandBuffer)cmd).Playback(repo);
            
            // Now should have all 100
            Assert.Equal(100, networkIdToEntity.Count);
        }
        
        [Fact]
        public void Reliability_DuplicatePackets_Idempotency()
        {
            using var repo = new EntityRepository();
            RegisterComponents(repo);
            
            var networkIdToEntity = new Dictionary<long, Entity>();
            var translator = new EntityMasterTranslator(new NodeIdMapper(1, 1), new TypeIdMapper(), 1, networkIdToEntity);
            
            // Create 1 packet, duplicated 5 times
            var packet = new DataSample
            {
                Data = new EntityMasterTopic { EntityId = 100, OwnerId = new NetworkAppId { AppDomainId = 1, AppInstanceId = 0 }, DisTypeValue = new DISEntityType { Kind = 1 }.Value },
                InstanceState = DdsInstanceState.Alive,
                EntityId = 100
            };
            
            var samples = Enumerable.Repeat((IDataSample)packet, 5).ToArray();
            var reader = new MockDataReader(samples);
            var cmd = ((ISimulationView)repo).GetCommandBuffer();
            
            translator.PollIngress(reader, cmd, repo);
            ((EntityCommandBuffer)cmd).Playback(repo);
            
            // Should exist once
            Assert.Single(networkIdToEntity);
            Assert.True(networkIdToEntity.ContainsKey(100));
            Assert.True(((ISimulationView)repo).IsAlive(networkIdToEntity[100]));
        }
        
        [Fact]
        public void Reliability_OutOfOrderPackets_EventualConsistency()
        {
            using var repo = new EntityRepository();
            RegisterComponents(repo);
            
            var networkIdToEntity = new Dictionary<long, Entity>();
            // Scenario: EntityMaster arrives AFTER EntityState (Ghost creation)
            
            var map = new DescriptorOwnershipMap();
            // EntityStateTranslator handles ghost creation if master missing
            var entityStateTranslator = new EntityStateTranslator(new NodeIdMapper(1, 1), 1, networkIdToEntity);
            var entityMasterTranslator = new EntityMasterTranslator(new NodeIdMapper(1, 1), new TypeIdMapper(), 1, networkIdToEntity);
            
            var cmd = ((ISimulationView)repo).GetCommandBuffer();
            
            // 1. Receive EntityState first (for Entity 500)
            var statePacket = new DataSample
            {
                Data = new EntityStateTopic { EntityId = 500 },
                InstanceState = DdsInstanceState.Alive,
                EntityId = 500
            };
            
            entityStateTranslator.PollIngress(new MockDataReader(statePacket), cmd, repo);
            ((EntityCommandBuffer)cmd).Playback(repo);
            
            // Check: Entity should exist as Ghost
            Assert.True(networkIdToEntity.ContainsKey(500));
            var ghost = networkIdToEntity[500];
            Assert.Equal(EntityLifecycle.Ghost, repo.GetHeader(ghost.Index).LifecycleState);
            
            // 2. Receive EntityMaster later
            var masterPacket = new DataSample
            {
                Data = new EntityMasterTopic { EntityId = 500, OwnerId = new NetworkAppId { AppDomainId = 2, AppInstanceId = 0 }, DisTypeValue = new DISEntityType { Kind = 99 }.Value },
                InstanceState = DdsInstanceState.Alive,
                EntityId = 500
            };
            
            entityMasterTranslator.PollIngress(new MockDataReader(masterPacket), cmd, repo);
            ((EntityCommandBuffer)cmd).Playback(repo);
            
            // Check: Entity should now have DIS Type 99 and NetworkSpawnRequest
            var updatedEntity = networkIdToEntity[500];
            Assert.Equal(ghost, updatedEntity);
            
            // Note: Lifecycle remains Ghost until Spawner processes the NetworkSpawnRequest? 
            // Or does MasterTranslator promote it?
            // MasterTranslator adds NetworkSpawnRequest. Spawner will promote to Constructing/Active.
            
            // We check for NetworkSpawnRequest component
            Assert.True(((ISimulationView)repo).HasComponent<NetworkSpawnRequest>(updatedEntity));
            var req = ((ISimulationView)repo).GetComponentRO<NetworkSpawnRequest>(updatedEntity);
            Assert.Equal(99, req.DisType.Kind);
        }
        
        private void RegisterComponents(EntityRepository repo)
        {
            repo.RegisterComponent<NetworkIdentity>();
            repo.RegisterComponent<NetworkSpawnRequest>();
            repo.RegisterComponent<NetworkOwnership>();
            repo.RegisterManagedComponent<DescriptorOwnership>();
            repo.RegisterComponent<Position>();
            repo.RegisterComponent<Velocity>();
            repo.RegisterComponent<NetworkTarget>();
        }
    }
}

