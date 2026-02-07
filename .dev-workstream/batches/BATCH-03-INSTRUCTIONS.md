# BATCH-03: Fixes & Advanced Distributed Features

**Batch Number:** BATCH-03  
**Tasks:** FDPLT-017, FIX-01, FIX-02, FIX-03, FIX-04, FIX-05  
**Phase:** Stabilization & Advanced Features  
**Estimated Effort:** 4-6 hours  
**Priority:** HIGH  
**Dependencies:** BATCH-02 (Needs Fixes)

---

## 📋 Onboarding & Workflow

### Developer Instructions
Batch 02 was partially successful but left some technical debt and missed the advanced scenarios. This batch focuses on **stabilizing the test suite** (fixing assertions, version mismatch), **fixing critical regressions** (interactive demo broken), and **completing the advanced feature tests** (Deterministic Time / Replay).

### Required Reading (IN ORDER)
1. **Previous Review:** `.dev-workstream/reviews/BATCH-02-REVIEW.md` - **CRITICAL:** Read the specific issues found.
2. **Task Details:** `docs/LOGGING-AND-TESTING-TASK-DETAILS.md` - FDPLT-017 specs.

### Source Code Location
- **Test Project:** `Fdp.Examples.NetworkDemo.Tests/`
- **Application:** `Fdp.Examples.NetworkDemo/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-03-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

1. **Task 1 (Fix):** Downgrade to net8.0 → Verify build ✅
2. **Task 2 (Fix):** Fix Ghost Test Assertion → Verify Pass ✅
3. **Task 3 (Fix):** Enable Log Assertions → Verify Pass ✅
4. **Task 4 (Fix):** Restore Auto-Spawn (FIX-04) → Verify Manual Run ✅
5. **Task 5 (Fix):** Fix Translator Registration (FIX-05) → Verify Log Cleanliness ✅
6. **Task 6 (Feat):** Implement AdvancedTests.cs → Verify Pass ✅

---

## ✅ Tasks

### Task 1: Fix Project Version Mismatch (FIX-01)

**File:** `Fdp.Examples.NetworkDemo.Tests/Fdp.Examples.NetworkDemo.Tests.csproj`

**Description:**
The test project targets `net9.0`, but the main app targets `net8.0`. This causes runtime instability.
- Change `<TargetFramework>net9.0</TargetFramework>` to `net8.0`.
- Ensure all packages are compatible.

---

### Task 2: Strengthen Ghost Protocol Test (FIX-02)

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/GhostProtocolTests.cs`

**Description:**
The current test checks `!HasComponent<NetworkPosition>` to infer inactivity. This is an implementation detail.
- **Requirement:** Check `appB.World.GetLifecycleState(ghostB) == EntityLifecycle.Constructing`.
- This proves the ghost is logically incomplete, not just missing data.

---

### Task 3: Enable Log Assertions (FIX-03)

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/ReplicationTests.cs`

**Description:**
The log assertions are commented out!
```csharp
// env.AssertLogContains(100, "Published descriptor"); // UNCOMMENT THIS
```
- **Requirement:** Uncomment the assertions.
- **Action:** If they fail, look at the logs (printed to output) and find the *actual* message string (e.g., "Pub" or "Auth OK" or "Scan"). Update the test to match reality. **Do not disable the test.**

---

### Task 4: Restore Auto-Spawn (FIX-04)

**File:** `Fdp.Examples.NetworkDemo/NetworkDemoApp.cs`

**Description:**
The interactive demo is broken ("Local: 0, Remote: 0") because `SpawnLocalEntities` was commented out in `InitializeAsync`. The tests use a separate extension method `SpawnTank`, hiding this regression.

**Requirements:**
1.  Add `bool autoSpawn = true` parameter to `InitializeAsync`.
2.  Uncomment `SpawnLocalEntities` call, guarded by `if (!isReplay && autoSpawn)`.
3.  Update `DistributedTestEnv.cs` to pass `autoSpawn: false` when initializing nodes (so tests remain clean).
4.  Update `Program.cs` to pass `autoSpawn: true` (or rely on default).

**Verification:**
Run the demo interactively: `dotnet run --project Fdp.Examples.NetworkDemo -- 1 live`. You should see "Local: 1".

---

### Task 5: Fix OwnershipUpdateTranslator (FIX-05)

**File:** `Fdp.Examples.NetworkDemo/Translators/OwnershipUpdateTranslator.cs`

**Description:**
The logs show a warning: "Could not determine topic type for translator OwnershipUpdateTranslator. Skipping DDS entity creation."
This happens because `CycloneNetworkModule` uses reflection to find the topic type for manual translators, looking for a property named `DescriptorType`.

**Requirements:**
1.  Add public property: `public Type DescriptorType => typeof(ModuleHost.Network.Cyclone.Topics.OwnershipUpdate);`
2.  Ensure proper namespaces are imported.

**Verification:**
Run the interactive demo again. The WARN message should disappear, replaced by successful registration logs.

---

### Task 6: Implement Advanced Scenarios (FDPLT-017)

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/AdvancedTests.cs` (CREATE NEW)

**Description:**
Implement the missing Task 6 from Batch 02. Verify Deterministic Time Mode Switching.

**Test Scenario:**
1. Start Nodes A and B (Continuous Mode).
2. Node A triggers Switch (Input T).
3. Wait for `SteppedTimeController` active on both nodes.
4. Verify `Kernel.CurrentTime.FrameNumber` is identical on both nodes.

**Design Ref:**
See [LOGGING-AND-TESTING-DESIGN.md](../../docs/LOGGING-AND-TESTING-DESIGN.md#62-runtime-switching).

---

## ⚠️ Quality Standards

**❗ TEST QUALITY EXPECTATIONS**
- **Strict Assertions:** `Assert.Equal(Expected, Actual)` - no "approximate" checks unless float.
- **Log Proof:** Every test must assert at least one log message to prove the *reason* for the state change.

---

## 📊 Report Requirements

In your report:
1. **Log Messages:** What were the *actual* log messages you verified in Task 3?
2. **Time Sync:** Did the frame numbers match exactly in Task 6?

---

## 📚 Reference Materials
- **Design:** [LOGGING-AND-TESTING-DESIGN.md](../../docs/LOGGING-AND-TESTING-DESIGN.md)
