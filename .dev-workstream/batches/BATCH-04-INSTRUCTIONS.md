# BATCH-04: Critical Fixes & Network Stabilization

**Batch Number:** BATCH-04  
**Tasks:** CLEANUP-02, FIX-07, FDPLT-023 (Replay)  
**Phase:** Stabilization  
**Estimated Effort:** 4-6 hours  
**Priority:** CRITICAL  
**Dependencies:** BATCH-03 (Completed with Issues)

---

## 📋 Onboarding & Workflow

### Developer Instructions
Batch 03 delivered working Time Sync logic but left the codebase in a messy state with blocking I/O and persistent network errors. The interactive demo shows "Remote: 0", meaning nodes are not discovering each other.

**Your primary goal is to fix the broken network discovery and clean up the code.** Do not implement new "Advanced Features" (Radar/Damage) until the core network is stable.

### Required Reading
1. **Previous Review:** `.dev-workstream/reviews/BATCH-03-REVIEW.md` - Read the specific issues found.
2. **Logs:** See the user report showing `[ERROR] ... Error creating DDS entities for OwnershipUpdateTranslator`.

### Source Code Location
- **Application:** `Fdp.Examples.NetworkDemo/`
- **ModuleHost:** `ModuleHost.Network.Cyclone/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-04-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW

**CRITICAL: Complete strictly in this order.**

1. **Task 1 (Cleanup):** Remove Debug Hacks (CLEANUP-02) → Verify Clean Build ✅
2. **Task 2 (Fix):** Fix Translator Registration (FIX-07) → Verify No Error Logs ✅
3. **Task 3 (Verify):** Verify Network Discovery → **STOP if "Remote: 0" persists.** ✅
4. **Task 4 (Feat):** Distributed Replay (FDPLT-023) → Verify Composite Replay ✅

*Note: Radar and Damage Control modules are deferred until network is stable.*

---

## ✅ Tasks

### Task 1: Remove Debug Hacks (CLEANUP-02)

**Files:**
- `ModuleHost/FDP.Toolkit.Time/Controllers/DistributedTimeCoordinator.cs`
- `ModuleHost/FDP.Toolkit.Time/Controllers/SlaveTimeModeListener.cs`
- Entire Solution Search

**Description:**
The previous developer left `System.IO.File.WriteAllText("debug_master.txt", ...)` and `Console.WriteLine` in the codebase.
- **Requirement:** Remove ALL file I/O.
- **Requirement:** Replace ALL `Console.WriteLine` in Toolkit/Modules with `FdpLog`.

**Verification:**
Search codebase for "debug_master.txt" and "Console.WriteLine". Should be zero (except in `Program.cs`).

---

### Task 2: Fix Translator Registration (FIX-07)

**File:** `ModuleHost.Network.Cyclone/Modules/CycloneNetworkModule.cs`

**Description:**
Logs show: `[ERROR] ... Error creating DDS entities for OwnershipUpdateTranslator`.
This error likely aborts the translator registration loop or puts the module in a bad state, preventing subsequent translators from working or discovery from completing.

**Investigation:**
- The `OwnershipUpdateTranslator` was modified to add `DescriptorType`, but it might still be failing.
- Check if `TopicMsgs.OwnershipUpdate` struct has the `[DdsTopic]` attribute.
- Check if `CycloneNetworkModule` handles exceptions gracefully (it should log InnerException).

**Fix:**
- Ensure `TopicMsgs.OwnershipUpdate` is a valid DDS topic.
- Ensure `CycloneNetworkModule` prints the *full* exception details if it fails.
- Fix the registration so the error disappears.

---

### Task 3: Verify Network Discovery

**Action:**
Run two nodes interactively:
```powershell
# Terminal 1
dotnet run --project Fdp.Examples.NetworkDemo -- 100 live
# Terminal 2
dotnet run --project Fdp.Examples.NetworkDemo -- 200 live
```

**Success Criteria:**
- **No ERROR logs.**
- **Output:** `[STATUS] Local: 2, Remote: 2` (or similar, assuming entities are replicated).
- **Log:** `[CycloneIngress] Created ghost ...`

**STOP CONDITION:**
If you still see "Remote: 0" after 10 seconds, **DO NOT PROCEED to Task 4.** Debug the network discovery issue.

---

### Task 4: Distributed Replay (FDPLT-023)

**Description:**
Once discovery works, enable the "Composite Replay" scenario.

**Requirements:**
- Verify `ReplayBridgeSystem` correctly injects *only* owned components.
- Verify that injected components trigger `TransformSyncSystem` → `CycloneNetworkEgress`.

**Verification:**
1. Record a session on Node A (Drive tank).
2. Restart Node A in Replay mode, Node B in Live mode.
3. Verify Node B sees Node A's tank moving (received via network from A's replay).

---

## ⚠️ Quality Standards

**❗ CODE HYGIENE**
- No commented-out code.
- No "FIXME" comments left behind.
- No `Console.WriteLine` in library code.

---

## 📊 Report Requirements

In your report:
1. **Root Cause:** What exactly caused the `OwnershipUpdateTranslator` error?
2. **Discovery Status:** Did you achieve "Remote: > 0"?
3. **Replay Validation:** Did distributed replay work?

---

## 📚 Reference Materials
- **Design:** `docs/TANK-DESIGN.md`
