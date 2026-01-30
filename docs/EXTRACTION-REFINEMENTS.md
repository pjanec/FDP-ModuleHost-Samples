# ModuleHost.Core Extraction - Design Refinements

**Document Version:** 1.1  
**Date:** 2026-01-30  
**Status:** Refinements Based on Additional Context

---

## New Information Incorporated

### 1. FastCycloneDDS Binding Architecture

**Key Insights from README:**

#### Zero-Allocation Design
The FastCycloneDDS C# bindings use:
- **Zero-Copy Reads**: Direct access to native DDS buffers using `ref struct` views
- **Serdata Integration**: Bypasses legacy C marshalling
- **Lazy Deserialization**: Only parse fields when accessed
- **ArrayPool**: Pooled buffers for writes

**Implications for Extraction:**
- ✅ `DdsIdAllocator`, `DdsWriter<T>`, `DdsReader<T>` are high-performance
- ✅ No need to optimize network layer further - it's already optimal
- ✅ Focus extraction effort on **architectural separation**, not performance tweaks

#### Sender Tracking Feature
```csharp
participant.EnableSenderTracking(new SenderIdentityConfig
{
    AppDomainId = 1,      // These map to NetworkAppId!
    AppInstanceId = 100
});
```

**Connection to NodeIdMapper:**
- `NetworkAppId { AppDomainId, AppInstanceId }` comes from **SenderTracking**
- NodeIdMapper maps this to `int OwnerNodeId` for Core
- This is how Core learns "who owns this entity"

---

## 2. ID Allocator Protocol (Detailed Spec)

### Protocol Overview

**One Server Per Exercise Session:**
- Server runs on a central node (e.g., physics server)
- Clients request chunks of IDs (e.g., 100 IDs at a time)
- Each client maintains a local pool, requests more when low

**Topics (IDL):**
```idl
// Request (from client): reliable, volatile, keep_all
struct IdRequest {
    string ClientId; // KEY (e.g., "Client_100")
    long ReqNo;      // Incrementing request counter
    EIdRequestType Type;
    unsigned long Start;
    unsigned long Count;
};

// Response (from server): reliable, volatile, keep_all
struct IdResponse {
    string ClientId; // KEY (matches request)
    long ReqNo;      // Matches request number
    EIdResponseType Type;
    unsigned long Start; // First ID in chunk
    unsigned long Count; // Number of IDs
};

// Status (from server): reliable, transient_local, keep_last
struct IdStatus {
    unsigned long HighestIdAllocated;
};
```

### Request Types

**EIdRequestType_Alloc** (Normal Operation):
- Client: `{ Type=Alloc, Count=100 }`
- Server: `{ Type=Alloc, Start=1000, Count=100 }`
- Client now owns IDs 1000-1099

**EIdRequestType_Reset** (Session Restart):
- Used at start of live exercise: `Reset(Start=0)`
  - Keeps IDs small and readable (1, 2, 3...)
- Used at start of replay: `Reset(Start=HighestIdFromLive)`
  - Prevents new IDs from colliding with recorded IDs

**EIdRequestType_GetStatus** (Health Check):
- Client: `{ Type=GetStatus }`
- Server: Publishes new `IdStatus` sample

### DdsIdAllocator Implementation Impact

**Current Design (from Task EXT-2-3):**
```csharp
public class DdsIdAllocator : INetworkIdAllocator
{
    private Queue<long> _availableIds = new();
    
    public long AllocateId()
    {
        if (_availableIds.Count < 10) // Low threshold
        {
            RequestMoreIds(100); // Request chunk
        }
        return _availableIds.Dequeue();
    }
}
```

