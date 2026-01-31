# BATCH-01.1: Foundation & Network Setup - Corrections

**Batch Number:** BATCH-01.1 (Corrective)  
**Parent Batch:** BATCH-01  
**Estimated Effort:** 4-6 Hours  
**Priority:** HIGH (Corrective)  

---

## 📋 Onboarding & Workflow

### Background
This is a **corrective batch** addressing issues found in BATCH-01 review.
**Review with Issues:** `.dev-workstream/reviews/BATCH-01-REVIEW.md`

Tasks 1 & 2 were successful. **Tasks 3 & 4 were missed.**

---

## 🎯 Objectives

1.  **Create Test Project:** Establish `ModuleHost.Network.Cyclone.Tests`.
2.  **Implement Data Types:** Define DDS Topics (Task 3).
3.  **Implement Logic:** Define NodeIdMapper (Task 4).

---

## ✅ Tasks

### Task 1: Create Test Project (Missing)
**Goal:** Create the unit test project for the network module.

**Specs:**
1. Create `ModuleHost\ModuleHost.Network.Cyclone.Tests\ModuleHost.Network.Cyclone.Tests.csproj`.
2. References:
   - `ModuleHost.Network.Cyclone`
   - `CycloneDDS.Runtime`
   - `xunit`
   - `Microsoft.NET.Test.Sdk`
   - `xunit.runner.visualstudio`
3. Add to `ModuleHost.sln`.

---

### Task 2: Define DDS Topics (Task 3 from BATCH-01)
**Goal:** Implement the missing topics.

**Files to Create:**
- `ModuleHost.Network.Cyclone/Topics/CommonTypes.cs`:
  - `NetworkAppId` struct (with `[DdsStruct]`)
  - `NetworkAffiliation` enum
  - `NetworkLifecycleState` enum
- `ModuleHost.Network.Cyclone/Topics/EntityMasterTopic.cs`
- `ModuleHost.Network.Cyclone/Topics/EntityStateTopic.cs`

**Tests Required:**
- Create `ModuleHost.Network.Cyclone.Tests/Topics/TopicSchemaTests.cs`:
  - Verify `NetworkAppId` equality.
  - Verify structs have correct `[DdsId]` attributes.

---

### Task 3: NodeIdMapper Service (Task 4 from BATCH-01)
**Goal:** Implement the missing ID mapping logic.

**Files to Create:**
- `ModuleHost.Network.Cyclone/Services/NodeIdMapper.cs`

**Tests Required:**
- Create `ModuleHost.Network.Cyclone.Tests/Services/NodeIdMapperTests.cs`:
  - `LocalNode_AlwaysHasId1`: Verify constructor reserves ID 1.
  - `NewExternalId_GetsUniqueInternalId`: Verify new mappings.
  - `Bidirectional_Mapping_Consistent`: Verify GetExternal(GetInternal(x)) == x.

---

## 🧪 Testing Requirements

**Success Criteria:**
1.  `dotnet test ModuleHost/ModuleHost.sln` must pass.
2.  At least **8 new tests** implemented (covering Topics and Mapper).

## 📚 Reference Materials
- [EXTRACTION-TASK-DETAILS.md](../../docs/EXTRACTION-TASK-DETAILS.md) - Tasks EXT-2-1, EXT-2-2.

Good luck. Complete the mission.
