# FDP Engine Distributed Recording and Playback - Developer Onboarding

Welcome to the FDP Engine Distributed Recording and Playback project! This document will help you get up to speed quickly.

---

## What We're Building

We're extending the FDP (Flight Data Processor) Engine to support **Distributed Recording and Playback** - a sophisticated feature that allows multiple networked nodes to record only their owned entity components, then replay them while simultaneously receiving remote data from other nodes' replays, perfectly reconstructing the complete distributed simulation.

### The Demo: "Composite Tank"

Our showcase is a multiplayer tank scenario where:
- **Node A (Driver)** controls the tank's movement (position/rotation) via geodetic network protocol (WGS84)
- **Node B (Gunner)** controls the tank's turret (yaw/pitch) via a simple auto-translated component
- Both nodes can record their session and replay it later
- During replay, each node broadcasts its recorded actions as if they were live
- The other node receives this replay stream and reconstructs the complete tank state

This demonstrates:
1. **Partial Component Ownership** - Single entities with mixed ownership
2. **Geographic Translation** - Network uses Lat/Lon, engine uses flat coordinates
3. **Deterministic Time** - Runtime switching between smooth and lockstep modes
4. **Zero Boilerplate Networking** - Attribute-based automatic synchronization
5. **High-Fidelity Replay** - Distributed reconstruction of complex interactions

---

## Project Structure

### Core Projects (Existing)

**Fdp.Kernel** (Tier 1 - ECS Core)
- Location: `ModuleHost/FDP/`
- Purpose: High-performance entity-component system with chunk-based memory management
- Key Systems: `EntityRepository`, `FlightRecorder`, `PlaybackSystem`

**FDP.Toolkit.Replication** (Tier 2 - Network Layer)
- Location: `ModuleHost/FDP.Toolkit.Replication/`
- Purpose: Network synchronization toolkit with SST (Single Source of Truth) ownership
- Key Components: `NetworkPosition`, `NetworkAuthority`, `NetworkIdentity`

**ModuleHost.Network.Cyclone** (Tier 3 - DDS Plugin)
- Location: `ModuleHost.Network.Cyclone/`
- Purpose: CycloneDDS integration for network transport
- Key Systems: Data readers/writers, serialization providers

**Fdp.Modules.Geographic** (Tier 3 - Coordinate Plugin)
- Location: `Fdp.Modules.Geographic/`
- Purpose: WGS84 ↔ Cartesian coordinate transformation
- Key Interface: `IGeographicTransform`

### Demo Project (Our Work)

**Fdp.Examples.NetworkDemo**
- Location: `Fdp.Examples.NetworkDemo/`
- Purpose: Showcase all FDP capabilities in a coherent demo
- Structure:
  ```
  Fdp.Examples.NetworkDemo/
    Components/          - Internal simulation components (DemoPosition, etc.)
    Descriptors/         - Network protocol structures (GeoStateDescriptor, etc.)
    Translators/         - Network ↔ ECS bridges (GeodeticTranslator, etc.)
    Systems/             - Simulation logic (ReplayBridgeSystem, TransformSyncSystem, etc.)
    Configuration/       - Metadata, TKB templates, recorder setup
    Program.cs           - Application entry point
  ```

---

## Key Documentation

1. **[DESIGN.md](./TANK-DESIGN.md)** - Complete architectural design
   - Read this first to understand the full picture
   - Pay special attention to § 4 (Shadow World Pattern) and § 3.1 (ID Management)


4. **[DEV-GUIDE.md](./.dev-workstream/DEV-GUIDE.md)** - Development practices
   - How to write code, tests, and documentation
   - Code style, git workflow, and PR requirements

---

## Architecture Overview

### The Shadow World Pattern

The core innovation is the **Shadow World** - an isolated entity repository that holds recorded data. During replay:

1. Load recording into Shadow World (isolated)
2. `ReplayBridgeSystem` runs every frame:
   - Advances Shadow World (reads next frame from disk)
   - For each entity, checks **what we owned** during recording
   - Selectively copies owned components from Shadow → Live World
   - Leaves unowned components untouched (they come from network)
3. Live World systems react normally:
   - `TransformSyncSystem` sees position changed → copies to network buffer
   - `GeodeticTranslator` sees buffer changed → converts to geodetic
   - Network egress sees geodetic data → broadcasts to peers
4. Remote node receives our replay stream as if it were live input

### Entity ID Management

To prevent collisions between recorded entities and new live entities:

**ID Partitioning:**
```
Range 0 - 65,535:     System entities (UI, managers) - NEVER RECORDED
Range 65,536+:        Simulation entities (tanks, projectiles) - RECORDED
```

**Why 65,536?**
- FDP uses 64KB chunks for components
- Smallest component (1 byte) fits 65,536 entities per chunk
- Gap ensures System entities (Chunk 0) never overlap with Recorded entities (Chunk 1+)

**Sidecar Metadata:**
- Recording ends: Save `{ MaxEntityId: 70000 }` to `.fdp.meta` file
- Replay starts: Load metadata, call `world.ReserveIdRange(70000)`
- New ghosts allocate from 70001+ (no collision with recorded 65536-70000)

### Component Recording Policy

**Recorded (Application State):**
- `DemoPosition` - Physics result, drives network
- `TurretState` - User input result
- All application-specific components

**Not Recorded (Transient Buffers):**
- `NetworkPosition` - Marked `[DataPolicy(NoRecord)]`, derived from `DemoPosition`
- `NetworkIdentity` - Runtime session data
- `NetworkAuthority` - Runtime topology

**Why?** Replaying internal state lets the engine re-run all translation/smoothing logic with current code, so bug fixes improve replay quality.

### Data Flow (Live Session)

```
Player Input
    ↓
Physics System
    ↓
DemoPosition (Internal) ← [RECORDED TO DISK]
    ↓
TransformSyncSystem (if owned)
    ↓
NetworkPosition (Buffer)
    ↓
GeodeticTranslator
    ↓
GeoStateDescriptor (DDS)
    ↓
Network → Remote Node
```

### Data Flow (Replay Session)

```
Disk
    ↓
Shadow World (Isolated)
    ↓
ReplayBridgeSystem (checks authority)
    ↓
DemoPosition (Live World) ← [INJECTED]
    ↓
TransformSyncSystem (if owned)
    ↓
NetworkPosition (Buffer)
    ↓
GeodeticTranslator
    ↓
GeoStateDescriptor (DDS)
    ↓
Network → Remote Node (receiving their replay)
```

---

## Building the Project

### Prerequisites

- **Windows 10/11** (FDP uses Windows-specific memory APIs)
- **.NET 8.0 SDK** or later
- **Visual Studio 2022** (recommended) or Rider
- **Git** for version control

### Build Steps

1. **Clone the repository:**
   ```powershell
   git clone <repository-url>
   cd FDP-ModuleHost-Samples
   ```

2. **Restore dependencies:**
   ```powershell
   dotnet restore Samples.sln
   ```

3. **Build solution:**
   ```powershell
   dotnet build Samples.sln --configuration Debug
   ```

4. **Run tests:**
   ```powershell
   dotnet test Samples.sln
   ```

### Running the Demo

**Live Mode (2 terminals):**
```powershell
# Terminal 1 - Node A (Driver)
dotnet run --project Fdp.Examples.NetworkDemo -- 1 live

# Terminal 2 - Node B (Gunner)
dotnet run --project Fdp.Examples.NetworkDemo -- 2 live
```

**Replay Mode (after recording):**
```powershell
# Terminal 1 - Node A Replay
dotnet run --project Fdp.Examples.NetworkDemo -- 1 replay

# Terminal 2 - Node B Replay
dotnet run --project Fdp.Examples.NetworkDemo -- 2 replay
```