**Refined Implementation (based on protocol):**
```csharp
public class DdsIdAllocator : INetworkIdAllocator
{
    private readonly DdsWriter<IdRequest> _requestWriter;
    private readonly DdsReader<IdResponse, IdResponse> _responseReader;
    private readonly DdsReader<IdStatus, IdStatus> _statusReader;
    private readonly string _clientId;
    private long _requestCounter = 0;
    private Queue<long> _availableIds = new();

    private const int CHUNK_SIZE = 100;
    private const int LOW_WATER_MARK = 10;

    public DdsIdAllocator(DdsParticipant participant, string clientId)
    {
        _clientId = clientId;
        
        // Create request writer
        _requestWriter = new DdsWriter<IdRequest>(participant, "IdAlloc_Request");
        
        // Create response reader (filter by our ClientId)
        _responseReader = new DdsReader<IdResponse, IdResponse>(participant, "IdAlloc_Response");
        _responseReader.SetFilter(r => r.ClientId == _clientId);
        
        // Create status reader
        _statusReader = new DdsReader<IdStatus, IdStatus>(participant, "IdAlloc_Status");
        
        // Initial request
        RequestChunk(CHUNK_SIZE);
    }

    public long AllocateId()
    {
        // Poll for responses in case we haven't processed them yet
        ProcessResponses();
        
        if (_availableIds.Count < LOW_WATER_MARK)
        {
            RequestChunk(CHUNK_SIZE);
        }
        
        if (_availableIds.Count == 0)
        {
            throw new InvalidOperationException("ID pool exhausted");
        }
        
        return _availableIds.Dequeue();
    }

    private void RequestChunk(int count)
    {
        _requestWriter.Write(new IdRequest
        {
            ClientId = _clientId,
            ReqNo = _requestCounter++,
            Type = EIdRequestType.Alloc,
            Start = 0, // Unused for Alloc
            Count = (ulong)count
        });
    }

    private void ProcessResponses()
    {
        using var scope = _responseReader.Take(); // Zero-copy loan
        
        foreach (var response in scope)
        {
            if (response.Type == EIdResponseType.Alloc)
            {
                // Add chunk to local pool
                for (ulong i = 0; i < response.Count; i++)
                {
                    _availableIds.Enqueue((long)(response.Start + i));
                }
            }
            else if (response.Type == EIdResponseType.Reset)
            {
                // Server wants us to forget reservations
                _availableIds.Clear();
                RequestChunk(CHUNK_SIZE);
            }
        }
    }

    public void Reset(long startId)
    {
        // Send reset request to server
        _requestWriter.Write(new IdRequest
        {
            ClientId = "", // Global request (empty ClientId)
            ReqNo = _requestCounter++,
            Type = EIdRequestType.Reset,
            Start = (ulong)startId,
            Count = 0
        });
        
        // Clear local pool (server will send Reset response)
        _availableIds.Clear();
    }
}
```

### Update to Task EXT-2-3

**Additional Files Needed:**
1. `ModuleHost.Network.Cyclone/Topics/IdAllocTopics.cs`:
   - `IdRequest` struct
   - `IdResponse` struct  
   - `IdStatus` struct
   - Enums: `EIdRequestType`, `EIdResponseType`

2. Update test expectations:
   - Test: `AllocateId_WithMockServer_ReturnsSequentialIds`
     - Mock server must send `IdResponse` with `Start` and `Count`
   - Test: `Reset_ClearsPoolAndRequestsNew`
     - Verify global reset request sent (empty ClientId)

---

## 3. Critical Implementation Warnings

### Warning 1: INetworkTopology Namespace Shift

**The Move:**
- FROM: `ModuleHost.Core/Network/Interfaces/INetworkTopology.cs`
- TO: `ModuleHost.Network.Cyclone/Abstractions/INetworkTopology.cs`

**Watch Out For (Task EXT-2-4):**

When moving `NetworkGatewayModule` to the Cyclone plugin, you MUST update its `using` statements:

```csharp
// ModuleHost.Network.Cyclone/Modules/NetworkGatewayModule.cs

// BEFORE (will break):
using ModuleHost.Core.Network.Interfaces; // ❌ INetworkTopology moved!

// AFTER (correct):
using ModuleHost.Network.Cyclone.Abstractions; // ✅ New location

public class NetworkGatewayModule : IModule
{
    private readonly INetworkTopology _topology; // Now from plugin namespace
    // ...
}
```

**Why This Matters:**
- If you forget this, you'll get: `Error CS0246: The type or namespace name 'INetworkTopology' could not be found`
- The compiler error will be confusing because the interface DID exist in Core before
- **Mitigation**: In Task EXT-2-4, explicitly check that NetworkGatewayModule uses the new namespace

---

### Warning 2: TypeId Determinism (Save Games)

**Current Implementation (Task EXT-2-6):**
```csharp
public int GetCoreTypeId(ulong disType)
{
    if (!_disToCore.TryGetValue(disType, out int id))
    {
        id = _disToCore.Count + 1; // ⚠️ Session-specific!
        _disToCore[disType] = id;
        _coreToDis[id] = disType;
    }
    return id;
}
```

**Problem:**
- TypeId assignment depends on **packet arrival order**
- Different sessions may assign different IDs to the same DIS type

**Example:**
| Session | Packet Order | Tank ID | Jeep ID |
|---------|-------------|---------|---------|
| Live    | Tank, Jeep  | 1       | 2       |
| Replay  | Jeep, Tank  | 2       | 1       |

