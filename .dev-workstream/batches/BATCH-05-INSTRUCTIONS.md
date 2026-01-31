# BATCH-05: Finalize Network & Begin Geographic Extraction

**Batch Number:** BATCH-05  
**Tasks:** EXT-2-7, EXT-3-1, EXT-3-2  
**Phase:** Phase 2 (Finish) & Phase 3 (Start)  
**Estimated Effort:** 6-8 Hours  
**Priority:** NORMAL  
**Dependencies:** BATCH-04.1 (Complete)  

---

## 📋 Onboarding & Workflow

### Context
We have successfully extracted the Network Layer locally. To finalize Phase 2, we need a **Test Server** for the ID Allocator to ensure distributed scenarios work.
Then, we immediately start **Phase 3: Geographic Extraction**. The goal is to move all GIS/Coordinate code out of Core and into `Fdp.Modules.Geographic`.

### Source Code Location
- **Network (Finish):** `ModuleHost.Network.Cyclone/Services/`
- **Geographic (Start):** `Fdp.Modules.Geographic/` & `ModuleHost/ModuleHost.Core/Geographic/` (Source to delete later)

---

## ✅ Tasks

### Task 1: Create ID Allocator Server (EXT-2-7) 🆕
**Goal:** Implement a DDS Server to handle ID allocation requests for integration testing.

**Specs:**
1.  **Implement** `DdsIdAllocatorServer` in `ModuleHost.Network.Cyclone/Services/`.
    - Reference: [TASK-EXT-2-7-IdAllocatorServer.md](../../docs/TASK-EXT-2-7-IdAllocatorServer.md)
    - Must handle `Alloc`, `Reset`, and `Status` requests.
2.  **Create Integration Test:**
    - `ModuleHost.Network.Cyclone.Tests/Integration/IdAllocatorIntegrationTests.cs`
    - Verify Roundtrip (Alloc -> Response).
    - Verify Reset (Global reset clears client pools).
3.  **Run Tests:** Ensure `dotnet test ModuleHost.Network.Cyclone.Tests` passes.

---

### Task 2: Move Geographic Components (EXT-3-1)
**Goal:** Move pure data components to the new module.

**Specs:**
1.  **Identify Components:**
    - `PositionGeodetic` (Data struct)
    - `GeodeticVelocity` (if exists)
2.  **Create Files:**
    - `Fdp.Modules.Geographic/Components/PositionGeodetic.cs`
3.  **Refactor:**
    - Namespace: `Fdp.Modules.Geographic.Components`
    - Ensure they implement `IComponent` or `IComponentData` as required.
4.  **Note:** Do **NOT** delete files from Core yet. In this batch, we *copy* and *establish* the new module. Deletion happens in Phase 5.

---

### Task 3: Move Geographic Systems (EXT-3-2)
**Goal:** Move coordinate systems.

**Specs:**
1.  **Identify Systems:**
    - `GeodeticSmoothingSystem`
    - `CoordinateTransformSystem` (or similar, check `ModuleHost.Core/Geographic` folder)
2.  **Move/Copy Logic:**
    - Implement in `Fdp.Modules.Geographic/Systems/`.
    - Namespace: `Fdp.Modules.Geographic.Systems`.
3.  **Dependencies:**
    - If systems depend on `ModuleHost.Core`, reference it (Project Reference is already set).
    - If they depend on `WGS84`, ensure the Transform logic is also moved (Task EXT-3-3 is next, but you might need to grab utils now).

---

## 🧪 Testing Requirements

**Success Criteria:**
1.  `IdAllocatorIntegrationTests` passing (Server logic verified).
2.  `Fdp.Modules.Geographic` compiles.
3.  Unit tests added for `GeodeticSmoothingSystem` in `Fdp.Modules.Geographic.Tests` (create project if missing, or add to existing tests).

**Quality Check:**
- Ensure `ModuleHost.Core` still builds (we are not deleting strict dependencies yet, just preparing the destination).

Good luck.
