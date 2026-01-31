# BATCH-09: Application Wiring & Final Demo

**Batch Number:** BATCH-09  
**Tasks:** EXT-6-1, EXT-6-2, EXT-7-4  
**Phase:** Phase 6 (Application Updates) & Phase 7 (Final Verification)  
**Estimated Effort:** 4-6 Hours  
**Priority:** NORMAL  
**Dependencies:** BATCH-08 (Complete)  

---

## 📋 Onboarding & Workflow

### Context
We have a clean Core and a working Network Plugin. But `BattleRoyale` is currently "Offline" regarding networking because it uses its own local `Position` and has no idea about the new `NetworkPosition` provided by the Cyclone plugin.
We need to **Wire them together**.

### Objective
Update `BattleRoyale` to consume the new `ModuleHost.Network.Cyclone` plugin and sync the "Shadow Components" to its "Local Components".

---

## ✅ Tasks

### Task 0: Final Core Cleanup (Residue)
**Goal:** Remove `EntityStateDescriptor` which was missed in Batch 07.

**Specs:**
1.  **Delete:** `EntityStateDescriptor` class from `ModuleHost.Core/Network/NetworkComponents.cs`.
2.  **Update:** `ModuleHost.Core.Tests/Mocks/MockDataReader.cs`.
    - It references `EntityStateDescriptor`.
    - Replace it with a local private class `MockEntityStateDescriptor` inside the test project or just check for `dynamic` or `object`.
    - Ensure `ModuleHost.Core` clearly exports NO descriptor types.

### Task 1: Create Sync System (EXT-6-1)
**Goal:** Create a system in `BattleRoyale` that copies data between Network and Local components.

**Specs:**
1.  **Create:** `Fdp.Examples.BattleRoyale/Systems/NetworkSyncSystem.cs`
2.  **Logic:**
    - **Ingress (Network -> Local):**
        - Query entities with `NetworkPosition` (Cyclone) AND `Position` (Local).
        - If `NetworkOwnership.PrimaryOwner != Local`, copy `NetworkPosition.Value` -> `Position.Value`.
    - **Egress (Local -> Network):**
        - Query entities with `Position` (Local) AND `NetworkPosition` (Cyclone).
        - If `NetworkOwnership.PrimaryOwner == Local`, copy `Position.Value` -> `NetworkPosition.Value`.
    - **Identity:**
        - Ensure entities have `NetworkIdentity` and `NetworkSpawnRequest` added when spawned by `EntityFactory`.

### Task 2: Register Network Plugin
**Goal:** Update `Program.cs`.

**Specs:**
1.  **Reference:** Ensure `BattleRoyale` references `ModuleHost.Network.Cyclone`.
2.  **Register:**
    - Register `CycloneNetworkModule` (or whatever the main entry point is called).
    - Register `NetworkSyncSystem`.

### Task 3: Final Verification (EXT-7-4)
**Goal:** Run the simulation and verifying network traffic (mocked or real).

**Specs:**
1.  **Run BattleRoyale:**
    - `dotnet run --project Fdp.Examples.BattleRoyale`
2.  **Verify Output:**
    - Ensure it runs for the full 300 frames without crashing.
    - Check console logs for "NetworkSync" activity (the existing module logging might need updates).

---

## 🧪 Testing Requirements

**Success Criteria:**
1.  `BattleRoyale` runs successfully.
2.  Code uses the new `NetworkPosition` -> `Position` bridging logic.
3.  No references to deleted Core types.

**Deliverable:**
- A fully functional Example Application running on the new Architecture.
