# FDP Distributed Logging and Testing Framework - Design Document

## 1. Executive Summary

This design introduces professional-grade infrastructure for the FDP Network Demo: **High-Performance Logging** and **Automated E2E Testing** for distributed systems. The implementation leverages NLog for mature logging capabilities while ensuring zero-allocation on hot paths, and establishes a comprehensive testing framework for verifying distributed simulation behavior.

### Key Capabilities

1. **High-Performance Logging**: Zero-allocation checks, async I/O, granular module-level filtering
2. **Context-Aware Logging**: Automatic node identification across async/threaded execution
3. **Distributed Test Framework**: Run multiple nodes in parallel with isolated logging
4. **Comprehensive Test Coverage**: Entity lifecycle, replication, ownership, replay validation
5. **Production-Ready**: Suitable for both debugging and production deployments

---

## 2. Architecture Overview

### 2.1 Logging Architecture

The logging system uses a three-layer architecture:

```
[APPLICATION LAYER]
  Systems/Modules → FdpLog<T>.Debug(...)
                         ↓
[FACADE LAYER]
              FdpLog<T> (Static Generic)
              - Zero-alloc checks
              - Type-based logger naming
                         ↓
[FRAMEWORK LAYER]
              NLog (Core)
              - AsyncLocal context flow
              - Async file I/O
              - Rule-based filtering
                         ↓
[OUTPUT LAYER]
              logs/node_100.log
              logs/node_200.log
```

**Key Design Principles:**
- **Check Before Format**: `if (FdpLog<T>.IsDebugEnabled)` guard prevents string allocation
- **Static Generic Facade**: `FdpLog<MyClass>` provides type-safe, cached logger instances
- **Async I/O**: File writes happen on background thread to avoid blocking simulation loop
- **Context Flow**: `AsyncLocal` automatically tracks NodeId across Tasks and awaits

### 2.2 Testing Architecture

```
[TEST LAYER]
  xUnit Test Class → DistributedTestEnv
                         ↓
[ORCHESTRATION LAYER]
              Task.Run (Node A) || Task.Run (Node B)
              Each with ScopeContext.PushProperty("NodeId", ...)
                         ↓
[APPLICATION LAYER]
              NetworkDemoApp instances
              - Shared ECS Kernel
              - Isolated Network stacks
                         ↓
[VERIFICATION LAYER]
              State Assertions + Log Capture
              - ECS component queries
              - In-memory log buffer
              - Timeout-based conditions
```

---

## 3. Component Design

### 3.1 FdpLog - Static Logging Facade

**Location:** `ModuleHost/FDP.Kernel/Logging/FdpLog.cs`

**Purpose:** Provide a high-performance, zero-allocation logging interface for hot paths.

**Features:**
- Generic type parameter `<T>` for automatic logger naming
- Boolean flags for fast level checks (`IsDebugEnabled`, `IsTraceEnabled`)
- Aggressive inlining for minimal overhead
- Multiple overloads to avoid `params object[]` allocations

**Usage Pattern:**
```csharp
// In performance-critical code
if (FdpLog<MySystem>.IsDebugEnabled)
{
    FdpLog<MySystem>.Debug($"Entity {entity.Index} at {position}");
}

// For non-critical paths
FdpLog<MyModule>.Info("System initialized");
FdpLog<MyModule>.Error("Critical failure", exception);
```

### 3.2 LogSetup - Configuration Module

**Location:** `Fdp.Examples.NetworkDemo/Configuration/LogSetup.cs`

**Purpose:** Centralized NLog configuration for different execution contexts (dev, test, production).

**Features:**
- Programmatic configuration (no XML files needed)
- Dynamic filename based on NodeId: `logs/node_${scopeproperty:NodeId}.log`
- Granular module-level filtering
- Async wrapper for background I/O
- Multiple configuration presets (verbose, production, test)

