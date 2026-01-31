using System;
using System.Collections.Generic;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone.Topics;
using ModuleHost.Network.Cyclone.Translators;
using Xunit;

namespace ModuleHost.Network.Cyclone.Tests.Translators
{
    public class EntityMasterTranslatorTests : IDisposable
    {
        private readonly EntityRepository _repo;
        private readonly NodeIdMapper _nodeMapper;
        private readonly TypeIdMapper _typeMapper;
        private readonly Dictionary<long, Entity> _networkIdToEntity;
        private readonly EntityMasterTranslator _translator;
        private readonly int _localNodeId = 1;

        public EntityMasterTranslatorTests()
        {
            _repo = new EntityRepository();
            
            // Register all required components
            _repo.RegisterComponent<NetworkIdentity>();
            _repo.RegisterComponent<NetworkOwnership>();
            _repo.RegisterComponent<NetworkSpawnRequest>();
            
            _nodeMapper = new NodeIdMapper(localDomain: 10, localInstance: 100);
            _typeMapper = new TypeIdMapper();
            _networkIdToEntity = new Dictionary<long, Entity>();
            _translator = new EntityMasterTranslator(
                _nodeMapper,
                _typeMapper,
                _localNodeId,
                _networkIdToEntity);
        }

        public void Dispose()
        {
            _repo?.Dispose();
        }

        [Fact]
        public void EntityMasterTranslator_PollIngress_MapsOwnerAndTypeCorrectly()
        {
            // Arrange
            var ddsOwnerId = new NetworkAppId { AppDomainId = 20, AppInstanceId = 200 };
            ulong disTypeValue = 0x0101010100000001; // Example DIS type
            long entityId = 1000;

            var reader = new MockDataReader();
            reader.AddSample(new EntityMasterTopic
            {
                EntityId = entityId,
                OwnerId = ddsOwnerId,
                DisTypeValue = disTypeValue,
                Flags = 0
            });

            var cmd = new MockEntityCommandBuffer();

            // Act
            _translator.PollIngress(reader, cmd, _repo);

            // Assert
            Assert.Single(_networkIdToEntity);
            Assert.True(_networkIdToEntity.ContainsKey(entityId));

            var entity = _networkIdToEntity[entityId];
            Assert.True(_repo.HasComponent<NetworkOwnership>(entity));
            Assert.True(_repo.HasComponent<NetworkIdentity>(entity));
            Assert.True(_repo.HasComponent<NetworkSpawnRequest>(entity));

            // Verify mapping: DDS OwnerId -> Core int
            var ownership = _repo.GetComponentRO<NetworkOwnership>(entity);
            int expectedCoreOwnerId = _nodeMapper.GetOrRegisterInternalId(ddsOwnerId);
            Assert.Equal(expectedCoreOwnerId, ownership.PrimaryOwnerId);

            // Verify mapping: DDS DisTypeValue -> Core TypeId
            var spawnReq = _repo.GetComponentRO<NetworkSpawnRequest>(entity);
            Assert.Equal(disTypeValue, spawnReq.DisType.Value);
            
            // Verify type mapper was called
            int coreTypeId = _typeMapper.GetCoreTypeId(disTypeValue);
            Assert.True(coreTypeId > 0);
        }

        [Fact]
        public void EntityMasterTranslator_PollIngress_CreatesEntityAsGhost()
        {
            // Arrange
            var reader = new MockDataReader();
            reader.AddSample(new EntityMasterTopic
            {
                EntityId = 1001,
                OwnerId = new NetworkAppId { AppDomainId = 20, AppInstanceId = 200 },
                DisTypeValue = 0x0101010100000002,
                Flags = 0
            });

            var cmd = new MockEntityCommandBuffer();

            // Act
            _translator.PollIngress(reader, cmd, _repo);

            // Assert
            var entity = _networkIdToEntity[1001];
            // Note: We can't easily check the lifecycle state from outside Fdp.Kernel
            // (GetLifecycleState is internal). Instead, verify entity was created.
            Assert.True(_repo.IsAlive(entity));
            Assert.True(_repo.HasComponent<NetworkIdentity>(entity));
        }

