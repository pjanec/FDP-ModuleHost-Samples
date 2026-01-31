using System;
using System.Collections.Generic;
using System.Numerics;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone.Topics;
using ModuleHost.Network.Cyclone.Translators;
using Xunit;

namespace ModuleHost.Network.Cyclone.Tests.Translators
{
    public class EntityStateTranslatorTests : IDisposable
    {
        private readonly EntityRepository _repo;
        private readonly NodeIdMapper _nodeMapper;
        private readonly Dictionary<long, Entity> _networkIdToEntity;
        private readonly EntityStateTranslator _translator;
        private readonly int _localNodeId = 1;

        public EntityStateTranslatorTests()
        {
            _repo = new EntityRepository();
            
            // Register all required components
            _repo.RegisterComponent<NetworkIdentity>();
            _repo.RegisterComponent<Position>();
            _repo.RegisterComponent<Velocity>();
            _repo.RegisterComponent<NetworkOwnership>();
            _repo.RegisterComponent<NetworkTarget>();
            
            _nodeMapper = new NodeIdMapper(localDomain: 10, localInstance: 100);
            _networkIdToEntity = new Dictionary<long, Entity>();
            _translator = new EntityStateTranslator(
                _nodeMapper,
                _localNodeId,
                _networkIdToEntity);
        }

        public void Dispose()
        {
            _repo?.Dispose();
        }

        [Fact]
        public void EntityStateTranslator_PollIngress_UpdatesPosition()
        {
            // Arrange - Create entity first
            var entityId = 1000L;
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkIdentity { Value = entityId });
            _networkIdToEntity[entityId] = entity;

            var reader = new MockDataReader();
            reader.AddSample(new EntityStateTopic
            {
                EntityId = entityId,
                PositionX = 10.5,
                PositionY = 20.3,
                PositionZ = 30.7,
                VelocityX = 1.0f,
                VelocityY = 2.0f,
                VelocityZ = 3.0f,
                Timestamp = 12345
            });

            var cmd = new MockEntityCommandBuffer();

            // Act
            _translator.PollIngress(reader, cmd, _repo);

            // Assert
            Assert.True(_repo.HasComponent<Position>(entity));
            Assert.True(_repo.HasComponent<Velocity>(entity));

            var position = _repo.GetComponentRO<Position>(entity);
            Assert.Equal(10.5f, position.Value.X, precision: 3);
            Assert.Equal(20.3f, position.Value.Y, precision: 3);
            Assert.Equal(30.7f, position.Value.Z, precision: 3);

            var velocity = _repo.GetComponentRO<Velocity>(entity);
            Assert.Equal(1.0f, velocity.Value.X, precision: 3);
            Assert.Equal(2.0f, velocity.Value.Y, precision: 3);
            Assert.Equal(3.0f, velocity.Value.Z, precision: 3);
        }

        [Fact]
        public void EntityStateTranslator_PollIngress_CreatesGhostEntity()
        {
            // Arrange - Entity doesn't exist yet (State-first scenario)
            var entityId = 2000L;
            var reader = new MockDataReader();
            reader.AddSample(new EntityStateTopic
            {
                EntityId = entityId,
                PositionX = 100.0,
                PositionY = 200.0,
                PositionZ = 300.0,
                VelocityX = 0.0f,
                VelocityY = 0.0f,
                VelocityZ = 0.0f,
                Timestamp = 54321
            });

            var cmd = new MockEntityCommandBuffer();

            // Act
            _translator.PollIngress(reader, cmd, _repo);

            // Assert
            Assert.Single(_networkIdToEntity);
            var entity = _networkIdToEntity[entityId];
            
            // Note: We can't easily check the lifecycle state from outside Fdp.Kernel
            // (GetLifecycleState is internal). Instead, verify entity was created.
            Assert.True(_repo.IsAlive(entity));

            // Verify components were added
            Assert.True(_repo.HasComponent<Position>(entity));
            Assert.True(_repo.HasComponent<Velocity>(entity));
            Assert.True(_repo.HasComponent<NetworkIdentity>(entity));
            Assert.True(_repo.HasComponent<NetworkTarget>(entity));
        }

