# FDP Engine Refactoring - Design Document

**Version:** 1.0  
**Date:** 2026-02-04  
**Status:** Draft

---

## Executive Summary

This document outlines the comprehensive refactoring of the FDP (Fast Data Plane) engine from a monolithic ModuleHost-centric architecture to a layered, modular system with reusable toolkit frameworks. The refactoring extracts network-specific, lifecycle, time synchronization, and TKB logic from the core orchestrator into dedicated, testable, and reusable assemblies.

### Goals

1. **Separation of Concerns**: Extract network-oriented, lifecycle, and distributed logic from `ModuleHost.Core` into dedicated toolkit assemblies
2. **Reusability**: Create framework-level toolkits (`FDP.Toolkit.*`) that can be used across multiple applications
3. **Flexibility**: Enable customization through interfaces and generic programming while providing sensible defaults
4. **Performance**: Maintain or improve performance through zero-allocation patterns and efficient memory management
5. **Testability**: Ensure all extracted components have comprehensive unit test coverage

### Non-Goals (Future Work)

- Renaming `Fdp.Kernel` to `FDP.ECS` (deferred to future phase)
- Renaming `ModuleHost.Core` to `FDP.ModuleHost` (deferred to future phase)
- Moving existing projects to new locations (projects stay where they are; only NEW projects are created)

---

## Architecture Overview

### Current State

The current architecture has several issues:
- `ModuleHost.Core` contains network-specific concepts (`NetworkOwnership`, `IDescriptorTranslator`)
- Entity Lifecycle Management (ELM) is tightly coupled to ModuleHost
- Time synchronization logic is embedded in Core
- TKB (Transient Knowledge Base) is minimal and lacks features needed for distributed systems
- Network replication logic is split between Core and the Cyclone plugin without clear boundaries

### Target Architecture

The new architecture follows a strict layering model:

```
┌─────────────────────────────────────────────────────────┐
│  Layer 5: Applications                                   │
│  FDP.Examples.NetworkDemo, Fdp.Examples.BattleRoyale    │
└─────────────────────────────────────────────────────────┘
                         ▲
┌─────────────────────────────────────────────────────────┐
│  Layer 4: Plugins (Concrete Drivers)                    │
│  FDP.Plugins.Network.Cyclone                            │
└─────────────────────────────────────────────────────────┘
                         ▲
┌─────────────────────────────────────────────────────────┐
│  Layer 3: Toolkits (Reusable Patterns)                  │
│  ├─ FDP.Toolkit.Lifecycle                               │
│  ├─ FDP.Toolkit.Time                                    │
│  ├─ FDP.Toolkit.Replication                             │
│  └─ FDP.Toolkit.Tkb                                     │
└─────────────────────────────────────────────────────────┘
                         ▲
┌─────────────────────────────────────────────────────────┐
│  Layer 2: Execution (Generic Orchestration)             │
│  ModuleHost.Core (stays as-is, cleaned up)              │
└─────────────────────────────────────────────────────────┘
                         ▲
┌─────────────────────────────────────────────────────────┐
│  Layer 1: Storage (ECS Primitives)                      │
│  Fdp.Kernel (stays as-is, minimal changes)              │
└─────────────────────────────────────────────────────────┘
                         ▲
┌─────────────────────────────────────────────────────────┐
│  Layer 0: Bridge (Shared Interfaces)                    │
│  FDP.Interfaces                                         │
└─────────────────────────────────────────────────────────┘
```

---

## Detailed Component Design

### 1. FDP.Interfaces (Layer 0)

**Purpose**: Minimal contract assembly to break circular dependencies and allow toolkits to reference each other's abstractions.

**Location**: `ModuleHost/FDP.Interfaces/`

**Contents**:
- `ITkbDatabase` - Interface for blueprint storage
- `INetworkTopology` - Interface for peer discovery
- `INetworkMaster` - Interface for entity master descriptors
- `IDescriptorTranslator` - Interface for DDS ↔ ECS translation
- `ISerializationProvider` - Interface for binary serialization
- `IDataReader` / `IDataWriter` / `IDataSample` - Network transport abstractions