**Controls (Live):**
- `WASD` - Drive tank (Node A)
- `Mouse` - Aim turret (Node B)
- `T` - Toggle time mode (Continuous ↔ Deterministic)
- `↑/↓` - Adjust time scale
- `→` - Manual step (when paused in deterministic mode)

**Controls (Replay):**
- `Space` - Pause/Resume
- `↑/↓` - Adjust playback speed (0.5x - 4x)
- `→` - Single-step (when paused)

---

## Development Workflow

### Phase-Based Approach

We're implementing this in 6 phases (see [TASK-TRACKER.md](./TASK-TRACKER.md)):

1. **Phase 1: Kernel Foundation** - ID management in FDP core
2. **Phase 2: Replication Toolkit** - Zero-boilerplate networking
3. **Phase 3: Demo Infrastructure** - Components and translators
4. **Phase 4: Systems** - Replay bridge and sync logic
5. **Phase 5: Integration** - Wire everything together
6. **Phase 6: Testing** - Validate and benchmark

### Before Starting Work

1. Read the relevant task in [TASK-DETAIL.md](./TASK-DETAIL.md)
2. Check dependencies - ensure prerequisite tasks are complete
3. Review success conditions - know what "done" means
4. Create a feature branch: `git checkout -b feature/FDP-DRP-XXX`

### During Development

1. Write unit tests first (TDD approach)
2. Implement to make tests pass
3. Run full test suite: `dotnet test`
4. Update task tracker when complete
5. Commit with clear messages: `[FDP-DRP-XXX] Brief description`

### Code Quality Standards

- **Performance:** No allocations in hot paths (use `ref`, `in`, `stackalloc`)
- **Safety:** All public APIs must validate inputs
- **Clarity:** Prefer clear code over clever code
- **Testing:** Every public method must have unit test
- **Documentation:** XML comments on all public APIs

See [DEV-GUIDE.md](./.dev-workstream/DEV-GUIDE.md) for complete standards.

---

## Key Concepts to Master

### 1. FDP ECS Architecture

- **Chunk-Based Storage:** Components stored in 64KB memory chunks
- **Sparse Indices:** Entity IDs can have gaps, memory only allocated for active chunks
- **Unmanaged Types:** All components must be `unmanaged` (no references)
- **Command Buffer:** Structural changes deferred to end of frame

**Resources:**
- `ModuleHost/FDP/EntityRepository.cs` - Core ECS implementation
- `ModuleHost/FDP/docs/` - Architecture documentation

### 2. SST Ownership Model

- **EntityMaster:** One node owns entity lifecycle (create/destroy)
- **Descriptors:** Specific data packets can be owned by different nodes
- **Granular Authority:** `HasAuthority(entity, descriptorKey)` checks specific ownership
- **Ownership Transfer:** Runtime protocol for changing authority

**Resources:**
- `docs/bdc-sst-rules.md` - SST specification
- `FDP.Toolkit.Replication/Components/NetworkAuthority.cs` - Authority component

### 3. Recording System

- **Binary Format:** Raw memory dumps for maximum speed
- **Chunk Granularity:** Records entire chunks, not individual entities
- **Metadata Header:** Frame count, component types, tick rate
- **Streaming:** Async writes to prevent blocking simulation

**Resources:**
- `ModuleHost/FDP/FlightRecorder/RecorderSystem.cs` - Recording implementation
- `ModuleHost/FDP/FlightRecorder/PlaybackSystem.cs` - Playback implementation

### 4. Network Translation

- **Descriptors:** Network protocol structures (DDS topics)
- **Components:** Internal ECS data
- **Translators:** Bidirectional mappers between descriptor ↔ component
- **Ownership Checking:** Only publish what we own

**Resources:**
- `ModuleHost.Network.Cyclone/Translators/` - Example translators
- `FDP.Toolkit.Replication/Translators/` - Generic translator (our work)

---

## Common Patterns

### Creating a Component

