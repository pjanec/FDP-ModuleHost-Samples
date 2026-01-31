using System;
using Xunit;
using Moq;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone.Modules;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Fdp.Kernel;
using Fdp.Kernel.Tkb;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.ELM;
using ModuleHost.Core.Network;
using ModuleHost.Core.Network.Interfaces;
using ModuleHost.Network.Cyclone.Topics;
using ModuleHost.Core.Network.Systems;
using ModuleHost.Network.Cyclone.Translators;
using ModuleHost.Network.Cyclone.Tests.Mocks;
using Moq;

namespace ModuleHost.Network.Cyclone.Tests.Integration
{
    public class NetworkELMIntegrationTests : IDisposable
    {
        private EntityRepository _repo;
        private Mock<ITkbDatabase> _mockTkb;
        private EntityLifecycleModule _elm; 
        private Mock<IOwnershipDistributionStrategy> _mockStrategy;
        private int _localNodeId = 1;

        public NetworkELMIntegrationTests()
        {
            _repo = new EntityRepository();
            RegisterComponents(_repo);
            
            _mockTkb = new Mock<ITkbDatabase>();
            _elm = new EntityLifecycleModule(new[] { 1 });
            _mockStrategy = new Mock<IOwnershipDistributionStrategy>();
        }

        private void RegisterComponents(EntityRepository repo)
        {
            repo.RegisterComponent<Position>();
            repo.RegisterComponent<Velocity>();
            repo.RegisterComponent<NetworkIdentity>();
            repo.RegisterComponent<NetworkOwnership>();
            repo.RegisterComponent<NetworkTarget>();
            repo.RegisterManagedComponent<DescriptorOwnership>();
            repo.RegisterComponent<NetworkSpawnRequest>();
            repo.RegisterComponent<PendingNetworkAck>();
            repo.RegisterComponent<ForceNetworkPublish>();
            repo.RegisterManagedComponent<WeaponStates>();
            
            repo.RegisterEvent<ConstructionOrder>();
            repo.RegisterEvent<DescriptorAuthorityChanged>();
        }

        public void Dispose()
        {
            _repo.Dispose();
        }

        private EntityLifecycle GetLifecycleState(Entity entity)
        {
            return _repo.GetHeader(entity.Index).LifecycleState;
        }

        private IEntityCommandBuffer GetCommandBuffer()
        {
            return ((ISimulationView)_repo).GetCommandBuffer();
        }

        // --- TkbTemplate Tests (4 tests) ---

        [Fact]
        public void TkbTemplate_ApplyTo_PreserveExistingFalse_OverwritesComponent()
        {
            var template = new TkbTemplate("TestTemplate");
            template.AddComponent(new Position { Value = new Vector3(10, 10, 10) });

            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new Position { Value = new Vector3(0, 0, 0) });

            template.ApplyTo(_repo, entity, preserveExisting: false);

            var pos = _repo.GetComponentRO<Position>(entity);
            Assert.Equal(new Vector3(10, 10, 10), pos.Value);
        }

        [Fact]
        public void TkbTemplate_ApplyTo_PreserveExistingTrue_KeepsExistingComponent()
        {
            var template = new TkbTemplate("TestTemplate");
            template.AddComponent(new Position { Value = new Vector3(10, 10, 10) });

            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new Position { Value = new Vector3(5, 5, 5) });

            template.ApplyTo(_repo, entity, preserveExisting: true);