**Configuration Presets:**
```csharp
LogSetup.ConfigureForDevelopment(nodeId);  // Verbose, all modules
LogSetup.ConfigureForTesting(testName);     // Structured, in-memory capture
LogSetup.ConfigureForProduction(nodeId);    // Warn+ only, optimized
```

### 3.3 DistributedTestEnv - Test Orchestration

**Location:** `Fdp.Examples.NetworkDemo.Tests/Framework/DistributedTestEnv.cs`

**Purpose:** Manage lifecycle of multiple NetworkDemoApp instances for E2E testing.

**Features:**
- Run 2+ nodes concurrently in separate Tasks
- Automatic scope isolation via `ScopeContext.PushProperty`
- Helper methods for waiting on conditions
- Centralized cleanup and disposal
- Integration with xUnit test output

**Key Methods:**
```csharp
Task StartNodesAsync();
Task WaitForCondition(Func<NetworkDemoApp, bool> predicate, NetworkDemoApp target, int timeoutMs);
Task RunFrames(int count);
void AssertLogContains(int nodeId, string message);
```

### 3.4 TestLogCapture - In-Memory Log Target

**Location:** `Fdp.Examples.NetworkDemo.Tests/Framework/TestLogCapture.cs`

**Purpose:** Capture logs during tests for verification without file I/O.

**Features:**
- Implements NLog `TargetWithLayout`
- Thread-safe `ConcurrentQueue<string>`
- Query methods for test assertions
- No disk I/O overhead

---

## 4. Implementation Phases

### Phase 1: Logging Foundation
**Goal:** Establish high-performance logging infrastructure.

**Tasks:**
1. Add NLog NuGet packages to `FDP.Kernel` and `Fdp.Examples.NetworkDemo`
2. Create `FdpLog<T>` static facade in `FDP.Kernel/Logging/`
3. Create `LogSetup` configuration module
4. Add scope context setup to `NetworkDemoApp.Start()`
5. Replace `Console.WriteLine` with `FdpLog` in network modules

**Success Criteria:**
- Zero-allocation when logging is disabled (verified via BenchmarkDotNet)
- AsyncLocal context flows correctly across `Task.Run` and `await`
- Different nodes write to separate log files
- Granular filtering works (e.g., Trace for Network.*, Warn for Kernel.*)

### Phase 2: Test Infrastructure
**Goal:** Build distributed testing framework.

**Tasks:**
1. Create `Fdp.Examples.NetworkDemo.Tests` xUnit project
2. Implement `TestLogCapture` NLog target
3. Implement `DistributedTestEnv` orchestrator
4. Add helper methods for common assertions
5. Refactor `NetworkDemoApp` to support headless execution

**Success Criteria:**
- Two nodes can run concurrently in test context
- Logs are captured separately per node
- State assertions can query ECS components
- Tests can wait for async conditions with timeout

### Phase 3: Core Test Cases
**Goal:** Verify all distributed features work correctly.

**Tasks:**
1. Test: Basic replication (entity creation, movement)
2. Test: Partial ownership (split control)
3. Test: Dynamic ownership transfer
4. Test: Entity lifecycle (spawn, activate, destroy)
5. Test: Ghost protocol (orphan protection)
6. Test: Sub-entity hierarchy cleanup
7. Test: AsyncLocal scope verification
8. Test: Deterministic time switching
9. Test: Reactive systems (damage, events)
10. Test: Distributed replay

**Success Criteria:**
- All tests pass reliably
- Each test verifies both state and logs
- Tests fail when regressions are introduced
- Test execution time < 30s for full suite

---

## 5. Logging Configuration Details

### 5.1 Log Levels and Module Filtering

**Level Hierarchy:**
- `Trace`: Detailed execution flow (hot path, enabled only during debugging)
- `Debug`: Important state changes, entity operations
- `Info`: Lifecycle events, mode switches, network discovery
- `Warn`: Recoverable issues, missing data, timeouts
- `Error`: Critical failures, exceptions