```csharp
// Internal component (recorded)
public struct DemoPosition
{
    public Vector3 Value;
}

// Buffer component (not recorded)
[DataPolicy(DataPolicy.NoRecord)]
public struct NetworkPosition
{
    public Vector3 Value;
}
```

### Creating a Network Descriptor (Auto)

```csharp
[FdpDescriptor(ordinal: 10, topicName: "Tank_Turret")]
[DdsTopic("Tank_Turret")]
public struct TurretState
{
    [DdsKey] public long EntityId;
    public float YawAngle;
    public float PitchAngle;
}
```

### Creating a Manual Translator

```csharp
public class GeodeticTranslator : IDescriptorTranslator
{
    public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, ISimulationView view)
    {
        foreach (var sample in reader.TakeSamples()) {
            // Map entity
            if (!_entityMap.TryGetEntity(sample.EntityId, out Entity entity))
                continue;
            
            // Convert descriptor → component
            var geoData = (GeoStateDescriptor)sample.Data;
            var flatPos = _geoTransform.ToCartesian(geoData.Latitude, geoData.Longitude, ...);
            cmd.SetComponent(entity, new NetworkPosition { Value = flatPos });
        }
    }
    
    public void ScanAndPublish(ISimulationView view, IDataWriter writer)
    {
        var query = view.Query().With<NetworkPosition>().With<NetworkAuthority>().Build();
        
        foreach (var entity in query) {
            // Check ownership
            if (!view.HasAuthority(entity, DescriptorOrdinal))
                continue;
            
            // Convert component → descriptor
            var flatPos = view.GetComponentRO<NetworkPosition>(entity);
            var (lat, lon, alt) = _geoTransform.ToGeodetic(flatPos.Value);
            writer.Write(new GeoStateDescriptor { Latitude = lat, ... });
        }
    }
}
```

### Checking Authority in Replay Bridge

```csharp
// In Shadow World, check what we owned during recording
if (_shadowRepo.HasAuthority(shadowEntity, CHASSIS_KEY)) {
    // We owned chassis - inject it into live world
    var pos = _shadowRepo.GetComponentRO<DemoPosition>(shadowEntity);
    cmd.SetComponent(liveEntity, pos);
}

// Don't inject turret if we didn't own it
// It will come from the network (remote node's replay)
```

---

## Testing Strategy

### Unit Tests

- Test individual components in isolation
- Mock dependencies
- Fast execution (< 1ms per test)
- Location: `{ProjectName}.Tests/`

**Example:**
```csharp
[Test]
public void ReserveIdRange_PreventsCollision() {
    var repo = new EntityRepository();
    repo.ReserveIdRange(1000);
    var e = repo.CreateEntity();
    Assert.That(e.Index, Is.GreaterThan(1000));
}
```

### Integration Tests

- Test system interactions
- Use real dependencies where feasible
- Moderate execution (< 100ms per test)
- Location: `Fdp.Examples.NetworkDemo.Tests/Integration/`

**Example:**
```csharp
[Test]
public async Task FullScenario_RecordAndReplay() {
    var nodeA = StartNode(1, isReplay: false);
    var nodeB = StartNode(2, isReplay: false);
    
    // Simulate session...
    
    nodeA.Stop();
    nodeB.Stop();
    
    // Replay...
    var replayA = StartNode(1, isReplay: true);
    var replayB = StartNode(2, isReplay: true);
    
    // Validate reconstruction...
}
```

### Performance Tests

- Benchmark critical paths
- Establish regression baselines
- Tagged `[Category("Performance")]`
- Run separately from main suite

---

## Debugging Tips

### Recording Issues

**Problem:** System entities appearing in recording
**Solution:** Verify `RecorderSystem.MinRecordableId = 65536`

**Problem:** Metadata file not created
**Solution:** Check `recorder.Dispose()` is called before metadata save

**Problem:** Recorded file size too large
**Solution:** Verify `[DataPolicy(NoRecord)]` on buffer components

