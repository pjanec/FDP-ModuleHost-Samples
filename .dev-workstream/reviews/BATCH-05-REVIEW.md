# BATCH-05 Review

**Batch:** BATCH-05  
**Reviewer:** Development Lead  
**Date:** 2026-02-07  
**Status:** ⚠️ PARTIALLY COMPLETE (Cleanup incomplete, Missing Tests)

---

## Summary

The developer successfully implemented the `RadarModule` and `DamageControlModule` and refactored the component registration.
However, two major issues remain:
1.  **Cleanup was incomplete:** While `System.IO.File` is gone from source, `Console.WriteLine` persists in `FDP.Toolkit.Time` and `ModuleHost.Core`. The user explicitly complained about this.
2.  **No Replay Tests:** The user correctly identified that there are **NO E2E tests for the Replay feature**. Running the full interactive demo to test replay is inefficient and manual. We need automated verification.

---

## Issues Found

### Issue 1: Console.WriteLine in Toolkit & Kernel
**Files:**
- `ModuleHost/FDP.Toolkit.Time/Controllers/SteppedSlaveController.cs` (Line 75)
- `ModuleHost/ModuleHost.Core/ModuleHostKernel.cs` (Multiple lines: 302, 515, 636, 712, 827...)
**Problem:** The instructions said "Replace ALL Console.WriteLine in Toolkit/Modules". The developer missed these files. `ModuleHostKernel` is especially noisy with "Swapped to..." and "Playing commands..." logs.
**Fix:** Replace with `FdpLog<T>` or `Console.Error` (if appropriate for kernel panic, but `FdpLog` is preferred for consistency).

### Issue 2: Missing Replay Verification
**Problem:** There is no `ReplayTests.cs`.
**Impact:** We cannot verify FDPLT-023 (Distributed Replay) automatically.
**Requirement:** Create `Fdp.Examples.NetworkDemo.Tests/Scenarios/ReplayTests.cs` that:
1.  Starts a node in RECORD mode.
2.  Simulates a few frames (moving a tank).
3.  Stops the node (saving metadata).
4.  Starts a node in REPLAY mode.
5.  Steps through frames and asserts the entity positions match the recording.

---

## Verdict

**Status:** **NEEDS FIXES**

The feature work (Radar/Damage) is good, but the task is not "Done" until the cleanup is thorough and the features are tested.

**Corrective Actions for BATCH-06:**
1.  **CLEANUP-04:** Finish `Console.WriteLine` removal in `SteppedSlaveController` and `ModuleHostKernel`.
2.  **TEST-01:** Implement `ReplayTests.cs`.

---

## 📝 Commit Message

```
feat: radar and damage modules (BATCH-05)

- Implemented RadarModule (SlowBackground, Snapshot-on-Demand)
- Implemented DamageControlModule (Reactive, WatchEvents)
- Refactored component registration to DemoComponentRegistry
- Removed System.IO.File debug hacks

Known Issues:
- Console.WriteLine remains in SteppedSlaveController and ModuleHostKernel
- Missing automated tests for Replay feature
```
