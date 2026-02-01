# BATCH-11: Network Integration & Validation

**Batch Number:** BATCH-11  
**Tasks:** EXT-2-7, EXT-6-4  
**Phase:** Validation & Demo  
**Estimated Effort:** 4-6 Hours  
**Priority:** NORMAL  
**Dependencies:** BATCH-10 (Complete)  

---

## 📋 Onboarding & Workflow

### Context
The extraction is complete. Now we need to validate the Distributed capabilities with a real **Network Integration Demo**.
This requires two components:
1.  **ID Allocator Server**: A centralized service to hand out Entity IDs to multiple nodes (Task EXT-2-7).
2.  **Network Demo App**: A peer-to-peer console app where two instances sync entities (Task EXT-6-4).

### Objective
Create `Fdp.Examples.IdAllocatorDemo` (Server) and `Fdp.Examples.NetworkDemo` (Peer App) and verify they work together.

---

## ✅ Tasks

### Task 1: Implement ID Allocator Server (EXT-2-7)
**Goal:** Create a standalone tool and integration tests for ID allocation.

**Specs:**
1.  **Implement Server Class**:
    - `ModuleHost.Network.Cyclone/Services/DdsIdAllocatorServer.cs`
    - Logic: Listen for `IdRequest`, respond with `IdResponse`, publish `IdStatus`.
    - Reference: [TASK-EXT-2-7-IdAllocatorServer.md](../../docs/TASK-EXT-2-7-IdAllocatorServer.md)
2.  **Add Integration Tests**:
    - `ModuleHost.Network.Cyclone.Tests/Integration/IdAllocatorServerTests.cs`
    - Verify Roundtrip (Alloc -> Response) and Reset functionality.
    - Create new project `Fdp.Examples.IdAllocatorDemo`.
    - Simple Main loop that runs the server.
    - **CRITICAL:** Update `.csproj` to copy the native DLL:
      ```xml
      <Target Name="CopyNativeDll" AfterTargets="Build">
          <Copy SourceFiles="..\..\..\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\ddsc.dll" DestinationFolder="$(OutDir)" SkipUnchangedFiles="true" />
      </Target>
      ```

### Task 2: Implement Network Demo App (EXT-6-4)
**Goal:** Create the "Alpha/Bravo" peer visualizer.

**Specs:**
1.  **Create Project**: `Fdp.Examples.NetworkDemo`.
2.  **Define Components**:
    - Local: `Position`, `Velocity`, `EntityType`.
    - Geodetic: `PositionGeodetic`.
    - Network: `NetworkedEntity` (Tracks ownership).
3.  **Implement Logic**:
    - **Bootstrap**: Register Core, Cyclone, and Geographic modules.
    - **Spawning**: Create 3 local entities ("Tank", "Jeep", "Heli").
    - **Sync System**: Sync `Position` <-> `NetworkPosition` (similar to BattleRoyale, but simpler).
    - **Visualization**: Console output showing LOCAL vs REMOTE entities with coordinates.
    - Reference: [TASK-EXT-6-4-NetworkIntegrationDemo.md](../../docs/TASK-EXT-6-4-NetworkIntegrationDemo.md)
    - **CRITICAL:** Update `.csproj` to copy the native `ddsc.dll` (same as above).

### Task 3: Automated Validation
**Goal:** Prove it works without manual eyeball checking.

**Specs:**
1.  **Implementation**:
    - Create `Fdp.Examples.NetworkDemo.Tests/NetworkDemoIntegrationTests.cs`.
    - Use `System.Diagnostics.Process` to launch 1 Server + 2 Demo Nodes.
    - Capture STDOUT.
    - Assert that both nodes report seeing 3 Local + 3 Remote entities.

---

## 🧪 Testing Requirements

**Success Criteria:**
1.  `dotnet test ModuleHost.Network.Cyclone.Tests` passes (IdAllocator logic).
2.  `dotnet test Fdp.Examples.NetworkDemo.Tests` passes (End-to-End Validation).

**Deliverable:**
- A working distributed demo that validates the entire Extraction architecture.
