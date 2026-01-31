# BATCH-06: Geographic Extraction & Component Migration

**Batch Number:** BATCH-06  
**Tasks:** EXT-3-3, EXT-3-4, EXT-4-1  
**Phase:** Phase 3 (Finish) & Phase 4 (Start)  
**Estimated Effort:** 4-6 Hours  
**Priority:** NORMAL  
**Dependencies:** BATCH-05 (Complete)  

---

## 📋 Onboarding & Workflow

### Context
We have the foundational `Fdp.Modules.Geographic` compiled. Now we need to finish moving the Transforms and wrap it all in a formal `IModule`.
Then we start **Phase 4**, where we prepare the `BattleRoyale` example application to define its own components, breaking the dependency on `ModuleHost.Core` for things like `Position` and `Health`.

### Source Code Location
- **Geographic:** `Fdp.Modules.Geographic/`
- **Transforms:** `ModuleHost/ModuleHost.Core/Geographic/` (Source)
- **Application:** `Fdp.Examples.BattleRoyale/`

---

## ✅ Tasks

### Task 1: Complete Transforms & Module (EXT-3-3, EXT-3-4)
**Goal:** Formalize the Geographic Module.

**Specs:**
1.  **Move `WGS84Transform`** (if not already moved) to `Fdp.Modules.Geographic/Transforms/`.
    - Ensure it implements `IGeographicTransform`.
2.  **Create `GeographicModule.cs`** in `Fdp.Modules.Geographic/`.
    - It must implement `IModule`.
    - It should register `GeodeticSmoothingSystem` and `CoordinateTransformSystem` in `RegisterSystems`.
3.  **Verification:**
    - Create a unit test `GeographicModuleTests.cs` that verifies the module registers its systems correctly.

---

### Task 2: Define Components in BattleRoyale (EXT-4-1)
**Goal:** Define local components in the example app to prepare for breaking Core dependency.

**Specs:**
1.  **Create Component Files** in `Fdp.Examples.BattleRoyale/Components/`:
    - `Position.cs` (Copy struct from Core but Namespace: `Fdp.Examples.BattleRoyale.Components`)
    - `Velocity.cs`
    - `Health.cs`
2.  **Constraint:** Do NOT update the systems yet. We are just creating the definitions. `BattleRoyale` code will still use Core components for now.
3.  **Note:** This prepares us for the "Big Switch" where we change `using ModuleHost.Core.Network` to `using Fdp.Examples.BattleRoyale.Components`.

---

## 🧪 Testing Requirements

**Success Criteria:**
1.  `Fdp.Modules.Geographic` compiles and has full coverage of its systems (from Batch 05) and module registration.
2.  `Fdp.Examples.BattleRoyale` compiles (the new components sit side-by-side with old ones, unused).

**Next Step Prep:**
- In BATCH-07, we will rewrite BattleRoyale to use these new components.
