# BATCH-01: Kernel Foundation & Replication Toolkit

**Batch Number:** BATCH-01  
**Tasks:** FDP-DRP-001 through FDP-DRP-007  
**Phase:** Phase 1 (Kernel) & Phase 2 (Replication)  
**Estimated Effort:** 12-16 hours  
**Priority:** CRITICAL  
**Dependencies:** None

---

## 📋 Onboarding & Workflow

### Developer Instructions
This batch combines the **Kernel Foundation** (Phase 1) and **Replication Toolkit** (Phase 2). This provides a complete infrastructure layer: enabling safe replay ID management AND zero-boilerplate networking.

### Required Reading (IN ORDER)
1. **Project Onboarding:** [`ONBOARDING.md`](../../ONBOARDING.md) - **READ THIS FIRST** to understand the Shadow World concept and ID partitioning strategy.
2. **Workflow Guide:** `.dev-workstream/README.md` - How to work with batches.
3. **Task Definitions:** [`docs/TASK-DETAIL.md`](../../docs/TASK-DETAIL.md) - Detailed specs for tasks 001-007.
4. **Design Document:** [`docs/DESIGN.md`](../../docs/DESIGN.md) - Section 3.1 (ID Management), 4.2 (Shadow World), and 7 (Zero Boilerplate).