**Module-Specific Rules:**
```csharp
// Global default: Info+
config.AddRule(LogLevel.Info, LogLevel.Fatal, asyncFile);

// Suppress noisy kernel internals
config.AddRule(LogLevel.Warn, LogLevel.Fatal, asyncFile, "Fdp.Kernel.EntityIndex");
config.AddRule(LogLevel.Warn, LogLevel.Fatal, asyncFile, "Fdp.Kernel.ComponentStore");

// Enable deep tracing for network debugging
config.AddRule(LogLevel.Trace, LogLevel.Fatal, asyncFile, "ModuleHost.Network.*");
config.AddRule(LogLevel.Trace, LogLevel.Fatal, asyncFile, "FDP.Toolkit.Replication.*");
```

### 5.2 Log Format

**File Output:**
```
2026-02-07 14:32:15.234|DEBUG|Node-100|CycloneNetworkModule|Registered translator: Tank_GeoState
2026-02-07 14:32:15.256|TRACE|Node-100|GenericDescriptorTranslator|Scanning entity 65536, HasAuth(5)=True
2026-02-07 14:32:15.278|INFO|Node-200|CycloneIngress|Created ghost for NetID 100_65536
```

**Format Components:**
- Timestamp (millisecond precision)
- Log level
- Node identifier (from AsyncLocal context)
- Logger name (short, type-based)
- Message + Exception (if present)

### 5.3 Performance Optimizations

**AsyncWrapper Configuration:**
```csharp
var asyncFile = new AsyncTargetWrapper(logFile)
{
    OverflowAction = AsyncTargetWrapperOverflowAction.Discard, // Don't block on overflow
    QueueLimit = 10000,          // Buffer size
    BatchSize = 100,             // Batch writes
    TimeToSleepBetweenBatches = 10  // Flush frequency (ms)
};
```

**Hot Path Pattern:**
```csharp
// BAD - Always allocates interpolated string
FdpLog<MySystem>.Debug($"Position: {pos.X}, {pos.Y}");

// GOOD - Zero allocation if disabled
if (FdpLog<MySystem>.IsDebugEnabled)
    FdpLog<MySystem>.Debug($"Position: {pos.X}, {pos.Y}");

// BEST - Use format strings for simple cases
FdpLog<MySystem>.Debug("Entity {0} moved", entity.Index);
```

---

## 6. Test Case Specifications

### 6.1 Infrastructure Validation Tests

**Test: Logging Scope Flow**
- **Purpose:** Verify AsyncLocal context flows across Tasks and async boundaries
- **Setup:** Start two Tasks with different NodeIds
- **Actions:** Call deep library methods from each task
- **Verification:** All logs from Task 1 have NodeId=100, all from Task 2 have NodeId=200
- **Logs Required:** Trace level from test library methods
- **Failure Modes:** Mixed/missing NodeIds indicate context loss

### 6.2 Replication Tests

**Test: Basic Entity Replication**
- **Purpose:** Verify entities spawn on remote nodes
- **Setup:** Node A creates entity, Node B is observer
- **Actions:** Spawn tank on Node A, wait for discovery
- **Verification:** Node B has matching entity with correct NetworkIdentity
- **Logs Required:** 
  - Node A: `[GeodeticTranslator] Published descriptor for entity X`
  - Node B: `[CycloneIngress] Received descriptor, created ghost X`
- **Success:** Entity exists on both nodes with matching NetId

**Test: Component Synchronization**
- **Purpose:** Verify component data replicates correctly
- **Setup:** Both nodes running, entity already replicated
- **Actions:** Node A updates DemoPosition component
- **Verification:** Node B's ghost entity has updated DemoPosition
- **Logs Required:**
  - Node A: `[SmartEgress] Published update for descriptor 5`
  - Node B: `[CycloneIngress] Updated component on ghost X`
- **Success:** Position delta < 0.1 units

### 6.3 Ownership Tests

**Test: Partial Ownership Isolation**
- **Purpose:** Verify split control doesn't cause overwrites
- **Setup:** Tank with Chassis owned by A, Turret owned by B
- **Actions:** 
  - Node A updates Position
  - Node B updates Turret.Yaw
  - Wait for cross-replication