            var pos = _repo.GetComponentRO<Position>(entity);
            Assert.Equal(new Vector3(5, 5, 5), pos.Value);
        }

        [Fact]
        public void TkbTemplate_ApplyTo_PreserveExistingTrue_AddsMissingComponent()
        {
            var template = new TkbTemplate("TestTemplate");
            template.AddComponent(new Position { Value = new Vector3(10, 10, 10) });
            template.AddComponent(new Velocity { Value = new Vector3(1, 1, 1) });

            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new Position { Value = new Vector3(5, 5, 5) });

            template.ApplyTo(_repo, entity, preserveExisting: true);

            var pos = _repo.GetComponentRO<Position>(entity);
            Assert.Equal(new Vector3(5, 5, 5), pos.Value); // Preserved

            var vel = _repo.GetComponentRO<Velocity>(entity);
            Assert.Equal(new Vector3(1, 1, 1), vel.Value); // Added
        }

        [Fact]
        public void TkbTemplate_ApplyTo_DefaultParam_IsFalse()
        {
            var template = new TkbTemplate("TestTemplate");
            template.AddComponent(new Position { Value = new Vector3(10, 10, 10) });

            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new Position { Value = new Vector3(0, 0, 0) });

            template.ApplyTo(_repo, entity); // Default

            var pos = _repo.GetComponentRO<Position>(entity);
            Assert.Equal(new Vector3(10, 10, 10), pos.Value);
        }

        // --- EntityStateTranslator Tests (6 tests) ---

        [Fact]
        public void EntityStateTranslator_Ingress_CreatesGhostEntity()
        {
            var translator = new EntityStateTranslator(new NodeIdMapper(1, 1), _localNodeId, new Dictionary<long, Entity>());
            var desc = new EntityStateTopic { EntityId = 100 };
            var reader = new MockDataReader(desc);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            var entity = FindEntityByNetworkId(100);
            Assert.NotEqual(Entity.Null, entity);
            Assert.Equal(EntityLifecycle.Ghost, GetLifecycleState(entity));
        }

        [Fact]
        public void EntityStateTranslator_Ghost_HasCorrectPositionAndVelocity()
        {
            var translator = new EntityStateTranslator(new NodeIdMapper(1, 1), _localNodeId, new Dictionary<long, Entity>());
            var desc = new EntityStateTopic { EntityId = 101, PositionX = 1, PositionY = 2, PositionZ = 3, VelocityX = 4, VelocityY = 5, VelocityZ = 6 };
            var reader = new MockDataReader(desc);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            var entity = FindEntityByNetworkId(101);
            var pos = _repo.GetComponentRO<Position>(entity);
            var vel = _repo.GetComponentRO<Velocity>(entity);

            Assert.Equal(new Vector3(1, 2, 3), pos.Value);
            Assert.Equal(new Vector3(4, 5, 6), vel.Value);
        }

        [Fact]
        public void EntityStateTranslator_Ghost_HasNetworkIdentity()
        {
            var translator = new EntityStateTranslator(new NodeIdMapper(1, 1), _localNodeId, new Dictionary<long, Entity>());
            var desc = new EntityStateTopic { EntityId = 102 };
            var reader = new MockDataReader(desc);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            var entity = FindEntityByNetworkId(102);
            var nid = _repo.GetComponentRO<NetworkIdentity>(entity);
            Assert.Equal(102, nid.Value);
        }

        [Fact]
        public void EntityStateTranslator_Ghost_ExcludedFromStandardQueries()
        {
            var translator = new EntityStateTranslator(new NodeIdMapper(1, 1), _localNodeId, new Dictionary<long, Entity>());
            var desc = new EntityStateTopic { EntityId = 103 };
            var reader = new MockDataReader(desc);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            int count = 0;
            foreach (var e in _repo.Query().Build()) count++;
            Assert.Equal(0, count);

            int countAll = 0;
            foreach (var e in _repo.Query().IncludeAll().Build()) countAll++;
            Assert.Equal(1, countAll);
        }

        [Fact]
        public void EntityStateTranslator_FindEntityByNetworkId_FindsGhosts()
        {
            var translator = new EntityStateTranslator(new NodeIdMapper(1, 1), _localNodeId, new Dictionary<long, Entity>());
            var desc = new EntityStateTopic { EntityId = 104 };
            var reader = new MockDataReader(desc);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            var desc2 = new EntityStateTopic { EntityId = 104, PositionX = 10, PositionY = 10, PositionZ = 10 };
            var reader2 = new MockDataReader(desc2);
            var cmd2 = GetCommandBuffer();

            translator.PollIngress(reader2, cmd2, _repo);
            ((EntityCommandBuffer)cmd2).Playback(_repo);

            var entity = FindEntityByNetworkId(104);
            var pos = _repo.GetComponentRO<NetworkTarget>(entity);
            Assert.Equal(new Vector3(10, 10, 10), pos.Value);
        }



        // --- EntityMasterTranslator Tests (7 tests) ---

        [Fact]
        public void EntityMasterTranslator_MasterFirst_CreatesEntityDirectly()
        {
            var translator = new EntityMasterTranslator(new NodeIdMapper(1, 1), new TypeIdMapper(), _localNodeId, new Dictionary<long, Entity>());
            var desc = new EntityMasterTopic { EntityId = 200, OwnerId = new NetworkAppId { AppDomainId = 2, AppInstanceId = 0 }, DisTypeValue = new DISEntityType { Kind = 1 }.Value };
            var reader = new MockDataReader(desc);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            var entity = FindEntityByNetworkId(200);
            Assert.NotEqual(Entity.Null, entity);
            Assert.NotEqual(EntityLifecycle.Ghost, GetLifecycleState(entity));
        }

        [Fact]
        public void EntityMasterTranslator_MasterAfterGhost_FindsExisting()
        {
            var entity = _repo.CreateEntity();
            _repo.SetLifecycleState(entity, EntityLifecycle.Ghost);
            _repo.AddComponent(entity, new NetworkIdentity { Value = 201 });
            var map = new Dictionary<long, Entity> { { 201, entity } };

            var translator = new EntityMasterTranslator(new NodeIdMapper(1, 1), new TypeIdMapper(), _localNodeId, map);
            var desc = new EntityMasterTopic { EntityId = 201, OwnerId = new NetworkAppId { AppDomainId = 2, AppInstanceId = 0 }, DisTypeValue = new DISEntityType { Kind = 1 }.Value };
            var reader = new MockDataReader(desc);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            int count = 0;
            foreach(var e in _repo.Query().IncludeAll().Build()) count++;
            Assert.Equal(1, count);
        }

        [Fact]
        public void EntityMasterTranslator_AddsNetworkSpawnRequest()
        {
            var translator = new EntityMasterTranslator(new NodeIdMapper(1, 1), new TypeIdMapper(), _localNodeId, new Dictionary<long, Entity>());
            var desc = new EntityMasterTopic { EntityId = 202, OwnerId = new NetworkAppId { AppDomainId = 3, AppInstanceId = 0 }, DisTypeValue = new DISEntityType { Kind = 99 }.Value, Flags = (int)MasterFlags.ReliableInit };
            var reader = new MockDataReader(desc);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            var entity = FindEntityByNetworkId(202);
            Assert.True(_repo.HasComponent<NetworkSpawnRequest>(entity));
            var req = _repo.GetComponentRO<NetworkSpawnRequest>(entity);
            Assert.Equal(99, req.DisType.Kind);
            Assert.Equal(MasterFlags.ReliableInit, req.Flags);
            Assert.Equal(2, req.PrimaryOwnerId);
        }

        [Fact]
        public void EntityMasterTranslator_SetsNetworkOwnership()
        {
            var translator = new EntityMasterTranslator(new NodeIdMapper(1, 1), new TypeIdMapper(), _localNodeId, new Dictionary<long, Entity>());
            var desc = new EntityMasterTopic { EntityId = 203, OwnerId = new NetworkAppId { AppDomainId = 4, AppInstanceId = 0 } };
            var reader = new MockDataReader(desc);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            var entity = FindEntityByNetworkId(203);
            var nw = _repo.GetComponentRO<NetworkOwnership>(entity);
            Assert.Equal(2, nw.PrimaryOwnerId);
        }

        [Fact]
        public void EntityMasterTranslator_Disposal_DestroysEntity()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkIdentity { Value = 204 });
            var map = new Dictionary<long, Entity> { { 204, entity } };
            
            var translator = new EntityMasterTranslator(new NodeIdMapper(1, 1), new TypeIdMapper(), _localNodeId, map);
            var desc = new EntityMasterTopic { EntityId = 204 };
            var sample = new MockDataSample { Data = desc, InstanceState = DdsInstanceState.NotAliveDisposed };
            var reader = new MockDataReader(sample);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            Assert.False(_repo.IsAlive(entity));
            Assert.False(map.ContainsKey(204));
        }

        [Fact]
        public void EntityMasterTranslator_MultipleDescriptors_Handled()
        {
            var translator = new EntityMasterTranslator(new NodeIdMapper(1, 1), new TypeIdMapper(), _localNodeId, new Dictionary<long, Entity>());
            var reader = new MockDataReader(
                new EntityMasterTopic { EntityId = 205, OwnerId = new NetworkAppId { AppDomainId = 1, AppInstanceId = 0 } },
                new EntityMasterTopic { EntityId = 206, OwnerId = new NetworkAppId { AppDomainId = 1, AppInstanceId = 0 } }
            );
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            Assert.NotEqual(Entity.Null, FindEntityByNetworkId(205));
            Assert.NotEqual(Entity.Null, FindEntityByNetworkId(206));
        }

        [Fact]
        public void EntityMasterTranslator_InvalidData_DoesNotCrash()
        {
            var translator = new EntityMasterTranslator(new NodeIdMapper(1, 1), new TypeIdMapper(), _localNodeId, new Dictionary<long, Entity>());
            var reader = new MockDataReader("InvalidObject");
            var cmd = GetCommandBuffer();
            translator.PollIngress(reader, cmd, _repo);
        }

        // --- NetworkSpawnerSystem Tests (10 tests) ---

        [Fact]
        public void NetworkSpawnerSystem_GhostPromotion_PreservesPosition()
        {
            var entity = _repo.CreateEntity();
            _repo.SetLifecycleState(entity, EntityLifecycle.Ghost);
            _repo.AddComponent(entity, new Position { Value = new Vector3(100, 0, 0) });
            _repo.AddComponent(entity, new NetworkSpawnRequest { DisType = new DISEntityType { Kind = 1 } });
            
            var template = new TkbTemplate("TestTank");
            template.AddComponent(new Position { Value = Vector3.Zero }); 
            _mockTkb.Setup(x => x.GetTemplateByEntityType(It.IsAny<DISEntityType>())).Returns(template);

            var system = new NetworkSpawnerSystem(_mockTkb.Object, _elm, _mockStrategy.Object, _localNodeId);
            system.Execute(_repo, 0.1f);

            var pos = _repo.GetComponentRO<Position>(entity);
            Assert.Equal(new Vector3(100, 0, 0), pos.Value); 
            Assert.Equal(EntityLifecycle.Constructing, GetLifecycleState(entity));
        }

        [Fact]
        public void NetworkSpawnerSystem_NewEntity_AppliesTemplate()
        {
            var entity = _repo.CreateEntity(); 
            _repo.AddComponent(entity, new NetworkSpawnRequest { DisType = new DISEntityType { Kind = 1 } });

            var template = new TkbTemplate("TestTank");
            template.AddComponent(new Position { Value = new Vector3(5, 5, 5) });
            _mockTkb.Setup(x => x.GetTemplateByEntityType(It.IsAny<DISEntityType>())).Returns(template);

            var system = new NetworkSpawnerSystem(_mockTkb.Object, _elm, _mockStrategy.Object, _localNodeId);
            system.Execute(_repo, 0.1f);

            var pos = _repo.GetComponentRO<Position>(entity);
            Assert.Equal(new Vector3(5, 5, 5), pos.Value); 
        }

        [Fact]
        public void NetworkSpawnerSystem_StrategyNull_UsesPrimaryOwner()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkSpawnRequest { DisType = new DISEntityType { Kind = 1 }, PrimaryOwnerId = 9 });

            var template = new TkbTemplate("Test");
            _mockTkb.Setup(x => x.GetTemplateByEntityType(It.IsAny<DISEntityType>())).Returns(template);
            
            _mockStrategy.Setup(x => x.GetInitialOwner(It.IsAny<long>(), It.IsAny<DISEntityType>(), It.IsAny<int>(), It.IsAny<long>()))
                .Returns((int?)null);

            var system = new NetworkSpawnerSystem(_mockTkb.Object, _elm, _mockStrategy.Object, _localNodeId);
            system.Execute(_repo, 0.1f);

            var doComp = _repo.GetComponentRO<DescriptorOwnership>(entity); // Read Managed via GetComponentRO
            Assert.Empty(doComp.Map);
        }

        [Fact]
        public void NetworkSpawnerSystem_StrategySpecific_PopulatesMap()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkSpawnRequest { DisType = new DISEntityType { Kind = 1 }, PrimaryOwnerId = 9 });

            var template = new TkbTemplate("Test");
            _mockTkb.Setup(x => x.GetTemplateByEntityType(It.IsAny<DISEntityType>())).Returns(template);
            
            _mockStrategy.Setup(x => x.GetInitialOwner(NetworkConstants.WEAPON_STATE_DESCRIPTOR_ID, It.IsAny<DISEntityType>(), It.IsAny<int>(), It.IsAny<long>()))
                .Returns(5); 

            var system = new NetworkSpawnerSystem(_mockTkb.Object, _elm, _mockStrategy.Object, _localNodeId);
            system.Execute(_repo, 0.1f);

            var doComp = _repo.GetComponentRO<DescriptorOwnership>(entity);
            Assert.True(doComp.Map.ContainsKey(OwnershipExtensions.PackKey(NetworkConstants.WEAPON_STATE_DESCRIPTOR_ID, 0)));
            Assert.Equal(5, doComp.Map[OwnershipExtensions.PackKey(NetworkConstants.WEAPON_STATE_DESCRIPTOR_ID, 0)]);
        }

        [Fact]
        public void NetworkSpawnerSystem_ReliableInit_AddsPendingNetworkAck()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkSpawnRequest { Flags = MasterFlags.ReliableInit, DisType = new DISEntityType { Kind = 1 } });
            _mockTkb.Setup(x => x.GetTemplateByEntityType(It.IsAny<DISEntityType>())).Returns(new TkbTemplate("T"));

            var system = new NetworkSpawnerSystem(_mockTkb.Object, _elm, _mockStrategy.Object, _localNodeId);
            system.Execute(_repo, 0.1f);

            Assert.True(_repo.HasComponent<PendingNetworkAck>(entity));
        }

        [Fact]
        public void NetworkSpawnerSystem_FastInit_NoPendingNetworkAck()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkSpawnRequest { Flags = MasterFlags.None, DisType = new DISEntityType { Kind = 1 } });
            _mockTkb.Setup(x => x.GetTemplateByEntityType(It.IsAny<DISEntityType>())).Returns(new TkbTemplate("T"));

            var system = new NetworkSpawnerSystem(_mockTkb.Object, _elm, _mockStrategy.Object, _localNodeId);
            system.Execute(_repo, 0.1f);

            Assert.False(_repo.HasComponent<PendingNetworkAck>(entity));
        }

        [Fact]
        public void NetworkSpawnerSystem_CallsElmBeginConstruction()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkSpawnRequest { DisType = new DISEntityType { Kind = 1 } });
            _mockTkb.Setup(x => x.GetTemplateByEntityType(It.IsAny<DISEntityType>())).Returns(new TkbTemplate("TemplateName"));

            var system = new NetworkSpawnerSystem(_mockTkb.Object, _elm, _mockStrategy.Object, _localNodeId);
            
            system.Execute(_repo, 0.1f);

            // Execute creates local command buffer, publishes event, and we need to process it.
            // But thread local buffer is not automatically played back?
            // "view.GetCommandBuffer()" returns thread local buffer.
            // If we don't playback, event is not in bus.
            // Issue: Test calls "Execute" which uses INTERNAL buffer. We can't access it to playback.
            // UNLESS: "GetCommandBuffer" returns the SAME buffer if main thread?
            // "view.GetCommandBuffer()" calls "EntityRepository.GetCommandBuffer()".
            
            // To verify ELM call:
            // ELM state changes only if ConstructionOrder is PROCESSED.
            // But ConstructionOrder is published via CmdBuffer.
            // We need to flush the buffer.
            // Since we can't flush thread-local buffer easily in test, we might check if ELM was called directly?
            // Wait, NetworkSpawnerSystem calls `_elm.BeginConstruction` DIRECTLY.
            // `BeginConstruction` adds to `_pendingConstruction` dictionary immediately.
            // THEN it calls `cmd.PublishEvent`.
            // So we can check `_elm.GetStatistics()` immediately!
            
            var stats = _elm.GetStatistics();
            Assert.Equal(1, stats.pending);
        }

        [Fact]
        public void NetworkSpawnerSystem_RemovesRequest_AfterProcessing()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkSpawnRequest { DisType = new DISEntityType { Kind = 1 } });
            _mockTkb.Setup(x => x.GetTemplateByEntityType(It.IsAny<DISEntityType>())).Returns(new TkbTemplate("T"));

            var system = new NetworkSpawnerSystem(_mockTkb.Object, _elm, _mockStrategy.Object, _localNodeId);
            system.Execute(_repo, 0.1f);

            Assert.False(_repo.HasComponent<NetworkSpawnRequest>(entity));
        }

        [Fact]
        public void NetworkSpawnerSystem_MissingTemplate_LogsErrorButRemovesRequest()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkSpawnRequest { DisType = new DISEntityType { Kind = 255 } });
            _mockTkb.Setup(x => x.GetTemplateByEntityType(It.IsAny<DISEntityType>())).Returns((TkbTemplate)null);

            var system = new NetworkSpawnerSystem(_mockTkb.Object, _elm, _mockStrategy.Object, _localNodeId);
            system.Execute(_repo, 0.1f);

            Assert.False(_repo.HasComponent<NetworkSpawnRequest>(entity));
        }

        [Fact]
        public void NetworkSpawnerSystem_ExceptionSafe()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkSpawnRequest { DisType = new DISEntityType { Kind = 1 } });
            _mockTkb.Setup(x => x.GetTemplateByEntityType(It.IsAny<DISEntityType>())).Throws(new Exception("Fail"));

            var system = new NetworkSpawnerSystem(_mockTkb.Object, _elm, _mockStrategy.Object, _localNodeId);
            
            system.Execute(_repo, 0.1f);
            
            Assert.False(_repo.HasComponent<NetworkSpawnRequest>(entity));
        }

        // --- OwnershipUpdateTranslator Tests (5 tests) ---

        [Fact]
        public void OwnershipUpdate_Acquired_EmitsEventIsNowOwnerTrue()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkOwnership { LocalNodeId = _localNodeId, PrimaryOwnerId = 9 });
            _repo.SetComponent(entity, new DescriptorOwnership());
            var map = new Dictionary<long, Entity> { { 300, entity } };
            
            var translator = new OwnershipUpdateTranslator(_localNodeId, new DescriptorOwnershipMap(), map);
            var update = new OwnershipUpdate { EntityId = 300, DescrTypeId = 1, NewOwner = _localNodeId };
            var reader = new MockDataReader(update);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);
            
            // Events are in bus current buffer after playback? 
            // Playback calls Bus.PublishRaw.
            // To read, must SwapBuffers.
            _repo.Bus.SwapBuffers();
            var events = _repo.Bus.Consume<DescriptorAuthorityChanged>();
            
            Assert.False(events.IsEmpty);
            Assert.True(events[0].IsNowOwner);
            Assert.Equal(entity, events[0].Entity);
            
            Assert.True(_repo.HasComponent<ForceNetworkPublish>(entity));
        }

        [Fact]
        public void OwnershipUpdate_Lost_EmitsEventIsNowOwnerFalse()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkOwnership { LocalNodeId = _localNodeId, PrimaryOwnerId = _localNodeId });
            _repo.SetComponent(entity, new DescriptorOwnership { Map = new Dictionary<long, int> { { OwnershipExtensions.PackKey(1, 0), _localNodeId } } });
            var map = new Dictionary<long, Entity> { { 301, entity } };

            var translator = new OwnershipUpdateTranslator(_localNodeId, new DescriptorOwnershipMap(), map);
            var update = new OwnershipUpdate { EntityId = 301, DescrTypeId = 1, NewOwner = 5 }; 
            var reader = new MockDataReader(update);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);
            
            _repo.Bus.SwapBuffers();
            var events = _repo.Bus.Consume<DescriptorAuthorityChanged>();

            Assert.False(events.IsEmpty);
            Assert.False(events[0].IsNowOwner);

            Assert.False(_repo.HasComponent<ForceNetworkPublish>(entity));
            var doComp = _repo.GetComponentRO<DescriptorOwnership>(entity);
            Assert.Equal(5, doComp.Map[OwnershipExtensions.PackKey(1, 0)]);
        }

        [Fact]
        public void OwnershipUpdate_NewOwner_AddsForceNetworkPublish()
        {
             var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkOwnership { LocalNodeId = _localNodeId, PrimaryOwnerId = 9 });
            _repo.SetComponent(entity, new DescriptorOwnership());
            var map = new Dictionary<long, Entity> { { 302, entity } };
            
            var translator = new OwnershipUpdateTranslator(_localNodeId, new DescriptorOwnershipMap(), map);
            var update = new OwnershipUpdate { EntityId = 302, DescrTypeId = 1, NewOwner = _localNodeId };
            var reader = new MockDataReader(update);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            Assert.True(_repo.HasComponent<ForceNetworkPublish>(entity));
        }

        [Fact]
        public void OwnershipUpdate_LostOwner_NoForceNetworkPublish()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkOwnership { LocalNodeId = _localNodeId, PrimaryOwnerId = _localNodeId });
            _repo.SetComponent(entity, new DescriptorOwnership());
            var map = new Dictionary<long, Entity> { { 303, entity } };
            
            var translator = new OwnershipUpdateTranslator(_localNodeId, new DescriptorOwnershipMap(), map);
            var update = new OwnershipUpdate { EntityId = 303, DescrTypeId = 1, NewOwner = 9 };
            var reader = new MockDataReader(update);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);

            Assert.False(_repo.HasComponent<ForceNetworkPublish>(entity));
        }

        [Fact]
        public void OwnershipUpdate_NoChange_NoEvent()
        {
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkOwnership { LocalNodeId = _localNodeId, PrimaryOwnerId = _localNodeId });
            _repo.SetComponent(entity, new DescriptorOwnership { Map = new Dictionary<long, int> { { OwnershipExtensions.PackKey(1, 0), _localNodeId } } });
            var map = new Dictionary<long, Entity> { { 304, entity } };
            
            var translator = new OwnershipUpdateTranslator(_localNodeId, new DescriptorOwnershipMap(), map);
            var update = new OwnershipUpdate { EntityId = 304, DescrTypeId = 1, NewOwner = _localNodeId }; 
            var reader = new MockDataReader(update);
            var cmd = GetCommandBuffer();

            translator.PollIngress(reader, cmd, _repo);
            ((EntityCommandBuffer)cmd).Playback(_repo);
            
            _repo.Bus.SwapBuffers();
            var events = _repo.Bus.Consume<DescriptorAuthorityChanged>();

            Assert.True(events.IsEmpty);

            var doComp = _repo.GetComponentRO<DescriptorOwnership>(entity);
            Assert.Equal(_localNodeId, doComp.Map[OwnershipExtensions.PackKey(1, 0)]);
        }

        private Entity FindEntityByNetworkId(long id)
        {
            foreach (var e in _repo.Query().IncludeAll().Build())
            {
                if (_repo.HasComponent<NetworkIdentity>(e))
                {
                    if (_repo.GetComponentRO<NetworkIdentity>(e).Value == id) return e;
                }
            }
            return Entity.Null;
        }
    }
}