### Source Code Location
- **Kernel Core:** `ModuleHost/FDP/Fdp.Kernel/`
- **Replication Toolkit:** `ModuleHost/FDP.Toolkit.Replication/`
- **Interfaces:** `ModuleHost/FDP.Interfaces/`
- **Tests:** `ModuleHost/FDP/Fdp.Tests/` and `ModuleHost/FDP.Toolkit.Replication.Tests/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-01-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/BATCH-01-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Kernel Tasks (001-003):** Implement → Write tests → **ALL tests pass** ✅
2. **Toolkit Foundation (004-005):** Implement → Write tests → **ALL tests pass** ✅
3. **Generic Translator (006):** Implement → Write tests → **ALL tests pass** ✅
4. **Auto-Registration (007):** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including previous tasks)

---

## 🎯 Batch Objectives
1. **ID Partitioning:** Prevents collisions between "System" entities and "Recorded" entities.
2. **Deterministic Hydration:** Allows the replay system to force-create entities with specific IDs.
3. **Selective Recording:** Prevents local system entities and transient network buffers from polluting the recording.
4. **Zero-Boilerplate Networking:** Enables networking for components just by adding attributes, using a generic translator.

---

## ✅ Part 1: Kernel Foundation

### Task 1: Entity Index ID Reservation (FDP-DRP-001)

**Files:**
- `ModuleHost/FDP/Fdp.Kernel/FdpConfig.cs` (UPDATE)
- `ModuleHost/FDP/Fdp.Kernel/EntityIndex.cs` (UPDATE)
- `ModuleHost/FDP/Fdp.Kernel/EntityRepository.cs` (UPDATE)

**Description:**
Implement the ability to reserve a range of IDs at the start of the entity index.

**Requirements:**
1. Define `public const int SYSTEM_ID_RANGE = 65536;` in `FdpConfig.cs`.
2. Implement `ReserveIdRange(int maxId)` in `EntityIndex.cs` (thread-safe).
3. Expose via `EntityRepository.ReserveIdRange`.

**Tests:**
- ✅ `ReserveIdRange_PreventsCollision`
- ✅ `ReserveIdRange_MultipleCalls`

---

### Task 2: Entity Hydration for Replay (FDP-DRP-002)

**Files:**
- `ModuleHost/FDP/Fdp.Kernel/EntityRepository.cs` (UPDATE)

**Description:**
Implement `HydrateEntity(int id, int generation)` to force-create entities at specific slots during replay.

**Requirements:**
1. Verify ID is within reserved range OR extend reservation.
2. Set entity generation to exactly match request.
3. Mark entity as active and emit lifecycle event.

**Tests:**
- ✅ `HydrateEntity_CreatesAtSpecificId`
- ✅ `HydrateEntity_EmitsLifecycleEvent`

---

### Task 3: Recorder Minimum ID Filter (FDP-DRP-003)

**Files:**
- `ModuleHost/FDP/Fdp.Kernel/FlightRecorder/RecorderSystem.cs` (UPDATE)

**Description:**
Update `RecorderSystem` to ignore entities below `MinRecordableId` (default 65536).

**Requirements:**
1. Add `MinRecordableId` property.
2. Modify `RecordDeltaFrame` to skip chunks/entities below this ID.

**Tests:**
- ✅ `RecorderSystem_SkipsSystemRange`

---

## ✅ Part 2: Replication Toolkit

### Task 4: Data Policy Enforcement (FDP-DRP-004)

**Files:**
- `ModuleHost/FDP.Toolkit.Replication/Components/NetworkPosition.cs` (UPDATE)
- `ModuleHost/FDP.Toolkit.Replication/Components/NetworkVelocity.cs` (UPDATE)
- `ModuleHost/FDP.Toolkit.Replication/Components/NetworkAuthority.cs` (VERIFY)
- `ModuleHost/FDP.Toolkit.Replication/Components/NetworkIdentity.cs` (VERIFY)

**Description:**
Mark buffer components with `[DataPolicy(DataPolicy.NoRecord)]` to prevent them from being recorded. Authority components MUST remain recordable.

**Requirements:**
1. Add attribute to `NetworkPosition`, `NetworkVelocity`.
2. Ensure `NetworkIdentity`, `NetworkAuthority`, `DescriptorOwnership` do NOT have this attribute.

**Tests:**
- ✅ `NetworkComponents_NotRecordable`: Verify `IsRecordable` returns false for buffers.

---

### Task 5: FdpDescriptor Attribute (FDP-DRP-005)

**Files:**
- `ModuleHost/FDP.Interfaces/Attributes/FdpDescriptorAttribute.cs` (CREATE)

**Description:**
Create the attribute used to mark structs for automatic translator generation.

**Requirements:**
1. Properties: `Ordinal` (int), `TopicName` (string), `IsMandatory` (bool).
2. AttributeUsage: Structs only.

**Tests:**
- ✅ `FdpDescriptorAttribute_ReflectionAccessible`

---

### Task 6: Generic Descriptor Translator (FDP-DRP-006)

**Files:**
- `ModuleHost/FDP.Toolkit.Replication/Translators/GenericDescriptorTranslator.cs` (CREATE)

**Description:**
Implement the generic translator that maps ECS components to descriptors 1:1. Includes **Ghost Stash** logic.

**Requirements:**
1. Generic class `GenericDescriptorTranslator<T>`.
2. `PollIngress`:
   - If entity has `BinaryGhostStore` (is a ghost) -> Serialize & Stash data (DO NOT apply).
   - If active entity -> Apply component directly.
3. `ScanAndPublish`:
   - Query entities with `T`, `NetworkIdentity`, `NetworkAuthority`.
   - Check `HasAuthority(descriptorOrdinal)`.
   - Publish if owned.

**Design Reference:** [DESIGN.md](../../docs/DESIGN.md) § 7.2

**Tests:**
- ✅ `GenericTranslator_Ingress_StashesForGhost`
- ✅ `GenericTranslator_Ingress_AppliesForActive`
- ✅ `GenericTranslator_Egress_RespectsAuthority`

---

### Task 7: Assembly Scanning (FDP-DRP-007)

**Files:**
- `ModuleHost/FDP.Toolkit.Replication/ReplicationBootstrap.cs` (CREATE/UPDATE)

**Description:**
Implement reflection scanner to find `[FdpDescriptor]` types and create translators.

**Requirements:**
1. `CreateAutoTranslators(Assembly assembly, ...)`
2. Scan for value types with attribute.
3. Instantiate `GenericDescriptorTranslator<T>` for each.
4. Create appropriate serializer for each.

**Tests:**
- ✅ `Bootstrap_CreatesTranslatorsForAttributedTypes`

---

## 🧪 Testing Requirements

**Test Projects:**
- `ModuleHost/FDP/Fdp.Tests/` (Kernel tasks)
- `ModuleHost/FDP.Toolkit.Replication.Tests/` (Toolkit tasks)

**Quality Standards:**
- **Zero Allocations:** `ReserveIdRange` and `HydrateEntity` must be allocation-free.
- **Test Quality:** Do not use `Assert.Contains` for generated code checks. Compile or use reflection to verify behavior.
- **Ghost Protocol:** Task 6 tests must explicitly verify the Ghost Stash behavior (data buffering) vs immediate application.

---

## ⚠️ Common Pitfalls to Avoid
- **Partial Ownership:** In Task 6, ensure `ScanAndPublish` checks `HasAuthority` for the *specific descriptor ordinal*, not just general authority.
- **Ghost Stashing:** Failing to stash data for ghosts will cause them to spawn with incomplete state or trigger premature activation.
- **Attribute Targets:** Ensure `FdpDescriptor` is only valid on structs (value types), as FDP components must be unmanaged/blittable.

---

## 📚 Reference Materials
- [TASK-DETAIL.md](../../docs/TASK-DETAIL.md) - Tasks 001-007
- [DESIGN.md](../../docs/DESIGN.md) - Section 7 (Zero Boilerplate Networking)