**Dependencies**: `Fdp.Kernel` only (for `Entity` type and `IEntityCommandBuffer`)

**Design Principles**:
- No implementation code
- Minimal surface area
- Stable contracts that rarely change
- **Critical**: Must contain transport interfaces (`IDataReader`/`IDataWriter`) to prevent circular dependencies when `IDescriptorTranslator` references them

---

### 2. FDP.Toolkit.Lifecycle (Layer 3)

**Purpose**: Implements the Entity Lifecycle Management (ELM) pattern - "Dark Construction" with cooperative module initialization.

**Location**: `ModuleHost/FDP.Toolkit.Lifecycle/`

**Key Features**:
1. **Dynamic Participation**: Modules can register/unregister from lifecycle coordination
2. **Blueprint Integration**: Uses `Direct Injection` pattern - components already on entity act as parameters
3. **Preservation Rule**: Local modules respect injected components during construction
4. **Timeout Handling**: Configurable timeout for stuck construction/destruction

**Core Components**:
- `EntityLifecycleModule` - Main coordination module
- `BlueprintApplicationSystem` - Applies TKB templates with preservation logic
- `LifecycleCleanupSystem` - Removes transient construction components on activation

**Events**:
```csharp
[EventId(9001)] ConstructionOrder { Entity, BlueprintId, FrameNumber }
[EventId(9002)] ConstructionAck { Entity, ModuleId, Success, ErrorMessage }
[EventId(9003)] DestructionOrder { Entity, FrameNumber, Reason }
[EventId(9004)] DestructionAck { Entity, ModuleId }
```

**Customization Points**:
- `RegisterRequirement(blueprintId, moduleId)` - Define which modules must ACK for which blueprints
- `TimeoutFrames` - Configurable timeout before abandoning stuck entities

**Extracted From**:
- `ModuleHost.Core/ELM/EntityLifecycleModule.cs`
- `ModuleHost.Core/ELM/LifecycleEvents.cs`
- `ModuleHost.Core/ELM/LifecycleSystem.cs`

**Tests to Move**:
- `ModuleHost.Core.Tests/EntityLifecycleModuleTests.cs`
- `ModuleHost.Core.Tests/EntityLifecycleIntegrationTests.cs`
- `ModuleHost.Core.Tests/LifecycleEventsTests.cs`

---

### 3. FDP.Toolkit.Time (Layer 3)

**Purpose**: Implements distributed time synchronization using PLL (Phase-Locked Loop) for smooth catchup and deterministic lockstep modes.

**Location**: `ModuleHost/FDP.Toolkit.Time/`

**Key Features**:
1. **Continuous Mode**: PLL-based smooth synchronization (no rubber-banding)
2. **Deterministic Mode**: Frame-perfect lockstep for replays
3. **Future Barrier**: Distributed pause/resume coordination
4. **Mode Switching**: Runtime transition between continuous and stepped modes

**Core Components**:
- `MasterTimeController` - Authoritative time source
- `SlaveTimeController` - PLL-based follower
- `SteppedMasterController` - Deterministic frame stepping
- `SteppedSlaveController` - Lockstep follower
- `DistributedTimeCoordinator` - Future barrier logic
- `JitterFilter` - Network spike smoothing

**Messages**:
```csharp
[EventId(8001)] TimePulse { MasterFrame, MasterTime, TimeScale }
[EventId(8002)] FrameOrder { TargetFrame }
[EventId(8003)] FrameAck { NodeId, AcknowledgedFrame }
[EventId(8004)] SwitchTimeModeEvent { NewMode, TransitionFrame }
```

**Interface (stays in ModuleHost.Core)**:
```csharp
public interface ITimeController : IDisposable
{
    GlobalTime Update();
    void SetTimeScale(float scale);
    GlobalTime GetCurrentState();
    void SeedState(GlobalTime state);
}
```

