using System;
using System.Threading.Tasks;
using Xunit;
using CycloneDDS.Runtime;
using ModuleHost.Network.Cyclone.Services;

namespace ModuleHost.Network.Cyclone.Tests.Integration
{
    public class IdAllocatorIntegrationTests : IDisposable
    {
        private readonly DdsParticipant _participant;
        private readonly DdsIdAllocatorServer _server;

        public IdAllocatorIntegrationTests()
        {
            _participant = new DdsParticipant(domainId: 99); // Test domain
            _server = new DdsIdAllocatorServer(_participant);
        }

        [Fact]
        public void Server_And_Client_Roundtrip()
        {
            // Create client
            using var client = new DdsIdAllocator(_participant, "TestClient");
            
            // Server processes requests in background
            var stopServer = false;
            var serverTask = Task.Run(async () =>
            {
                while (!stopServer)
                {
                    _server.ProcessRequests();
                    await Task.Delay(10);
                }
            });
            
            try 
            {
                // Client allocates IDs
                // AllocateId might block initially, so we need the server running
                long id1 = client.AllocateId();
                long id2 = client.AllocateId();
                
                // Assert
                // Default server start is 1
                // Default chunk size is 100
                Assert.InRange(id1, 1, 100);
                Assert.InRange(id2, 1, 100);
                Assert.NotEqual(id1, id2);
            }
            finally
            {
                stopServer = true;
                serverTask.Wait(1000);
            }
        }

        [Fact]
        public void Server_Reset_ClearsAllClients()
        {
            using var client1 = new DdsIdAllocator(_participant, "Client1");
            using var client2 = new DdsIdAllocator(_participant, "Client2");
            
             // Server processes requests in background
            var stopServer = false;
            var serverTask = Task.Run(async () =>
            {
                while (!stopServer)
                {
                    _server.ProcessRequests();
                    await Task.Delay(10);
                }
            });

            try
            {
                // Allocate some IDs to "dirty" the state
                client1.AllocateId();
                client2.AllocateId();
                
                // Server sends global reset, starting from 1000
                client1.Reset(1000); 
                
                // Wait for reset to propagate
                System.Threading.Thread.Sleep(500);
                
                // Next allocations should start from 1000
                long newId = client1.AllocateId();
                Assert.InRange(newId, 1000, 1100);
            }
             finally
            {
                stopServer = true;
                serverTask.Wait(1000);
            }
        }

        public void Dispose()
        {
            _server?.Dispose();
            _participant?.Dispose();
        }
    }
}
