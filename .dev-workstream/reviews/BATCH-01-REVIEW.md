# BATCH-01 Review

**Batch:** BATCH-01  
**Reviewer:** Development Lead  
**Date:** 2026-02-06  
**Status:** ⚠️ NEEDS FIXES

---

## Summary
Kernel foundation (ID reservation, hydration) and basic replication infrastructure (attributes, policy) are implemented. However, critical tests for the Generic Translator are missing, and there is an API violation in the translator implementation.

---

## Issues Found

### Issue 1: Missing Tests for GenericDescriptorTranslator
**File:** `ModuleHost/FDP.Toolkit.Replication.Tests/`
**Problem:** `GhostProtocolTests.cs` verifies that *if* data is stashed, it gets promoted. However, there are **NO tests** verifying that `GenericDescriptorTranslator.PollIngress` actually stashes data when it encounters a Ghost.
**Why It Matters:** This is the core logic of the "Ghost Stash" pattern. If the translator applies data directly to a ghost (bypassing the stash), the ghost might activate prematurely with incomplete state.
**Fix:** Create `GenericDescriptorTranslatorTests.cs` and verify:
1.  `PollIngress` on Ghost Entity -> Data goes to `BinaryGhostStore` (Stash).
2.  `PollIngress` on Active Entity -> Data goes to Component (Apply).

### Issue 2: API Violation (Writing to Read-Only Component)
**File:** `GenericDescriptorTranslator.cs` (Line 42)
**Problem:**
```csharp
var store = view.GetManagedComponentRO<BinaryGhostStore>(entity);
store.StashedData[key] = buffer; // Modifying object obtained via RO API!
```
**Fix:** Use `GetManagedComponentRW` (or equivalent) when you intend to modify the component. Even if C# allows modifying reference types returned by RO, it violates the ECS contract and may bypass dirty tracking (though `BinaryGhostStore` might not need dirty tracking, it's bad practice).

### Issue 3: Incorrect Default Value
**File:** `RecorderSystem.cs` (Line 30)
**Problem:** `public int MinRecordableId { get; set; } = 0;`
**Fix:** Change default to `FdpConfig.SYSTEM_ID_RANGE` (65536) as specified. Users shouldn't have to manually configure safety defaults.

---

## Verdict
**Status:** NEEDS FIXES

**Required Actions:**
1.  Create `BATCH-01.1` to address the missing tests and API violation.
2.  Ensure `GenericDescriptorTranslator` is robustly tested before proceeding to Phase 3.