**Extracted From**:
- `ModuleHost.Core/Time/MasterTimeController.cs`
- `ModuleHost.Core/Time/SlaveTimeController.cs`
- `ModuleHost.Core/Time/SteppedMasterController.cs`
- `ModuleHost.Core/Time/SteppedSlaveController.cs`
- `ModuleHost.Core/Time/DistributedTimeCoordinator.cs`
- `ModuleHost.Core/Time/SlaveTimeModeListener.cs`
- `ModuleHost.Core/Time/TimeDescriptors.cs`

**Tests to Move**:
- `ModuleHost.Core.Tests/Time/*` (entire folder - 10 test files)

---

### 4. FDP.Toolkit.Tkb (Layer 3)

**Purpose**: Comprehensive blueprint/template system with mandatory descriptor tracking for ghost promotion.

**Location**: `ModuleHost/FDP.Toolkit.Tkb/`

**Key Enhancements** (vs current minimal TKB in Fdp.Kernel):
1. **TkbType Support**: Use `long` TkbType as primary key (not string names)
2. **Mandatory Descriptors**: Define hard/soft requirements per type
3. **Packaging**: A descriptor key is a composite of `(DescriptorOrdinal, InstanceId)`
4. **Preservation Logic**: Built-in support for `preserveExisting` flag

**Core Types**:
```csharp
public struct MandatoryDescriptor
{
    public long PackedKey;     // (Ordinal << 32) | InstanceId
    public bool IsHard;        // Hard = wait forever, Soft = timeout allowed
    public uint SoftTimeout;   // Frames to wait for soft requirements
}

public class TkbTemplate
{
    public long TkbType { get; }
    public string Name { get; }
    public List<MandatoryDescriptor> MandatoryDescriptors { get; }
    
    public void AddComponent<T>(T component) where T : unmanaged;
    public void AddManagedComponent<T>(Func<T> factory) where T : class;
    public void ApplyTo(EntityRepository repo, Entity entity, bool preserveExisting);
}

public class TkbDatabase : ITkbDatabase
{
    public void Register(TkbTemplate template);
    public TkbTemplate GetByType(long tkbType);
    public TkbTemplate GetByName(string name);
}
```

**Utilities**:
```csharp
public static class PackedKey
{
    public static long Create(int ordinal, int instanceId);
    public static int GetOrdinal(long key);
    public static int GetInstance(long key);
}
```

**Partially Extracted From**:
- `Fdp.Kernel/Tkb/TkbDatabase.cs` (base structure exists)
- `Fdp.Kernel/Tkb/TkbTemplate.cs` (base structure exists)

**New Features** (need implementation):
- Mandatory descriptor tracking
- PackedKey utilities
- TkbType-based lookup
- Hard/soft requirements

**Tests to Move/Create**:
- Move: `Fdp.Tests/TkbTests.cs`
- Create: Tests for mandatory descriptors, packed keys, preservation logic

---

### 5. FDP.Toolkit.Replication (Layer 3)

**Purpose**: Implements the SST (Single Source of Truth) protocol for distributed entity replication with partial ownership, ghost handling, and ID allocation.

**Location**: `ModuleHost/FDP.Toolkit.Replication/`

This is the largest and most complex extraction.

#### 5.1 Core Components

**Components (ECS)**:
```csharp
public struct NetworkIdentity { public long Value; }

public struct NetworkAuthority
{
    public int PrimaryOwnerId;  // Owner of EntityMaster
    public int LocalNodeId;     // This node's ID
}

public class DescriptorOwnership  // Managed component
{
    public Dictionary<long, int> Map;  // PackedKey -> OwnerId (partial ownership)
}
```

**Ghost Management**:
```csharp
public class BinaryGhostStore  // Managed, transient component
{
    public Dictionary<long, GhostEntry> Stashed;
    public uint FirstSeenFrame;
}

public struct GhostEntry
{
    public int Offset;    // Into shared NativeArena pool
    public int Length;
}

public struct NetworkSpawnRequest  // Tag component
{
    public long TkbType;  // Signals: Master arrived, waiting for mandatory set
}
```