        [Fact]
        public void EntityStateTranslator_ScanAndPublish_PublishesOwnedEntities()
        {
            // Arrange - Create entity owned by local node
            var entity = _repo.CreateEntity();
            var networkId = 3000L;
            var position = new Vector3(50.0f, 60.0f, 70.0f);
            var velocity = new Vector3(5.0f, 6.0f, 7.0f);

            _repo.AddComponent(entity, new NetworkIdentity { Value = networkId });
            _repo.AddComponent(entity, new Position { Value = position });
            _repo.AddComponent(entity, new Velocity { Value = velocity });
            _repo.AddComponent(entity, new NetworkOwnership
            {
                PrimaryOwnerId = _localNodeId,
                LocalNodeId = _localNodeId
            });

            _networkIdToEntity[networkId] = entity;

            var writer = new MockDataWriter();

            // Act
            _translator.ScanAndPublish(_repo, writer);

            // Assert
            Assert.Single(writer.WrittenSamples);
            var topic = writer.WrittenSamples[0] as EntityStateTopic?;
            Assert.NotNull(topic);
            Assert.Equal(networkId, topic.Value.EntityId);
            Assert.Equal(50.0, topic.Value.PositionX);
            Assert.Equal(60.0, topic.Value.PositionY);
            Assert.Equal(70.0, topic.Value.PositionZ);
            Assert.Equal(5.0f, topic.Value.VelocityX);
            Assert.Equal(6.0f, topic.Value.VelocityY);
            Assert.Equal(7.0f, topic.Value.VelocityZ);
        }

        [Fact]
        public void EntityStateTranslator_ScanAndPublish_SkipsRemoteEntities()
        {
            // Arrange - Create entity owned by remote node
            var entity = _repo.CreateEntity();
            var networkId = 4000L;
            var remoteNodeId = 2; // Not local

            _repo.AddComponent(entity, new NetworkIdentity { Value = networkId });
            _repo.AddComponent(entity, new Position { Value = Vector3.Zero });
            _repo.AddComponent(entity, new Velocity { Value = Vector3.Zero });
            _repo.AddComponent(entity, new NetworkOwnership
            {
                PrimaryOwnerId = remoteNodeId,
                LocalNodeId = _localNodeId
            });

            var writer = new MockDataWriter();

            // Act
            _translator.ScanAndPublish(_repo, writer);

            // Assert - Should not publish remote entities
            Assert.Empty(writer.WrittenSamples);
        }

        [Fact]
        public void EntityStateTranslator_PollIngress_UpdatesNetworkTarget()
        {
            // Arrange
            var entityId = 5000L;
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkIdentity { Value = entityId });
            _networkIdToEntity[entityId] = entity;

            var reader = new MockDataReader();
            var timestamp = 99999L;
            reader.AddSample(new EntityStateTopic
            {
                EntityId = entityId,
                PositionX = 11.0,
                PositionY = 22.0,
                PositionZ = 33.0,
                VelocityX = 0.0f,
                VelocityY = 0.0f,
                VelocityZ = 0.0f,
                Timestamp = timestamp
            });

            var cmd = new MockEntityCommandBuffer();

            // Act
            _translator.PollIngress(reader, cmd, _repo);

            // Assert
            Assert.True(_repo.HasComponent<NetworkTarget>(entity));
            var target = _repo.GetComponentRO<NetworkTarget>(entity);
            Assert.Equal(11.0f, target.Value.X, precision: 3);
            Assert.Equal(22.0f, target.Value.Y, precision: 3);
            Assert.Equal(33.0f, target.Value.Z, precision: 3);
            Assert.Equal(timestamp, target.Timestamp);
        }

        // Mock classes for testing
        private class MockDataReader : IDataReader
        {
            private readonly List<IDataSample> _samples = new();

            public void AddSample(EntityStateTopic topic)
            {
                _samples.Add(new DataSample
                {
                    Data = topic,
                    InstanceState = DdsInstanceState.Alive,
                    EntityId = topic.EntityId
                });
            }

            public IEnumerable<IDataSample> TakeSamples() => _samples;
            public string TopicName => "SST_EntityState";
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
            public string TopicName => "SST_EntityState";
            public void Dispose() { }
        }

        private class MockEntityCommandBuffer : IEntityCommandBuffer
        {
            public Entity CreateEntity() => throw new NotImplementedException();
            public void DestroyEntity(Entity entity) => throw new NotImplementedException();
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