### Replay Issues

**Problem:** Entity not found during replay
**Solution:** Check ID reservation happened before playback starts

**Problem:** Authority checks failing
**Solution:** Verify `DescriptorOwnership` component exists in shadow world

**Problem:** Shadow/Live ID mismatch
**Solution:** Ensure same ID reservation value used for both

### Network Issues

**Problem:** Geographic coordinates incorrect
**Solution:** Verify origin set correctly: `transform.SetOrigin(52.52, 13.40, 0)`

**Problem:** Turret not syncing
**Solution:** Check auto-translator registered: `ReplicationBootstrap.CreateAutoTranslators()`

**Problem:** Ownership conflicts
**Solution:** Verify only one node has authority for each descriptor

### Time Synchronization Issues

**Problem:** Mode switch doesn't happen
**Solution:** Check all nodes received barrier event and ACKed

**Problem:** Deterministic mode desyncs
**Solution:** Verify all nodes using same tick rate and frame counter

---

## Logging Infrastructure

The project uses **NLog** for high-performance logging with AsyncLocal context flow for distributed system diagnostics.

### Architecture

- **Static Facade:** `FdpLog<T>` - Zero-allocation logging interface
- **Context Flow:** AsyncLocal automatically tracks NodeId across Tasks/awaits
- **Granular Filtering:** Module-level log configuration (Trace for Network, Warn for Kernel)
- **Async I/O:** Background file writes to avoid blocking simulation loop

### Basic Usage

```csharp
using FDP.Kernel.Logging;

// In performance-critical code (hot path)
if (FdpLog<MySystem>.IsDebugEnabled)
    FdpLog<MySystem>.Debug($"Entity {entity.Index} moved to {position}");

// For non-critical paths
FdpLog<MyModule>.Info("System initialized successfully");
FdpLog<MyModule>.Error("Critical failure detected", exception);
```

### Configuration

See `Fdp.Examples.NetworkDemo/Configuration/LogSetup.cs` for setup options:

```csharp
// Development mode - verbose logging
LogSetup.ConfigureForDevelopment(nodeId: 100, verboseTrace: true);

// Production mode - optimized
LogSetup.ConfigureForProduction(nodeId: 100);

// Test mode - in-memory capture
LogSetup.ConfigureForTesting(testName: "MyTest");
```

### Log Output

Logs are written to `logs/node_{nodeId}.log` with automatic node identification:

```
2026-02-07 14:32:15.234|DEBUG|Node-100|CycloneNetworkModule|Registered 2 translators
2026-02-07 14:32:15.256|TRACE|Node-100|GenericDescriptorTranslator|Entity 65536: HasAuth(5)=True
2026-02-07 14:32:15.278|INFO|Node-200|CycloneIngress|Created ghost for NetID 100_65536
```

### Performance Guidelines

- **Always guard expensive operations:**
  ```csharp
  // BAD - Always allocates interpolated string
  FdpLog<MySystem>.Debug($"Position: {pos.X}, {pos.Y}, {pos.Z}");
  
  // GOOD - Zero allocation if disabled
  if (FdpLog<MySystem>.IsDebugEnabled)
      FdpLog<MySystem>.Debug($"Position: {pos.X}, {pos.Y}, {pos.Z}");
  ```

- **Use appropriate log levels:**
  - `Trace` - Detailed flow (rarely enabled, hot path)
  - `Debug` - Important state changes
  - `Info` - Lifecycle events, mode switches
  - `Warn` - Recoverable issues
  - `Error` - Critical failures

### Troubleshooting with Logs

Common diagnostic patterns:

**"Remote: 0" issue:**
```powershell
# Check if descriptors are being published
grep "Published descriptor" logs/node_100.log

# Check if remote node receives them
grep "Received sample" logs/node_200.log
```

**Ownership issues:**
```powershell
# Should see "Skipped" for non-owned descriptors
grep "Skipped.*Not Owner" logs/node_100.log
```

