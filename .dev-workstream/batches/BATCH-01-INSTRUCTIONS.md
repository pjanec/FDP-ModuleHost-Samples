# BATCH-01: Foundation & Network Setup

**Batch Number:** BATCH-01  
**Tasks:** EXT-1-1, EXT-1-2, EXT-1-3, EXT-1-4, EXT-2-2, EXT-2-1  
**Phase:** Phase 1 (Foundation) + Phase 2 Start  
**Estimated Effort:** 8-10 Hours  
**Priority:** HIGH  
**Dependencies:** None  

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to the first batch of the Extraction Refactor! This batch establishes the structural foundation for the entire project and starts the critical network layer extraction. 

**This is a "Demanding" Batch.**
We are combining Phase 1 (Setup) with the start of Phase 2 (Network Types & Mapping) to validate the architecture immediately. Precision is key.

### Required Reading (IN ORDER)
1. **[ONBOARDING.md](../ONBOARDING.md)** - **READ THIS FIRST**.
2. **[EXTRACTION-TASK-TRACKER.md](../../docs/EXTRACTION-TASK-TRACKER.md)** - Review Phase 1 & 2 status.
3. **[EXTRACTION-TASK-DETAILS.md](../../docs/EXTRACTION-TASK-DETAILS.md)** - The detailed specs for tasks below.
4. **[EXTRACTION-REFINEMENTS.md](../../docs/EXTRACTION-REFINEMENTS.md)** - Read section on "Zero Knowledge of Wire Format".

### Source Code Location
- **ModuleHost Solution:** `ModuleHost/ModuleHost.sln` (Main work area)
- **Samples Solution:** `Samples.sln` (Secondary check)

### Report Submission
When done, submit your report to: `.dev-workstream/reports/BATCH-01-REPORT.md`

---

## 🚦 Current Progress Snapshot (2026-01-30)

- Task 1 (Foundation) ✅ Complete - solutions build successfully
- Task 2 (Core Abstractions) ✅ Complete - All interface tests passing (NetworkInterfacesTests + MigrationSmokeTests)
- Last test run: All 4 tests passing - INetworkIdAllocator_CanBeMocked, INetworkTopology_CanBeMocked, KernelCreation_BeforeMigration_Succeeds, ComponentRegistration_BeforeMigration_Succeeds
- **Next:** Task 3 - Define DDS Topics (NetworkAppId, EntityMasterTopic, etc.)
  
## 🎯 Immediate Next Actions

- Create test project: `ModuleHost.Network.Cyclone.Tests` with xUnit and reference to ModuleHost.Network.Cyclone
- Implement CommonTypes.cs with NetworkAppId struct and enums
- Implement EntityMasterTopic.cs with DDS attributes
- Create TopicSchemaTests.cs with validation tests
- Run tests to verify schema validation before proceeding to Task 4

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (Setup):** Create Projects → successful build ✅
2. **Task 2 (Interfaces):** Define Interfaces → Write Compilation Tests → **pass** ✅
3. **Task 3 (Topics):** Define Structs → Write Schema Tests → **pass** ✅
4. **Task 4 (Logic):** Implement Mapper → Write Logic Tests → **pass** ✅

**DO NOT** move to the next task until the current one is solid.

---

## ✅ Tasks

### Task 1: Foundation Setup (Rules of Engagement)
**Tasks covered:** EXT-1-1, EXT-1-2

**Goal:** Create the two main extraction targets: `ModuleHost.Network.Cyclone` and `Fdp.Modules.Geographic`.

**Specs:**
- Follow **EXT-1-1** and **EXT-1-2** in [EXTRACTION-TASK-DETAILS.md](../../docs/EXTRACTION-TASK-DETAILS.md) exactly.
- **Correction:** Add `ModuleHost.Network.Cyclone` to `ModuleHost.sln`.
- **Correction:** Add `Fdp.Modules.Geographic` to **BOTH** `ModuleHost.sln` (as it's a core module) and `Samples.sln` (for visibility).

**Verify:**
- `dotnet build ModuleHost/ModuleHost.sln` succeeds.

---

### Task 2: Core Abstractions & Smoke Test
**Tasks covered:** EXT-1-3, EXT-1-4

**Goal:** Establish the interfaces that allow decupling.

**Specs:**
- Follow **EXT-1-3** to define `INetworkIdAllocator` and `INetworkTopology`.
    - **Note:** Ensure `INetworkTopology` is in `ModuleHost.Core.Network.Interfaces` for now. Later steps might move it, but follow the task detail.
- Follow **EXT-1-4** to create the Migration Smoke Test.

**Tests Required:**
- ✅ `INetworkIdAllocator_CanBeMocked`
- ✅ `INetworkTopology_CanBeMocked`
- ✅ `MigrationSmokeTests` (Verify baseline passes)

---

### Task 3: Define DDS Topics (Data Types)
**Tasks covered:** EXT-2-2

**Goal:** Define the wire-format structs. This is the "Contract" between peers.

**Specs:**
- Follow **EXT-2-2** in [EXTRACTION-TASK-DETAILS.md](../../docs/EXTRACTION-TASK-DETAILS.md).
- Implement `NetworkAppId`, `EntityMasterTopic`, etc. in `ModuleHost.Network.Cyclone/Topics/`.
- Ensure `CycloneDDS.Schema` attributes are applied correctly.

**Tests Required:**
- ✅ `CommonTypes_ValidateEnums`
- ✅ `EntityMasterTopic_HasCorrectKeys`
- ✅ `NetworkAppId_Equality_Works`

---

### Task 4: NodeIdMapper Service (Logic)
**Tasks covered:** EXT-2-1

**Goal:** Implement the translation implementation between DDS IDs ("Alpha:100") and Core IDs (int).

**Specs:**
- Follow **EXT-2-1**.
- Implement `NodeIdMapper.cs` in `ModuleHost.Network.Cyclone/Services/`.
- This service depends on `NetworkAppId` defined in Task 3.

**Tests Required:**
- ✅ `LocalNode_AlwaysHasId1`
- ✅ `NewExternalId_GetsUniqueInternalId`
- ✅ `Bidirectional_Mapping_Consistent`
- ✅ `ConcurrentAccess_ThreadSafe`

---

## 🧪 Testing Requirements

**Total Expected New/Updated Tests:** >15

**Quality Gates:**
1. **Compilation:** Both Solutions must build cleanly.
2. **Tests:** All new tests in `ModuleHost.Network.Cyclone.Tests` must pass.
3. **Smoke:** `MigrationSmokeTests` in Core must pass.

---

## ⚠️ Common Pitfalls

1. **Namespace Confusion:**
   - Core Interfaces: `ModuleHost.Core.Network.Interfaces`
   - Cyclone Implementation: `ModuleHost.Network.Cyclone.*`
   - **Do not mix them up.**

2. **Project References:**
   - Ensure `ModuleHost.Network.Cyclone` references `ModuleHost.Core`.
   - Ensure `ModuleHost.Network.Cyclone` references `CycloneDDS.Runtime` (fast bindings).

3. **Test Project:**
   - You will need to create `ModuleHost.Network.Cyclone.Tests` project if it's not explicitly mentioned in EXT-1-1. **CREATE IT**.
   - Reference `ModuleHost.Network.Cyclone` and `xunit`.

---

## 📚 Reference Materials
- [EXTRACTION-TASK-DETAILS.md](../../docs/EXTRACTION-TASK-DETAILS.md) - **The Source of Truth**
- [EXTRACTION-DESIGN.md](../../docs/EXTRACTION-DESIGN.md) - The Vision

Good luck. Speed is good, accuracy is better.