- **Verification:**
  - Node A sees updated Yaw, keeps its Position
  - Node B sees updated Position, keeps its Yaw
- **Logs Required:**
  - Node A: `[SmartEgress] Skipped Turret (Not Owner)`
  - Node B: `[SmartEgress] Skipped Chassis (Not Owner)`
- **Success:** No data loss or overwrites

**Test: Dynamic Ownership Transfer**
- **Purpose:** Verify runtime authority transfer
- **Setup:** Node A owns all descriptors initially
- **Actions:** Node B requests Turret ownership
- **Verification:**
  - Authority flags update on both nodes
  - Egress switches from A to B for Turret
- **Logs Required:**
  - Node A: `[Ownership] Authority LOST for descriptor 6`
  - Node B: `[Ownership] Authority GAINED for descriptor 6`
- **Success:** Only B publishes Turret updates after transfer

### 6.4 Lifecycle Tests

**Test: Entity Creation Flow**
- **Purpose:** Verify ghost spawning and activation
- **Setup:** Node A creates entity, Node B observes
- **Actions:** Spawn tank on A
- **Verification:**
  1. Node B creates ghost (lifecycle = Constructing)
  2. After mandatory data arrives, ghost promotes to Active
- **Logs Required:**
  - Node B: `[GhostSpawner] Created ghost for NetID X (state: Constructing)`
  - Node B: `[GhostActivator] Promoted ghost to Active (all mandatory data present)`
- **Success:** Ghost activation happens only after all mandatory descriptors received

**Test: Entity Destruction Propagation**
- **Purpose:** Verify deletion replicates
- **Setup:** Both nodes have active entity
- **Actions:** Node A destroys entity
- **Verification:** Node B's ghost is destroyed
- **Logs Required:**
  - Node A: `[Lifecycle] Destroying entity X`
  - Node B: `[GhostCleanup] Received deletion for NetID X`
- **Success:** Entity gone from both nodes within 1 second

**Test: Orphan Protection (Missing Mandatory Data)**
- **Purpose:** Verify ghost stays inactive without required data
- **Setup:** Node A configured to not send Chassis descriptor
- **Actions:** Spawn tank on A (EntityMaster sent, Chassis blocked)
- **Verification:** Node B has ghost in Constructing state, never Active
- **Logs Required:**
  - Node B: `[GhostSpawner] Created ghost, waiting for mandatory descriptor 5`
  - Node B: (NO log saying "Promoted to Active")
- **Success:** Ghost remains in Constructing state indefinitely

### 6.5 Advanced Feature Tests

**Test: Sub-Entity Hierarchy Cleanup**
- **Purpose:** Verify child entities destroy with parent
- **Setup:** Tank with MachineGun sub-entity
- **Actions:** Destroy tank
- **Verification:** MachineGun also destroyed
- **Logs Required:**
  - `[SubEntityCleanup] Parent X destroyed, cleaning 1 children`
- **Success:** Child entity no longer exists

**Test: Deterministic Time Mode Switch**
- **Purpose:** Verify distributed time synchronization
- **Setup:** Both nodes in Continuous mode
- **Actions:** Node A requests Deterministic mode
- **Verification:**
  - Both nodes pause at same frame
  - Both switch to Stepped time controller
- **Logs Required:**
  - Both nodes: `[TimeControl] Switched to Deterministic at frame 120`
- **Success:** Frame numbers match exactly

**Test: Distributed Replay**
- **Purpose:** Verify replay triggers network egress
- **Setup:** 
  - Record session with Node A
  - Start Node A in replay mode
  - Start Node B in live mode
- **Actions:** Node A replays recording
- **Verification:** Node B receives data from replay
- **Logs Required:**
  - Node A: `[ReplayBridge] Injecting frame 60 from recording`
  - Node B: `[CycloneIngress] Received update for NetID X`
- **Success:** Node B's ghost moves according to replay data

---