**Impact Analysis:**

| Feature | Works? | Notes |
|---------|--------|-------|
| **Live Multiplayer** | ✅ Yes | Single session, IDs never reset |
| **Save Games** | ❌ No | Saved `TypeId=1` may load as wrong type |
| **Replay (with Reset)** | ❌ No | Live IDs don't match replay IDs |
| **Hot Reload** | ❌ No | Application restart changes mappings |

**Recommendation for Phase 2:**
- ✅ **Ship it as-is** for initial extraction
- ✅ Add prominent TODO comment in `TypeIdMapper.cs`:
  ```csharp
  // TODO: For Save Game / Replay support, use deterministic mapping
  // Option 1: Hash DISEntityType to stable int
  // Option 2: Static registration table
  ```
- 📝 Document in README that save games are not supported in v1.0

**Future Fix (Phase 8 - Post-Extraction):**
```csharp
public int GetCoreTypeId(ulong disType)
{
    // Deterministic hash (collision-resistant for 32-bit space)
    int hash = (int)((disType ^ (disType >> 32)) & 0x7FFFFFFF);
    return Math.Max(1, hash); // Ensure positive, non-zero
}
```

---

## 4. Updated Task Details

### Task EXT-1-3: Define Core Interfaces

**Remove from Core:**
- ~~`INetworkTopology`~~ → Moved to Cyclone plugin

**Rationale Updated:**
- Only `INetworkIdAllocator` stays in Core
- Topology is a Cyclone-specific concept (not all network layers need it)

---

### Task EXT-2-3: Implement DdsIdAllocator

**Additional Implementation Details:**
- Create IDL topic structs (IdRequest, IdResponse, IdStatus)
- Implement chunked allocation protocol (100 IDs per request)
- Handle `EIdResponseType.Reset` gracefully
- Low-water mark: Request new chunk when < 10 IDs remain

**Updated Test Requirements:**
- Mock DDS server that sends proper IdResponse messages
- Test reset behavior (clear pool, send global request)
- Test chunk management (auto-request when low)

---

### Task EXT-2-6: Create TypeIdMapper

**Add Implementation Warning:**
```csharp
/// <summary>
/// Translates DDS DISEntityType (ulong) to Core TypeId (int).
/// 
/// ⚠️ WARNING: Current implementation is session-specific.
/// TypeId assignments are not deterministic across session restarts.
/// 
/// This works for:
///   ✅ Live multiplayer (single continuous session)
/// 
/// This does NOT work for:
///   ❌ Save games (TypeId may change on load)
///   ❌ Replay sessions (IDs won't match live session)
///   ❌ Hot reload (application restart resets mappings)
/// 
/// TODO (Post-V1): Implement deterministic mapping for save/replay support.
/// </summary>
public class TypeIdMapper
{
    // Current: Dynamic allocation (order-dependent)
    public int GetCoreTypeId(ulong disType) { /* ... */ }
}
```

---

## 5. Appendix: FastCycloneDDS Integration Notes

### Participant Setup (Application Bootstrap)
```csharp
// Enable sender tracking BEFORE creating writers
var participant = new DdsParticipant(domainId: 0);
participant.EnableSenderTracking(new SenderIdentityConfig
{
    AppDomainId = 1,      // Cluster ID
    AppInstanceId = 100   // Node ID within cluster
});

// These IDs become NetworkAppId in EntityMaster topic
// NodeIdMapper converts them to simple int for Core
```

### Zero-Copy Reading Pattern
```csharp
// Correct: Scope pattern (zero-copy)
using var scope = reader.Take();
foreach (var sample in scope)
{
    ProcessSample(sample); // 'sample' is ref struct, no heap alloc
}

// Incorrect: Materializing outside scope
var list = new List<Sample>();
using var scope = reader.Take();
foreach (var sample in scope) 
{
    list.Add(sample); // ❌ Can't do this - ref struct can't escape
}
```

### Filter Performance
```csharp
// Filters execute on raw buffer views (zero-copy)
reader.SetFilter(view => 
    view.OwnerId.AppDomainId == 1 && // Direct field access
    view.DisTypeValue > 5000UL
);
```

---

## 6. Quality Gate Updates

### Gate 2 (After Phase 5) - Add Check:
- [ ] **TypeIdMapper has prominent warning comment**
- [ ] README documents that save games are not supported in v1

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.1 | 2026-01-30 | Added ID allocator protocol, TypeIdMapper warnings, INetworkTopology namespace note, FastCycloneDDS integration insights |
| 1.0 | 2026-01-30 | Initial design with critical flaw fixes |