**See [LOGGING-AND-TESTING-DESIGN.md](./docs/LOGGING-AND-TESTING-DESIGN.md) for complete architecture.**

---

## Testing Framework

The project includes a comprehensive **Distributed E2E Testing Framework** for verifying networked simulation behavior.

### Running Tests

```powershell
# Run all tests
dotnet test Fdp.Examples.NetworkDemo.Tests

# Run specific test
dotnet test --filter "FullyQualifiedName~Entity_Lifecycle"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Test Categories

**Infrastructure Tests** (`Scenarios/InfrastructureTests.cs`)
- Verify logging scope flows correctly
- Validate AsyncLocal context across Tasks

**Replication Tests** (`Scenarios/ReplicationTests.cs`)
- Basic entity creation and synchronization
- Component data propagation
- Geographic coordinate translation

**Lifecycle Tests** (`Scenarios/LifecycleTests.cs`)
- Entity creation, activation, destruction
- Ghost protocol (orphan protection)
- Sub-entity hierarchy cleanup

**Ownership Tests** (`Scenarios/OwnershipTests.cs`)
- Partial ownership (split control)
- Dynamic ownership transfer
- Authority validation

**Advanced Tests**
- Deterministic time mode switching
- Distributed replay verification
- Reactive system behavior

### Writing New Tests

Use `DistributedTestEnv` to orchestrate multi-node scenarios:

```csharp
[Fact]
public async Task My_Distributed_Test()
{
    using var env = new DistributedTestEnv(_output);
    await env.StartNodesAsync();
    
    // Spawn entity on Node A
    var tankA = env.NodeA.SpawnTank();
    long netId = env.NodeA.GetNetworkId(tankA);
    
    // Wait for replication to Node B
    await env.WaitForCondition(
        app => app.TryGetEntityByNetId(netId, out _),
        env.NodeB,
        timeoutMs: 3000);
    
    // Verify state
    var tankB = env.NodeB.GetEntityByNetId(netId);
    Assert.NotEqual(Entity.Null, tankB);
    
    // Verify logs
    env.AssertLogContains(100, "Published descriptor");
    env.AssertLogContains(200, "Created ghost");
}
```

### Test Helpers

- `env.StartNodesAsync()` - Initialize nodes concurrently
- `env.RunFrames(count)` - Execute N simulation frames
- `env.WaitForCondition(predicate, target, timeout)` - Async condition waiting
- `env.AssertLogContains(nodeId, message)` - Log verification

**See test examples in `Fdp.Examples.NetworkDemo.Tests/Scenarios/` directory.**

---

## Getting Help

### Internal Resources

1. **Tank Design Document** - [docs/TANK-DESIGN.md](./docs/TANK-DESIGN.md) - Network demo architecture
2. **Logging & Testing Design** - [docs/LOGGING-AND-TESTING-DESIGN.md](./docs/LOGGING-AND-TESTING-DESIGN.md) - Infrastructure details
3. **Task Details** - [docs/LOGGING-AND-TESTING-TASK-DETAILS.md](./docs/LOGGING-AND-TESTING-TASK-DETAILS.md) - Implementation specs
4. **Task Tracker** - [docs/LOGGING-AND-TESTING-TASK-TRACKER.md](./docs/LOGGING-AND-TESTING-TASK-TRACKER.md) - Progress tracking
5. **Code Comments** - Most APIs have XML documentation
6. **Unit Tests** - See test files for usage examples

### External Resources

1. **FDP Kernel Docs** - `ModuleHost/FDP/docs/`
2. **CycloneDDS Docs** - DDS protocol and API reference
3. **WGS84 Spec** - Geographic coordinate system standard
4. **NLog Documentation** - https://nlog-project.org/

### Communication

- **Questions:** Create issue with `question` label
- **Bugs:** Create issue with `bug` label and reproduction steps
- **Proposals:** Create issue with `enhancement` label and design sketch

---