## 7. Migration Strategy

### 7.1 Refactoring NetworkDemoApp

**Current Structure:**
```csharp
public async Task Start(int nodeId, ...)
{
    // Setup
    while (!cancel)
    {
        Update();
        await Task.Delay(...);
    }
}
```

**New Structure (Testable):**
```csharp
public async Task Start(int nodeId, ...)
{
    using (ScopeContext.PushProperty("NodeId", nodeId))
    {
        // Setup only
        await InitializeAsync();
    }
}

public void Update(float dt)
{
    // Single frame update
}

public async Task RunLoopAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        Update(0.016f);
        await Task.Delay(16);
    }
}
```

**Benefits:**
- Tests can call `Update()` directly for deterministic frame control
- Scope context wraps entire execution
- Easier to inject test data between frames

### 7.2 Console.WriteLine Replacement Plan

**Files to Update:**
1. `ModuleHost.Network.Cyclone/CycloneNetworkModule.cs`
2. `ModuleHost.Network.Cyclone/Systems/CycloneNetworkEgressSystem.cs`
3. `ModuleHost.Network.Cyclone/Systems/CycloneNetworkIngressSystem.cs`
4. `FDP.Toolkit.Replication/Translators/GenericDescriptorTranslator.cs`
5. `Fdp.Examples.NetworkDemo/NetworkDemoApp.cs`
6. `Fdp.Examples.NetworkDemo/Systems/*` (all system implementations)

**Pattern:**
```csharp
// Before
Console.WriteLine($"Registered {count} translators");

// After
FdpLog<CycloneNetworkModule>.Info($"Registered {count} translators");

// Hot path before
Console.WriteLine($"Entity {e.Index} updated to {pos}");

// Hot path after
if (FdpLog<TransformSyncSystem>.IsDebugEnabled)
    FdpLog<TransformSyncSystem>.Debug($"Entity {e.Index} updated to {pos}");
```

---

## 8. Success Metrics

### 8.1 Performance Requirements
- ✅ Zero allocation when logging disabled (BenchmarkDotNet validation)
- ✅ Log check overhead < 5ns (IsDebugEnabled flag read)
- ✅ No frame drops during heavy logging (async I/O)
- ✅ Log file writes < 10ms average (buffered writes)

### 8.2 Functionality Requirements
- ✅ Logs from different nodes separated into different files
- ✅ AsyncLocal context preserved across Task.Run and await
- ✅ Module-level filtering works (enable Trace for Network.*, Warn for Kernel.*)
- ✅ Test framework runs 2 nodes concurrently without interference
- ✅ All 10+ test cases pass reliably

### 8.3 Diagnostics Requirements
- ✅ "Remote: 0" bug identifiable from logs within 30 seconds
- ✅ Ownership issues show clear "Skipped (Not Owner)" messages
- ✅ Missing descriptor issues show "Waiting for mandatory descriptor X"
- ✅ DDS discovery issues show connection attempts and failures

---

## 9. Future Enhancements

### 9.1 Structured Logging
- Add semantic logging with structured data (JSON)
- Query logs programmatically in tests
- Integration with Seq/Elasticsearch for production

### 9.2 Distributed Tracing
- Add correlation IDs for cross-node message tracking
- Visualize message flow between nodes
- Measure latency across replication pipeline

### 9.3 Test Coverage Expansion
- Load testing (1000+ entities)
- Network failure simulation (packet loss, latency)
- Chaos engineering (random node crashes)
- Performance regression tests

### 9.4 Production Monitoring
- Metrics export (Prometheus)
- Health checks and dashboards
- Alerting for error rate thresholds
- Log aggregation service integration

---

## 10. References

- **Design Talk:** `docs/distrib-tests-and-logging-design-talk.md`
- **Tank Design:** `docs/TANK-DESIGN.md`
- **NLog Documentation:** https://nlog-project.org/
- **AsyncLocal Guide:** https://docs.microsoft.com/dotnet/api/system.threading.asynclocal-1
