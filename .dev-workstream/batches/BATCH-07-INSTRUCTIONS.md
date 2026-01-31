# BATCH-07: Core Simplification & Final Cleanup

**Batch Number:** BATCH-07  
**Tasks:** EXT-4-4, EXT-5-3, EXT-7-3  
**Phase:** Phase 4 (Finish), Phase 5, Phase 7  
**Estimated Effort:** 4-6 Hours  
**Priority:** HIGH  
**Dependencies:** BATCH-06 (Complete)  

---

## 📋 Onboarding & Workflow

### Context
`BattleRoyale` is now fully decoupled from Core (using its own components). `Geographic` is extracted. `Network` is extracted.
This means `ModuleHost.Core` is theoretically clean. **HOWEVER**, we still have old files there (legacy `Position`, `NetworkGatewayModule`, etc.) and the Core Tests likely still reference them.

### Objective
This is the **"Destructive Batch"**. We will delete the old code from Core and fix the Core tests to prove that Core is truly generic.

### Source Code Location
- **Cleanup Target:** `ModuleHost/ModuleHost.Core/`
- **Tests Target:** `ModuleHost/ModuleHost.Core.Tests/`

---

## ✅ Tasks

### Task 1: Refactor Core Tests (EXT-4-4) 🏗️
**Goal:** Ensure Core tests do not rely on legacy types before we delete them.

**Specs:**
1.  **Audit `ModuleHost.Core.Tests`**:
    - Look for usages of `ModuleHost.Core.Network.Position`, `Velocity`, `Health`.
    - Look for usages of `NetworkGatewayModule`.
2.  **Define Test Components:**
    - Create a local `TestComponents.cs` in the test project if needed (e.g., `TestPosition`, `TestVelocity`) to verify the repository can handle generic components.
    - DO NOT use the types we are about to delete.
3.  **Update Tests:**
    - Rewrite `EntityRepositoryTests` or similar to use `TestPosition` instead of `Core.Network.Position`.

---

### Task 2: Delete Old Files (EXT-5-3) 🗑️
**Goal:** Remove the legacy code.

**Specs:**
1.  **DELETE** the following from `ModuleHost.Core`:
    - `Geographic/` (Folder) - *Verify implementation in Fdp.Modules.Geographic first!*
    - `Network/Translators/` (Folder) - *Already empty/moved? Check.*
    - `Network/NetworkGatewayModule.cs` - *Already moved to Cyclone.*
    - `Network/NetworkSpawnerSystem.cs` - *Move logic to BattleRoyale if not already there, or delete if unused.*
    - `Network/NetworkSpawnRequest.cs`
    - `Network/Position.cs`
    - `Network/Velocity.cs`
    - `Network/Health.cs` (if exists)
    - `Network/DescriptorOwnership.cs`
2.  **Verify Build:**
    - `ModuleHost.Core` must compile.
    - `ModuleHost.Core.Tests` must compile.

---

### Task 3: Full Suite Verification (EXT-7-3) 🧪
**Goal:** Prove the extraction success.

**Specs:**
1.  Run **ALL** tests in the solution:
    - `dotnet test Samples.sln`
2.  **Expected Results:**
    - `ModuleHost.Core.Tests`: PASS (Generic kernel tests)
    - `ModuleHost.Network.Cyclone.Tests`: PASS (Network logic)
    - `Fdp.Modules.Geographic.Tests`: PASS (GIS logic)
    - `Fdp.Examples.BattleRoyale`: RUN (Manually verify it still runs via `dotnet run`)

---

## ⚡ Critical Checks

**Before deleting `Position.cs`:**
- Ensure `BattleRoyale` is definitely referencing `Fdp.Examples.BattleRoyale.Components.Position` and NOT `ModuleHost.Core.Network.Position`.
- Double check `csproj` references.

**Before deleting `NetworkGatewayModule.cs`:**
- Ensure `ModuleHost.Core.Tests` isn't using it. (Task 1 covers this).

**Definition of Done:**
- `ModuleHost.Core` has **ZERO** references to `CycloneDDS`.
- `ModuleHost.Core` has **ZERO** references to `Network/` logic (except abstract interfaces).
- All Projects Build & Test Pass.
