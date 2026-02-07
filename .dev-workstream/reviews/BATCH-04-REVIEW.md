# BATCH-04 Review

**Batch:** BATCH-04  
**Reviewer:** Development Lead  
**Date:** 2026-02-07  
**Status:** ⚠️ NEEDS FIXES (Proceed to BATCH-05 with strict cleanup)

---

## Summary

The developer claims to have fixed the network discovery and replay issues in Batch 04. 
- **Auto-Spawn Restored:** The interactive demo now correctly shows "Local: 2" (Node 100 + TimeSync Entity).
- **Time Sync Verified:** `AdvancedTests.cs` confirms the barrier protocol works.
- **Replay Fixed:** The report mentions fixing a "Component ID 22" crash and Authority mismatch.

**HOWEVER, critical issues remain:**
1.  **Debug Debris:** The developer explicitly ignored the instruction to remove file I/O debug hacks. I found `System.IO.File.AppendAllText(@"d:\Work\FDP-ModuleHost-Samples\MASTER_TRACE.txt"...` in the code. This is unacceptable.
2.  **Console Logs:** The user reports the demo still outputs raw Console logs (e.g., `[Replication] Auto-registered: ...`). This violates the "Clean Output" requirement.
3.  **Duplicate Registration:** `DemoComponentRegistry` has overlapping registrations with `NetworkDemoApp`, creating fragility.

---

## Issues Found

### Issue 1: Blocking File I/O in Production Code
**File:** `ModuleHost/FDP.Toolkit.Time/Controllers/DistributedTimeCoordinator.cs` (likely, based on user report)
**Problem:** `System.IO.File.AppendAllText` with a hardcoded absolute path (`d:\Work\...`).
**Severity:** **CRITICAL**. This crashes on any other machine and kills performance.
**Fix:** Must be removed immediately.

### Issue 2: Console.WriteLine Persists
**File:** `NetworkDemoApp.cs` (likely library code called from it)
**Log:** `[Replication] Auto-registered: TurretState ...`
**Problem:** This log comes from `ReplicationBootstrap.cs` which uses `Console.WriteLine`. The developer missed replacing it in the library code.
**Fix:** Replace with `FdpLog` in `ReplicationBootstrap`.

### Issue 3: Duplicate Component Registration
**File:** `DemoComponentRegistry.cs` vs `NetworkDemoApp.cs`
**Problem:** `NetworkDemoApp.InitializeAsync` calls `DemoComponentRegistry.Register(World)` BUT also has manual `World.RegisterComponent` calls for `TimeModeComponent` and `FrameAckComponent`.
**Risk:** If registration order changes, Component IDs shift, breaking binary compatibility with recordings.
**Fix:** Move ALL registration into `DemoComponentRegistry`.

---

## Test Quality Assessment

**AdvancedTests.cs:**
- ✅ Correctly triggers mode switch via EventBus.
- ✅ Verifies controller type swap.
- ✅ Asserts frame synchronization (`Math.Abs <= 2`).
- **Verdict:** Good test coverage for the feature.

---

## Verdict

**Status:** **NEEDS FIXES**

The core functionality works, but the code quality is not shipping-grade due to debug debris.

**Corrective Actions for BATCH-05:**
1.  **CLEANUP-03:** Search and destroy `System.IO.File` and `Console.WriteLine` in ALL source files.
2.  **REFACTOR-01:** Centralize component registration in `DemoComponentRegistry`.
3.  **FDPLT-021/022:** Only *after* cleanup, proceed to Radar/Damage modules.

---

## 📝 Commit Message

```
feat: distributed replay and time sync fixes (BATCH-04)

Completes FDPLT-023, FIX-07, FDPLT-017

- Implemented AdvancedTests for Deterministic Time switching
- Fixed ReplayBridgeSystem to handle missing infrastructure components
- Fixed Authority mismatch in Replay mode
- Restored network discovery (Remote > 0)

Known Issues:
- Debug file I/O left in Toolkit (scheduled for BATCH-05 cleanup)
- Duplicate component registration (scheduled for BATCH-05 cleanup)
```
