# BATCH-06: Replay Tests & Final Cleanup

**Batch Number:** BATCH-06  
**Tasks:** CLEANUP-04, TEST-01  
**Phase:** Stabilization & Testing  
**Estimated Effort:** 2-4 hours  
**Priority:** HIGH  
**Dependencies:** BATCH-05

---

## 📋 Onboarding & Workflow

### Developer Instructions
You are finalizing the "Logging and Testing" workstream. Your primary goal is to **prove** the Replay feature works via automated testing, and to finish the code cleanup that was missed in the previous batch.

### Source Code Location
- **Tests:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/`
- **Toolkit:** `ModuleHost/FDP.Toolkit.Time/`
- **Kernel:** `ModuleHost/ModuleHost.Core/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-06-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW

1. **Task 1 (Cleanup):** Remove Console.WriteLine in Toolkit/Kernel (CLEANUP-04) → Verify Search ✅
2. **Task 2 (Test):** Implement `ReplayTests.cs` (TEST-01) → Verify Test Pass ✅

---

## ✅ Tasks

### Task 1: Complete Console Cleanup (CLEANUP-04)

**Files:**
- `ModuleHost/FDP.Toolkit.Time/Controllers/SteppedSlaveController.cs`
- `ModuleHost/ModuleHost.Core/ModuleHostKernel.cs`

**Description:**
The user is still seeing `Console.WriteLine` output. Review found occurrences in the files above.

**Requirements:**
- Replace `Console.WriteLine` and `Console.Error.WriteLine` with `FdpLog<T>` (or similar structured logging if `FdpLog` isn't available in Core - check references. If not available, use a delegate or event, or conditional compilation. **Note:** `Fdp.Kernel.Logging` might not be referenced in `ModuleHost.Core`. Check dependencies. If strict layering prevents it, use `System.Diagnostics.Debug.WriteLine` or a provided `ILogger` interface if available. Do **NOT** leave raw `Console.WriteLine`).
- *Self-Correction:* `ModuleHostKernel` receives `EventAccumulator` but maybe not a logger. **Check if FdpLog is available.** If not, standard practice in this engine seems to be removing the log or making it an event. **However**, `FDP.Kernel` seems to be referenced.
- **Specific target:** `[TimeController] Swapped to...` and `[Playback] Playing commands...` must go.

**Verification:**
Search for `Console.WriteLine` in `ModuleHost` directory.

---

### Task 2: Replay E2E Test (TEST-01)

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/ReplayTests.cs` (Create New)

**Description:**
Create a test that verifies recording and replay without running the full interactive app.

**Test Logic:**
1.  **Record:**
    - Initialize `NetworkDemoApp` in LIVE mode (`replayMode: false`) with a specific `recPath` (e.g., "TestRecording").
    - Run it for 10 frames (using `RunLoopAsync` with token or manual `Update`).
    - Move a tank or spawn an entity to ensure data changes.
    - Dispose the app (triggers Save).
2.  **Verify File:**
    - Assert that "TestRecording.fdp" and "TestRecording.fdp.meta" exist.
3.  **Replay:**
    - Initialize `NetworkDemoApp` in REPLAY mode (`replayMode: true`) pointing to "TestRecording".
    - Step through 10 frames.
    - **Assert:** The entity counts and positions match the recorded expectation (or at least that entities exist and move).
    - **Assert:** `ReplayTime` component is updated.

**Requirements:**
- Use `DistributedTestEnv` helpers if possible, or manually instantiate `NetworkDemoApp`.
- Ensure clean setup/teardown of files.

---

## ⚠️ Quality Standards

- **No Flaky Tests:** Ensure file paths are unique or cleaned up to avoid collisions.
- **No Console Noise:** The test output should be clean.