#### 5.2 Ghost Protocol

**Philosophy**: "Blind Accumulation" - start stashing data from the moment ANY descriptor arrives, even without knowing the entity type.

**Workflow**:
1. **Blind Ghost**: Unknown type, accumulating descriptors in binary form
2. **Identified Ghost**: Master arrived, now know `TkbType`, checking mandatory requirements
3. **Promotion**: All mandatory descriptors present → apply blueprint → inject stashed data → transition to `Constructing`

**Systems**:
- `GhostCreationSystem` - Creates ghosts when descriptors arrive for unknown entities
- `GhostPromotionSystem` - Checks mandatory requirements and promotes ready ghosts
  - Time-budgeted promotion to prevent frame spikes
  - Hard/soft requirement checking
  - Automatic cleanup of stale ghosts
- `GhostTimeoutSystem` - Destroys ghosts that never receive their Master

#### 5.3 Ownership Management

**Implicit Ownership Rule**: Authority is determined by who is currently writing a descriptor (DDS writer metadata), NOT by a field in the data.

**Transfer Protocol** (4-step handshake):
1. **Request**: Any node publishes `OwnershipUpdate` message
2. **Relinquish**: Current owner stops publishing immediately
3. **Takeover**: New owner updates local map
4. **Confirmation**: New owner publishes descriptor to confirm

**Crash Recovery**:
- Master node monitors disposal events
- If partial owner crashes, descriptor ownership returns to Primary Owner (fallback logic)

**Systems**:
- `OwnershipIngressSystem` - Processes `OwnershipUpdate` messages
- `DisposalMonitoringSystem` - Handles crash recovery
- `OwnershipEgressSystem` - Forces confirmation writes

**Messages**:
```csharp
[EventId(9010)] OwnershipUpdate
{
    long EntityId;
    long PackedDescriptorKey;
    int NewOwnerId;
}

[EventId(9011)] DescriptorAuthorityChanged  // Local event for systems
{
    Entity Entity;
    long PackedDescriptorKey;
    bool IsNowOwner;
    int NewOwnerId;
}
```

#### 5.4 ID Allocation

**Pattern**: Block-based allocation to minimize network round-trips.

**Components**:
```csharp
public class BlockIdManager : INetworkIdAllocator
{
    private Queue<long> _localPool;
    private int _lowWaterMark;
    
    public long AllocateId();  // Zero latency, from local pool
}
```

**Messages**:
```csharp
[EventId(9020)] IdBlockRequest { string ClientId, int RequestSize }
[EventId(9021)] IdBlockResponse { string ClientId, long StartId, int Count }
```

**Systems**:
- `IdAllocationMonitorSystem` - Watches pool level, requests refills
- `IdBlockResponseSystem` - Processes server responses

#### 5.5 Replication Optimization

**Binary Stashing**:
- Shared `NativeMemoryPool` for ghost data (avoid boxing/GC pressure)
- Per-descriptor serialization delegates (registered by plugin)
```csharp
public interface ISerializationProvider
{
    int GetSize(object descriptor);
    void Encode(object descriptor, Span<byte> buffer);
    void Apply(Entity entity, ReadOnlySpan<byte> buffer, EntityRepository repo);
}
```

**Smart Egress** (future enhancement):
- Dirty tracking to send only changed descriptors
- Salted rolling windows for unreliable descriptors
- Rate limiting per descriptor type

#### 5.6 Sub-Entity Parts (Multi-Instance Descriptors)

When a descriptor has `InstanceId > 0` (e.g., Turret1, Turret2):
- Create hidden sub-entities in ECS
- Link via parent-child relationship
- Network sees one entity with multiple parts
- ECS sees multiple entities (parent + parts)

**Components**:
```csharp
public struct PartMetadata
{
    public int InstanceId;
    public Entity ParentEntity;
}

public class ChildMap  // On parent
{
    public Dictionary<int, Entity> InstanceToEntity;
}
```

