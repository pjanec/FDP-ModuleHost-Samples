# BATCH-02: Fixes & Demo Infrastructure

**Batch Number:** BATCH-02  
**Tasks:** Fixes (Batch 01), FDP-DRP-008, FDP-DRP-009, FDP-DRP-010  
**Phase:** Phase 3 (Demo Infrastructure)  
**Estimated Effort:** 8-12 hours  
**Priority:** HIGH  
**Dependencies:** BATCH-01 (Partially completed)

---

## 📋 Onboarding & Workflow

### Developer Instructions
This batch consolidates the **corrective fixes** from Batch 01 and the **infrastructure setup** for the Tank Demo (Phase 3). You must fix the foundational issues before building the demo components on top of them.

### Required Reading (IN ORDER)
1.  **Review Findings:** `.dev-workstream/reviews/BATCH-01-REVIEW.md` - Understand why the fixes are needed.
2.  **Task Definitions:** [`docs/TASK-DETAIL.md`](../../docs/TASK-DETAIL.md) - Specs for tasks 008, 009, 010.
3.  **Design Document:** [`docs/DESIGN.md`](../../docs/DESIGN.md) - Section 5.2 (Demo Components) and 8 (Metadata).

### Source Code Location
- **Toolkit Fixes:** `ModuleHost/FDP.Toolkit.Replication/`
- **Demo Project:** `Fdp.Examples.NetworkDemo/`
- **Tests:** `ModuleHost/FDP.Toolkit.Replication.Tests/` and `Fdp.Examples.NetworkDemo.Tests/` (You may need to create the demo test project).

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-02-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1.  **Fixes:** Implement fixes → Write missing tests → **ALL tests pass** ✅
2.  **Metadata:** Implement → Write tests → **ALL tests pass** ✅
3.  **Components:** Implement → Write tests → **ALL tests pass** ✅
4.  **Translator:** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the Demo Infrastructure until the Toolkit Fixes are verified.

---

## 🔧 Part 1: BATCH-01 Corrective Fixes

### Fix 1: GenericDescriptorTranslator API & Tests
**File:** `ModuleHost/FDP.Toolkit.Replication/Translators/GenericDescriptorTranslator.cs`

**Issue:** Code uses `GetManagedComponentRO` but modifies the returned object. This violates the read-only contract.
**Requirement:** Change to `GetManagedComponent` (or RW variant) when accessing `BinaryGhostStore` for modification.

**Missing Tests:**
**File:** `ModuleHost/FDP.Toolkit.Replication.Tests/Translators/GenericDescriptorTranslatorTests.cs` (CREATE)
**Requirements:**
- ✅ `PollIngress_GhostEntity_StashesData`: Verify data goes to `StashedData` and **NOT** to the component.
- ✅ `PollIngress_ActiveEntity_AppliesData`: Verify data goes directly to the component.

### Fix 2: Recorder Default Value
**File:** `ModuleHost/FDP/Fdp.Kernel/FlightRecorder/RecorderSystem.cs`

**Issue:** Default `MinRecordableId` is 0.
**Requirement:** Change default to `FdpConfig.SYSTEM_ID_RANGE` (65536) to safely exclude system entities by default.

---

## 🏗️ Part 2: Phase 3 Infrastructure (Demo Setup)

### Task 3: Recording Metadata Structure (FDP-DRP-008)
**Files:**
- `Fdp.Examples.NetworkDemo/Configuration/RecordingMetadata.cs`
- `Fdp.Examples.NetworkDemo/Configuration/MetadataManager.cs`

**Description:**
Define the sidecar file structure for recording metadata (`.fdp.meta`).

**Requirements:**
1.  `RecordingMetadata` class (Serializable): `MaxEntityId`, `Timestamp`, `NodeId`.
2.  `MetadataManager`: Static helper for JSON Save/Load.

**Tests:**
- ✅ `Metadata_SaveLoad_PreservesData`: Round-trip serialization test.

### Task 4: Demo Component Definitions (FDP-DRP-009)
**Files:**
- `Fdp.Examples.NetworkDemo/Components/DemoPosition.cs` (Internal logic position - RECORDED)
- `Fdp.Examples.NetworkDemo/Descriptors/GeoStateDescriptor.cs` (Network w/ DDS attributes)
- `Fdp.Examples.NetworkDemo/Components/TurretState.cs` (Auto-translated w/ FdpDescriptor)

**Description:**
Define the structs for the tank demo. `DemoPosition` drives `GeoStateDescriptor`. `TurretState` is auto-translated.

**Requirements:**
1.  `DemoPosition`: `Vector3 Value`.
2.  `GeoStateDescriptor`: `Lat`, `Lon`, `Alt`, `Heading`, `EntityId` (DdsKey).
3.  `TurretState`: `Yaw`, `Pitch`, `EntityId` (DdsKey). Add `[FdpDescriptor]`.

**Tests:**
- ✅ Verify attributes via reflection (ensure `FdpDescriptor` is present on `TurretState`).

### Task 5: Geographic Translator (FDP-DRP-010)
**Files:**
- `Fdp.Examples.NetworkDemo/Translators/GeodeticTranslator.cs`

**Description:**
Implement the manual translator that converts between `DemoPosition`/`NetworkPosition` (Cartesian) and `GeoStateDescriptor` (WGS84).

**Requirements:**
1.  Inject `IGeographicTransform` and `NetworkEntityMap`.
2.  `PollIngress`: `GeoStateDescriptor` -> `NetworkPosition` (Cartesian).
3.  `ScanAndPublish`: `NetworkPosition` -> `GeoStateDescriptor` (Geodetic).
4.  **Ownership Check:** `view.HasAuthority(entity, DescriptorOrdinal)` before publishing.

**Tests:**
- ✅ `GeodeticTranslator_RoundTrip`: Verify `Vector3` -> `Geo` -> `Vector3` preserves values (using `WGS84Transform`).

---

## 🧪 Testing Requirements

**Test Projects:**
- `ModuleHost/FDP.Toolkit.Replication.Tests/` (Fixes)
- `Fdp.Examples.NetworkDemo.Tests/` (Create if missing)

**Quality Standards:**
- **Zero Allocations:** Translator hot paths (`PollIngress`) should minimize allocations (reuse buffers where possible, though deserialization often allocates).
- **Correctness:** Verify the math in `GeodeticTranslator` using a known coordinate (e.g., Berlin origin).

---

## 📚 Reference Materials
- [TASK-DETAIL.md](../../docs/TASK-DETAIL.md)
- `Fdp.Modules.Geographic` - See `WGS84Transform` usage.
