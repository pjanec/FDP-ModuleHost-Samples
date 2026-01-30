# Task EXT-2-7: ID Allocator Server (Test Infrastructure)

**Added to Phase 2** after Task EXT-2-6

## Description
Implement a simple ID Allocator Server for testing `DdsIdAllocator`. This server will be used in integration tests and can also serve as a standalone test tool.

## Implementation

### Step 1: Create DdsIdAllocatorServer.cs

Location: `ModuleHost.Network.Cyclone/Services/DdsIdAllocatorServer.cs`

```csharp
using CycloneDDS.Runtime;
using ModuleHost.Network.Cyclone.Topics;

namespace ModuleHost.Network.Cyclone.Services
{
    /// <summary>
    /// Simple ID Allocator Server for testing.
    /// Handles Alloc, Reset, and GetStatus requests.
    /// One server per exercise session.
    /// </summary>
    public class DdsIdAllocatorServer : IDisposable
    {
        private readonly DdsReader<IdRequest, IdRequest> _requestReader;
        private readonly DdsWriter<IdResponse> _responseWriter;
        private readonly DdsWriter<IdStatus> _statusWriter;
        
        private ulong _nextId = 1;
        private readonly Dictionary<string, long> _clientRequestCounters = new();

        public DdsIdAllocatorServer(DdsParticipant participant)
        {
            _requestReader = new DdsReader<IdRequest, IdRequest>(participant, "IdAlloc_Request");
            _responseWriter = new DdsWriter<IdResponse>(participant, "IdAlloc_Response");
            _statusWriter = new DdsWriter<IdStatus>(participant, "IdAlloc_Status");
            
            PublishStatus(); // Initial status
        }

        public void ProcessRequests()
        {
            using var scope = _requestReader.Take();
            
            foreach (var request in scope)
            {
                HandleRequest(request);
            }
        }

        private void HandleRequest(IdRequest request)
        {
            switch (request.Type)
            {
                case EIdRequestType.Alloc:
                    HandleAlloc(request);
                    break;
                
                case EIdRequestType.Reset:
                    HandleReset(request);
                    break;
                
                case EIdRequestType.GetStatus:
                    PublishStatus();
                    break;
            }
        }

        private void HandleAlloc(IdRequest request)
        {
            ulong start = _nextId;
            ulong count = request.Count;
            
            _nextId += count;
            
            _responseWriter.Write(new IdResponse
            {
                ClientId = request.ClientId,
                ReqNo = request.ReqNo,
                Type = EIdResponseType.Alloc,
                Start = start,
                Count = count
            });
            
            PublishStatus();
        }

        private void HandleReset(IdRequest request)
        {
            // Global reset (empty ClientId) or specific client
            bool isGlobal = string.IsNullOrEmpty(request.ClientId.ToString());
            
            _nextId = request.Start;
            
            if (isGlobal)
            {
                // Tell all clients to reset
                _responseWriter.Write(new IdResponse
                {
                    ClientId = new FixedString64(""), // Broadcast
                    ReqNo = 0,
                    Type = EIdResponseType.Reset,
                    Start = 0,
                    Count = 0
                });
            }
            
            PublishStatus();
        }

        private void PublishStatus()
        {
            _statusWriter.Write(new IdStatus
            {
                HighestIdAllocated = _nextId - 1
            });
        }

        public void Dispose()
        {
            _requestReader?.Dispose();
            _responseWriter?.Dispose();
            _statusWriter?.Dispose();
        }
    }
}
```

### Step 2: Create Integration Test

Location: `ModuleHost.Network.Cyclone.Tests/Integration/IdAllocatorIntegrationTests.cs`

```csharp
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
        Task.Run(async () =>
        {
            while (true)
            {
                _server.ProcessRequests();
                await Task.Delay(10);
            }
        });
        
        // Client allocates IDs
        long id1 = client.AllocateId();
        long id2 = client.AllocateId();
        
        Assert.InRange(id1, 1, 100);
        Assert.InRange(id2, 1, 100);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Server_Reset_ClearsAllClients()
    {
        using var client1 = new DdsIdAllocator(_participant, "Client1");
        using var client2 = new DdsIdAllocator(_participant, "Client2");
        
        // Allocate some IDs
        client1.AllocateId();
        client2.AllocateId();
        
        // Server sends global reset
        client1.Reset(1000); // This sends global reset request
        
        _server.ProcessRequests();
        
        // Next allocations should start from 1000
        long newId = client1.AllocateId();
        Assert.InRange(newId, 1000, 1100);
    }

    public void Dispose()
    {
        _server?.Dispose();
        _participant?.Dispose();
    }
}
```

### Step 3: OPTIONAL - Demo Application

Location: `Fdp.Examples.IdAllocatorDemo/Program.cs`

```csharp
class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== ID Allocator Demo ===\n");
        
        using var participant = new DdsParticipant(domainId: 0);
        
        // Start server
        using var server = new DdsIdAllocatorServer(participant);
        var serverTask = Task.Run(async () =>
        {
            while (true)
            {
                server.ProcessRequests();
                await Task.Delay(100);
            }
        });
        
        // Create clients
        var client1 = new DdsIdAllocator(participant, "Client_A");
        var client2 = new DdsIdAllocator(participant, "Client_B");
        
        // Demonstrate allocation
        Console.WriteLine("Client A allocates 5 IDs:");
        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine($"  ID: {client1.AllocateId()}");
        }
        
        Console.WriteLine("\nClient B allocates 5 IDs:");
        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine($"  ID: {client2.AllocateId()}");
        }
        
        // Demonstrate reset
        Console.WriteLine("\nGlobal Reset to 1000...");
        client1.Reset(1000);
        await Task.Delay(500);
        
        Console.WriteLine("Client A allocates after reset:");
        for (int i = 0; i < 3; i++)
        {
            Console.WriteLine($"  ID: {client1.AllocateId()}");
        }
    }
}
```

## Success Criteria
- ✅ Server compiles and implements protocol
- ✅ Integration test passes (server + client roundtrip)
- ✅ Reset protocol tested (global and client-specific)
- ✅ Can be used in Task EXT-2-3 tests
- ✅ Demo app shows protocol in action (optional)

## References
- **[EXTRACTION-REFINEMENTS.md § ID Allocator Protocol](../EXTRACTION-REFINEMENTS.md#2-id-allocator-protocol-detailed-spec)** - Complete protocol spec

## Dependencies
- Requires Task EXT-2-2 (DDS Topics) complete
- Used by Task EXT-2-3 (DdsIdAllocator tests)