#### 5.7 Generic Master Pattern

**Interface Approach**:
```csharp
public interface INetworkMaster
{
    long EntityId { get; }
    long TkbType { get; }
    // NO OwnerId field - implicit from DDS writer metadata!
}

public class GenericNetworkSpawner<TMaster> : IModuleSystem 
    where TMaster : struct, INetworkMaster
{
    public void Execute(ISimulationView view, float dt)
    {
        // Process incoming TMaster packets
        // Create/identify ghosts
        // Update NetworkAuthority.PrimaryOwnerId from DDS metadata
    }
}
```

**Binary Stashing Interface** (corrected for thread-safety):
```csharp
public interface ISerializationProvider
{
    int GetSize(object descriptor);
    void Encode(object descriptor, Span<byte> buffer);
    
    // CRITICAL: Uses IEntityCommandBuffer for thread-safe mutations
    // NOT EntityRepository directly!
    void Apply(Entity entity, ReadOnlySpan<byte> buffer, IEntityCommandBuffer cmd);
}
```

**Network Entity Map**:
```csharp
public class NetworkEntityMap
{
    private Dictionary<long, Entity> _networkToEntity;
    private Dictionary<long, uint> _graveyard;  // Recently destroyed IDs
    
    public bool TryGet(long networkId, out Entity entity);
    public void Register(long networkId, Entity entity);
    public void Unregister(long networkId);
    public bool IsInGraveyard(long networkId, uint currentFrame);
}
```

#### Extracted From:
- `ModuleHost.Core/Network/` (most files)
- `ModuleHost.Network.Cyclone/Components/` (NetworkIdentity, NetworkPosition, etc.)
- `ModuleHost.Network.Cyclone/Services/` (NetworkEntityMap, TypeIdMapper)
- `ModuleHost.Network.Cyclone/Modules/NetworkGatewayModule.cs`
- `ModuleHost.Network.Cyclone/Translators/` (generic pattern, not specific implementations)

#### Tests to Move/Create:
- Move: `ModuleHost.Core.Tests/Network/` folder tests
- Move: `ModuleHost.Network.Cyclone.Tests/` relevant tests
- Create: Ghost promotion tests, ownership transfer tests, ID allocation tests, binary stashing tests

---

### 6. FDP.Plugins.Network.Cyclone (Layer 4)

**Purpose**: DDS-specific transport implementation using CycloneDDS.

**Location**: `ModuleHost/FDP.Plugins.Network.Cyclone/` (already exists, will be refactored)