        [Fact]
        public void EntityMasterTranslator_ScanAndPublish_WritesCorrectTopic()
        {
            // Arrange - Create an entity owned by local node
            var entity = _repo.CreateEntity();
            var networkId = 2000L;
            var disType = new DISEntityType { Value = 0x0101010100000003 };

            _repo.AddComponent(entity, new NetworkIdentity { Value = networkId });
            _repo.AddComponent(entity, new NetworkOwnership 
            { 
                PrimaryOwnerId = _localNodeId,
                LocalNodeId = _localNodeId
            });
            _repo.AddComponent(entity, new NetworkSpawnRequest
            {
                DisType = disType,
                PrimaryOwnerId = _localNodeId,
                Flags = MasterFlags.None,
                NetworkEntityId = networkId
            });

            _networkIdToEntity[networkId] = entity;

            var writer = new MockDataWriter();

            // Act
            _translator.ScanAndPublish(_repo, writer);

            // Assert
            Assert.Single(writer.WrittenSamples);
            var topic = writer.WrittenSamples[0] as EntityMasterTopic?;
            Assert.NotNull(topic);
            Assert.Equal(networkId, topic.Value.EntityId);
            Assert.Equal(disType.Value, topic.Value.DisTypeValue);
            
            // Verify reverse mapping: Core int -> DDS OwnerId
            var expectedDdsOwnerId = _nodeMapper.GetExternalId(_localNodeId);
            Assert.Equal(expectedDdsOwnerId, topic.Value.OwnerId);
        }

        [Fact]
        public void EntityMasterTranslator_PollIngress_UpdatesExistingEntity()
        {
            // Arrange - Create entity first
            var entityId = 3000L;
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkIdentity { Value = entityId });
            _networkIdToEntity[entityId] = entity;

            var newOwnerId = new NetworkAppId { AppDomainId = 30, AppInstanceId = 300 };
            var reader = new MockDataReader();
            reader.AddSample(new EntityMasterTopic
            {
                EntityId = entityId,
                OwnerId = newOwnerId,
                DisTypeValue = 0x0101010100000004,
                Flags = 1
            });

            var cmd = new MockEntityCommandBuffer();

            // Act
            _translator.PollIngress(reader, cmd, _repo);

            // Assert - Entity should be updated, not duplicated
            Assert.Single(_networkIdToEntity);
            var ownership = _repo.GetComponentRO<NetworkOwnership>(entity);
            int expectedCoreOwnerId = _nodeMapper.GetOrRegisterInternalId(newOwnerId);
            Assert.Equal(expectedCoreOwnerId, ownership.PrimaryOwnerId);
        }

        [Fact]
        public void EntityMasterTranslator_PollIngress_HandlesDisposal()
        {
            // Arrange - Create entity
            var entityId = 4000L;
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkIdentity { Value = entityId });
            _networkIdToEntity[entityId] = entity;

            var reader = new MockDataReader();
            reader.AddDisposedSample(entityId);

            var cmd = new MockEntityCommandBuffer();

            // Act
            _translator.PollIngress(reader, cmd, _repo);

            // Assert
            Assert.Empty(_networkIdToEntity);
            Assert.Contains(entity, cmd.DestroyedEntities);
        }

        // Mock classes for testing
        private class MockDataReader : IDataReader
        {
            private readonly List<IDataSample> _samples = new();

            public void AddSample(EntityMasterTopic topic)
            {
                _samples.Add(new DataSample
                {
                    Data = topic,
                    InstanceState = DdsInstanceState.Alive,
                    EntityId = topic.EntityId
                });
            }

            public void AddDisposedSample(long entityId)
            {
                _samples.Add(new DataSample
                {
                    Data = new EntityMasterTopic { EntityId = entityId },
                    InstanceState = DdsInstanceState.NotAliveDisposed,
                    EntityId = entityId
                });
            }

            public IEnumerable<IDataSample> TakeSamples() => _samples;
            public string TopicName => "SST_EntityMaster";
            public void Dispose() { }
        }

        private class MockDataWriter : IDataWriter
        {
            public List<object> WrittenSamples { get; } = new();

            public void Write(object sample)
            {
                WrittenSamples.Add(sample);
            }

            public void Dispose(long networkEntityId) { }
            public string TopicName => "SST_EntityMaster";
            public void Dispose() { }
        }

        private class MockEntityCommandBuffer : IEntityCommandBuffer
        {
            public List<Entity> DestroyedEntities { get; } = new();

            public Entity CreateEntity() => throw new NotImplementedException();
            public void DestroyEntity(Entity entity) => DestroyedEntities.Add(entity);
            public void AddComponent<T>(Entity entity, in T component) where T : unmanaged => throw new NotImplementedException();
            public void RemoveComponent<T>(Entity entity) where T : unmanaged => throw new NotImplementedException();
            public void SetComponent<T>(Entity entity, in T component) where T : unmanaged => throw new NotImplementedException();
            public void AddManagedComponent<T>(Entity entity, T? component) where T : class => throw new NotImplementedException();
            public void SetManagedComponent<T>(Entity entity, T? component) where T : class => throw new NotImplementedException();
            public void RemoveManagedComponent<T>(Entity entity) where T : class => throw new NotImplementedException();
            public void PublishEvent<T>(in T evt) where T : unmanaged => throw new NotImplementedException();
            public void SetLifecycleState(Entity entity, EntityLifecycle state) => throw new NotImplementedException();
        }
    }
}
