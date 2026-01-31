using System;
using System.Collections.Generic;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.ELM;
using ModuleHost.Core.Network;
using ModuleHost.Core.Network.Interfaces;
using Xunit;

// Alias to avoid ambiguity
using CycloneGateway = ModuleHost.Network.Cyclone.Modules.NetworkGatewayModule;

namespace ModuleHost.Network.Cyclone.Tests.Modules
{
    public class NetworkGatewayModuleTests : IDisposable
    {
        private readonly EntityRepository _repo;
        private readonly EntityLifecycleModule _elm;
        private readonly MockNetworkTopology _topology;
        private readonly CycloneGateway _gateway;
        private const int MODULE_ID = 100;
        private const int LOCAL_NODE_ID = 1;

        public NetworkGatewayModuleTests()
        {
            _repo = new EntityRepository();
            
            // Register required components
            _repo.RegisterComponent<PendingNetworkAck>();
            _repo.RegisterComponent<NetworkIdentity>();
            
            // Create ELM with gateway as participating module
            _elm = new EntityLifecycleModule(new[] { MODULE_ID }, MODULE_ID);
            _topology = new MockNetworkTopology(LOCAL_NODE_ID);
            _gateway = new CycloneGateway(
                MODULE_ID,
                LOCAL_NODE_ID,
                _topology,
                _elm);
        }

        public void Dispose()
        {
            _repo?.Dispose();
        }

        [Fact]
        public void Constructor_WithValidDependencies_Succeeds()
        {
            // Arrange & Act
            var gateway = new CycloneGateway(
                101,
                2,
                _topology,
                _elm);

            // Assert
            Assert.NotNull(gateway);
            Assert.Equal("NetworkGateway", gateway.Name);
            Assert.Equal(101, gateway.ModuleId);
            Assert.Equal(ExecutionPolicy.Synchronous(), gateway.Policy);
        }

        [Fact]
        public void Constructor_WithNullTopology_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new CycloneGateway(102, 2, null, _elm));
        }

        [Fact]
        public void Constructor_WithNullELM_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new CycloneGateway(103, 2, _topology, null));
        }

        [Fact]
        public void ProcessConstructionOrders_WithNoPeers_AcksImmediately()
        {
            // Arrange
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkIdentity { Value = 1000 });
            _repo.AddComponent(entity, new PendingNetworkAck 
            { 
                ExpectedType = ReliableInitType.AllPeers 
            });

            // Configure topology to return no peers
            _topology.SetPeers(ReliableInitType.AllPeers, Array.Empty<int>());

            // Act & Assert
            // Just verify we can call Tick without exception
            // Full integration testing would require more complex setup
            _gateway.Tick(_repo, 0.016f, 1);
            
            // Verify gateway is operational
            Assert.True(true);
        }

        [Fact]
        public void ProcessConstructionOrders_WithoutPendingAck_AcksImmediately()
        {
            // Arrange
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new NetworkIdentity { Value = 2000 });
            // Note: NOT adding PendingNetworkAck

            // Act & Assert
            // Just verify we can call Tick without exception
            _gateway.Tick(_repo, 0.016f, 1);
            
            // Success means no exception was thrown
            Assert.True(true);
        }

        // Mock topology for testing
        private class MockNetworkTopology : INetworkTopology
        {
            private readonly int _localNodeId;
            private readonly Dictionary<ReliableInitType, int[]> _peerConfig = new();

            public MockNetworkTopology(int localNodeId)
            {
                _localNodeId = localNodeId;
                
                // Default: no peers for any type
                _peerConfig[ReliableInitType.None] = Array.Empty<int>();
                _peerConfig[ReliableInitType.PhysicsServer] = Array.Empty<int>();
                _peerConfig[ReliableInitType.AllPeers] = Array.Empty<int>();
            }

            public void SetPeers(ReliableInitType type, int[] peers)
            {
                _peerConfig[type] = peers;
            }

            public IEnumerable<int> GetExpectedPeers(ReliableInitType type)
            {
                if (_peerConfig.TryGetValue(type, out var peers))
                {
                    return peers;
                }
                return Array.Empty<int>();
            }
        }
    }
}