**Slimmed Down Contents**:
- `CycloneDataReader` / `CycloneDataWriter` - DDS wrappers implementing toolkit interfaces
- `DdsIdAllocatorServer` - Centralized ID server (keep as-is, it's plugin-specific)
- `NodeIdMapper` - Maps DDS participant GUIDs to integer node IDs
- `CycloneSerializationProvider` - Uses CycloneDDS native serialization for binary stashing

**What Moves OUT**:
- Generic networking concepts → `FDP.Toolkit.Replication`
- `NetworkIdentity`, `NetworkPosition` components → `FDP.Toolkit.Replication`
- `IDescriptorTranslator` interface → `FDP.Interfaces`

**Dependencies**:
- `FDP.Toolkit.Replication`
- `FDP.Toolkit.Lifecycle`
- CycloneDDS bindings (existing)

---

### 7. Fdp.Examples.NetworkDemo Refactoring

**Purpose**: Demonstrate "zero boilerplate" usage of the new toolkit architecture.

**New Folder Structure**:
```
Fdp.Examples.NetworkDemo/
├── Descriptors/              # DDS Topic Structs
│   ├── DemoMasterDescriptor.cs
│   ├── PhysicsDescriptor.cs
│   └── TurretDescriptor.cs
├── Systems/                  # Application Logic
│   ├── SimplePhysicsSystem.cs
│   └── PlayerInputSystem.cs
├── Configuration/            # TKB & Topology Setup
│   ├── TkbSetup.cs
│   └── DemoTopology.cs
└── Program.cs                # Bootstrap
```

**Key Changes**:
1. **No Manual Translators**: Use attribute-based auto-discovery
2. **Unified Components**: Network descriptors ARE ECS components (direct mapping)
3. **Declarative TKB**: Define mandatory requirements in TkbSetup
4. **Authority-Aware Systems**: Use `view.HasAuthority(entity)` extension method

**Example Descriptor** (new pattern):
```csharp
[DdsTopic("Demo_PhysicsState")]
[FdpDescriptor(ordinal: 1, isMandatory: true)]  // Toolkit attribute
[FdpUnreliable]  // Enables rolling-window refresh
public partial struct PhysicsDescriptor
{
    [DdsKey] public long EntityId;
    public Vector3 Position;
    public Vector3 Velocity;
}
```

**Bootstrap** (Program.cs):
```csharp
// Layers are explicit and minimal
var repo = new EntityRepository();
var host = new ModuleHostKernel(repo);

var tkb = new TkbDatabase();
TkbSetup.Initialize(tkb);

var lifecycle = new EntityLifecycleModule(tkb);
var replication = new ReplicationToolkit(tkb, new DemoTopology());

host.RegisterModule(lifecycle);
host.RegisterModule(replication);

// Auto-discovery of descriptors
replication.Bootstrap(typeof(DemoMasterDescriptor).Assembly);

var cyclone = new CyclonePlugin(replication);
cyclone.Connect();
```

---

## Implementation Phases

### Phase 0: Foundation & Interfaces
**Duration**: ~1 week  
**Goal**: Create base infrastructure without breaking existing code

- Create `FDP.Interfaces` project
- Define all interface contracts
- Create `FDP.Toolkit.Tkb` project structure
- Enhance TKB with TkbType support, mandatory descriptor tracking

**Deliverables**:
- ✅ All interface definitions compile
- ✅ TKB can track mandatory descriptors
- ✅ PackedKey utilities work correctly
- ✅ Existing code still compiles and runs

### Phase 1: Lifecycle Extraction
**Duration**: ~1.5 weeks  
**Goal**: Extract ELM to standalone toolkit

- Create `FDP.Toolkit.Lifecycle` project
- Move ELM code from ModuleHost.Core
- Integrate with TKB for blueprint application
- Implement "Direct Injection" preservation logic
- Move and update tests

**Deliverables**:
- ✅ Lifecycle toolkit compiles independently
- ✅ All moved tests pass
- ✅ New preservation logic tests pass
- ✅ ModuleHost.Core no longer has ELM folder
- ✅ Example integration (simple test app)

### Phase 2: Time Extraction
**Duration**: ~1 week  
**Goal**: Extract time synchronization to standalone toolkit

- Create `FDP.Toolkit.Time` project
- Move time controllers from ModuleHost.Core
- Keep `ITimeController` interface in ModuleHost.Core
- Move and update tests

**Deliverables**:
- ✅ Time toolkit compiles independently
- ✅ All moved tests pass
- ✅ ModuleHost.Core no longer has Time folder (except interface)
- ✅ PLL and lockstep modes work correctly

### Phase 3: Replication - Core Infrastructure
**Duration**: ~2 weeks  
**Goal**: Create replication toolkit foundation

- Create `FDP.Toolkit.Replication` project
- Implement components (`NetworkIdentity`, `NetworkAuthority`, `DescriptorOwnership`)
- Implement `NetworkEntityMap` with graveyard
- Implement `BlockIdManager`
- Create basic tests

**Deliverables**:
- ✅ Replication toolkit project structure
- ✅ Core components defined
- ✅ ID allocation works with block pattern
- ✅ Network entity mapping works correctly

### Phase 4: Replication - Ghost Protocol
**Duration**: ~2 weeks  
**Goal**: Implement zero-allocation ghost handling

- Implement `BinaryGhostStore` with shared memory pool
- Implement `GhostCreationSystem`
- Implement `GhostPromotionSystem` with time budgeting
- Integrate with TKB mandatory requirements
- Create comprehensive ghost tests

**Deliverables**:
- ✅ Binary stashing works without GC pressure
- ✅ Ghost promotion handles hard/soft requirements
- ✅ Timeout and graveyard logic prevents leaks
- ✅ Time-budgeted promotion prevents frame spikes
- ✅ All ghost protocol tests pass

### Phase 5: Replication - Ownership Management
**Duration**: ~1.5 weeks  
**Goal**: Implement ownership transfer and crash recovery

- Implement `OwnershipIngressSystem`
- Implement `DisposalMonitoringSystem`
- Implement 4-step handshake protocol
- Create ownership transfer tests
- Create crash recovery tests

**Deliverables**:
- ✅ Ownership transfers complete successfully
- ✅ Crash recovery returns ownership to primary
- ✅ `ForcePublish` confirmation works
- ✅ All ownership tests pass

### Phase 6: Replication - Sub-Entity Parts
**Duration**: ~1 week  
**Goal**: Handle multi-instance descriptors as sub-entities

- Implement `PartMetadata` and `ChildMap` components
- Implement part spawning logic
- Create parent-child linking system
- Test with multi-turret scenarios

**Deliverables**:
- ✅ Multi-instance descriptors create sub-entities
- ✅ Parent-child relationships maintained
- ✅ Parts can have independent ownership
- ✅ Part tests pass

### Phase 7: Plugin Refactoring
**Duration**: ~1 week  
**Goal**: Slim down Cyclone plugin to pure transport

- Move generic networking out of plugin
- Implement `CycloneSerializationProvider`
- Update plugin to use toolkit interfaces
- Move relevant tests

**Deliverables**:
- ✅ Plugin has no generic networking logic
- ✅ Plugin implements toolkit interfaces
- ✅ DDS-specific code remains in plugin
- ✅ All plugin tests pass

### Phase 8: NetworkDemo Refactoring
**Duration**: ~1.5 weeks  
**Goal**: Demonstrate new architecture with real application

- Refactor NetworkDemo folder structure
- Create descriptor definitions with attributes
- Implement TkbSetup configuration
- Create authority-aware systems
- Implement auto-discovery bootstrap

**Deliverables**:
- ✅ NetworkDemo uses new toolkit architecture
- ✅ No manual translators in demo
- ✅ Demo runs with 2+ nodes successfully
- ✅ Ghost handling works in practice
- ✅ Ownership transfers work in practice

### Phase 9: Integration & Documentation
**Duration**: ~1 week  
**Goal**: Ensure everything works together

- Integration testing across all toolkits
- Performance benchmarking
- Update architecture documentation
- Create migration guide for other examples

**Deliverables**:
- ✅ All tests pass
- ✅ No performance regressions
- ✅ Documentation complete
- ✅ Migration guide available

---

## Success Criteria

### Functional
- ✅ All existing tests continue to pass
- ✅ New tests for extracted functionality pass
- ✅ NetworkDemo runs successfully with new architecture
- ✅ Ghost promotion handles out-of-order packet arrival
- ✅ Ownership transfers complete successfully
- ✅ Time synchronization maintains smooth simulation
- ✅ ID allocation prevents collisions

### Performance
- ✅ No GC allocations in ghost stashing (binary approach)
- ✅ Promotion time budget prevents frame spikes
- ✅ No performance regression vs current implementation
- ✅ Memory usage stable during long-running tests

### Code Quality
- ✅ Clear separation of concerns (layering respected)
- ✅ No circular dependencies
- ✅ All public APIs documented
- ✅ Test coverage ≥ 80% for new code
- ✅ All warnings treated as errors (per user rules)

### Reusability
- ✅ Toolkits can be used independently
- ✅ New applications can integrate toolkits with minimal code
- ✅ Plugin interface allows alternative network transports
- ✅ TKB can be configured per-application

---

## Risk Mitigation

### Risk: Breaking Changes During Extraction
**Mitigation**: 
- Phase 0 establishes contracts before moving code
- Each phase includes integration tests
- Existing tests continue to run until code is fully migrated

### Risk: Performance Regression in Ghost Handling
**Mitigation**:
- Binary stashing benchmarked early (Phase 4)
- Profiling before/after each major change
- Performance tests as part of success criteria

### Risk: Complex Ownership Logic Has Subtle Bugs
**Mitigation**:
- Comprehensive test scenarios (transfer, crash, multi-part)
- Integration tests with actual DDS plugin
- NetworkDemo serves as real-world test

### Risk: Over-Engineering / Too Many Abstractions
**Mitigation**:
- Each abstraction justified by reuse need
- Interfaces only where plugin-swapping is realistic
- Concrete implementations as default (avoid framework sprawl)

---

## Future Enhancements (Post-Refactoring)

1. **Rename Projects** to FDP.* convention (Phase 10+)
2. **Smart Egress Optimization**: Dirty tracking, rolling windows, rate limiting
3. **Alternative Plugins**: ENet, WebSockets transport layers
4. **Replay System Integration**: Enhanced recording/playback with new architecture
5. **Automatic Translator Generation**: Source generators for descriptor → component mapping
6. **Dynamic TKB Loading**: Runtime blueprint loading from files/databases

---

## Appendix A: Dependency Graph

```
FDP.Examples.NetworkDemo
  └─→ FDP.Plugins.Network.Cyclone
  └─→ FDP.Toolkit.Replication
       └─→ FDP.Toolkit.Tkb
       └─→ FDP.Toolkit.Lifecycle
       └─→ FDP.Interfaces

FDP.Plugins.Network.Cyclone
  └─→ FDP.Toolkit.Replication
  └─→ CycloneDDS.Runtime

FDP.Toolkit.Replication
  └─→ FDP.Toolkit.Tkb
  └─→ FDP.Toolkit.Lifecycle
  └─→ FDP.Toolkit.Time
  └─→ FDP.Interfaces
  └─→ ModuleHost.Core
  └─→ Fdp.Kernel

FDP.Toolkit.Lifecycle
  └─→ FDP.Interfaces
  └─→ ModuleHost.Core
  └─→ Fdp.Kernel

FDP.Toolkit.Time
  └─→ FDP.Interfaces
  └─→ ModuleHost.Core
  └─→ Fdp.Kernel

FDP.Toolkit.Tkb
  └─→ FDP.Interfaces
  └─→ Fdp.Kernel

ModuleHost.Core
  └─→ Fdp.Kernel

FDP.Interfaces
  (no dependencies - pure interfaces)
```

---

## Appendix B: Key Design Decisions

### 1. Why Keep `ITimeController` in ModuleHost.Core?
**Rationale**: The orchestrator (ModuleHostKernel) consumes the time controller. Keeping the interface where the consumer lives reduces dependencies and keeps Core functional with just a local clock implementation.

### 2. Why Remove `OwnerId` from Descriptors?
**Rationale**: Per SST rules, ownership is implicit (determined by DDS writer). Having an explicit field creates sync risk and maintenance burden. ECS `NetworkAuthority` component tracks ownership locally, and `FlightRecorder` saves it for replay.

### 3. Why Use Binary Stashing for Ghosts?
**Rationale**: Boxing descriptors to `object` causes GC pressure in high-traffic scenarios. Binary stashing leverages CycloneDDS's native serialization and uses a shared memory pool (zero allocation).

### 4. Why Create FDP.Interfaces Layer?
**Rationale**: Breaks circular dependencies (e.g., Lifecycle needs ITkbDatabase, Tkb needs IModule). Pure interface assembly is a standard pattern for plugin architectures.

### 5. Why Direct Injection vs ParameterEntity?
**Rationale**: Creating two entities doubles ID overhead and risks network replication of the parameter entity. Direct injection treats network ghosting and local spawning identically (data already on entity = override).

---

## Document Control

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-04 | AI Assistant | Initial draft based on design talk discussions |

