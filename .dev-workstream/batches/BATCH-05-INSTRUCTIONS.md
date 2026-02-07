# BATCH-05: Final Polish & Advanced Modules

**Batch Number:** BATCH-05  
**Tasks:** CLEANUP-03, REFACTOR-01, FDPLT-021, FDPLT-022  
**Phase:** Polish & Advanced Features  
**Estimated Effort:** 4-6 hours  
**Priority:** HIGH  
**Dependencies:** BATCH-04 (Functional but messy)

---

## 📋 Onboarding & Workflow

### Developer Instructions
Batch 04 proved the distributed features work (Time Sync, Replay), but left the codebase in a "debug state" with hardcoded file paths and console spam. Your first job is to **sanitize the codebase**. Once clean, you will implement the final two demonstration modules (Radar and Damage Control).

### Required Reading
1. **Previous Review:** `.dev-workstream/reviews/BATCH-04-REVIEW.md` - Read about the specific debug debris found.
2. **Task Details:** `docs/LOGGING-AND-TESTING-TASK-DETAILS.md` (You will implement FDPLT-021/022).

### Source Code Location
- **Application:** `Fdp.Examples.NetworkDemo/`
- **Toolkit:** `ModuleHost/FDP.Toolkit.Time/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-05-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW

1. **Task 1 (Cleanup):** Remove File I/O Hacks (CLEANUP-03) → Verify Clean Search ✅
2. **Task 2 (Refactor):** Centralize Registration (REFACTOR-01) → Verify Replay Compatibility ✅
3. **Task 3 (Feat):** Radar Module (FDPLT-021) → Verify Async Logic ✅
4. **Task 4 (Feat):** Damage Control (FDPLT-022) → Verify Reactive Logic ✅

---

## ✅ Tasks

### Task 1: Remove Debug Debris (CLEANUP-03)

**Files:**
- `ModuleHost/FDP.Toolkit.Time/Controllers/*.cs` (DistributedTimeCoordinator)
- `ModuleHost/FDP.Toolkit.Replication/ReplicationBootstrap.cs`

**Description:**
The codebase contains:
1. `System.IO.File.AppendAllText(...)` with hardcoded paths. **MUST REMOVE.**
2. `Console.WriteLine` in `ReplicationBootstrap` (logs "Auto-registered...").

**Requirements:**
- Delete all `System.IO.File` calls.
- Replace `Console.WriteLine` in `ReplicationBootstrap` with `FdpLog<ReplicationBootstrap>.Info`.

**Verification:**
Search solution for `System.IO.File`. Should be zero.

---

### Task 2: Centralize Component Registration (REFACTOR-01)

**Files:**
- `Fdp.Examples.NetworkDemo/Configuration/DemoComponentRegistry.cs`
- `Fdp.Examples.NetworkDemo/NetworkDemoApp.cs`

**Description:**
`NetworkDemoApp` manually registers `TimeModeComponent` and `FrameAckComponent`, duplicating logic that belongs in `DemoComponentRegistry`. This risks ID drift if the registration order changes between Live/Replay/Test modes.

**Requirements:**
1. Move `World.RegisterComponent<TimeModeComponent>()` and `FrameAckComponent` into `DemoComponentRegistry.Register()`.
2. Remove manual registration from `NetworkDemoApp.InitializeAsync`.
3. Verify `ReplayBridgeSystem` manually registers components in the correct order (or calls Registry). *Self-correction: ReplayBridge usually reads metadata, but for internal components not in recording, it needs manual setup. Ensure consistency.*

**Verification:**
Run a recording/replay cycle. If Component IDs shifted, replay will crash or show garbage.

---

### Task 3: Radar Module (FDPLT-021)

**File:** `Fdp.Examples.NetworkDemo/Modules/RadarModule.cs`

**Description:**
Implement a module that runs in `SlowBackground` mode (1Hz). It should scan the `Snapshot` for entities within range and publish `RadarContactEvent`.

**Requirements:**
- Use `[ExecutionPolicy(ExecutionMode.SlowBackground, priority: 1)]`.
- Use `[SnapshotPolicy(SnapshotMode.OnDemand)]`.
- Query the *snapshot* (safe for background), not the live view.
- Publish events to the EventBus.

---

### Task 4: Damage Control Module (FDPLT-022)

**File:** `Fdp.Examples.NetworkDemo/Modules/DamageControlModule.cs`

**Description:**
Implement a reactive module that only wakes up when `DetonationEvent` occurs.

**Requirements:**
- Use `[WatchEvents(typeof(DetonationEvent))]`.
- Apply damage to the target entity's `Health` component.
- Ensure it does *not* run every frame.

---

## ⚠️ Quality Standards

**❗ NO DEBUG LEFTOVERS**
- **Zero Tolerance:** If I see `Console.WriteLine` or `File.Write` in the next review, the batch will be rejected immediately.

---

## 📊 Report Requirements

In your report:
1. **Cleanup Confirmation:** List the files you cleaned up.
2. **Registration Safety:** How did you ensure Component IDs remained stable?

---

## 📚 Reference Materials
- **Design:** `docs/TANK-DESIGN.md` Section 8.
