# BATCH-02: Distributed Feature Verification

**Batch Number:** BATCH-02  
**Tasks:** FDPLT-013, FDPLT-014, FDPLT-015, FDPLT-016, FDPLT-017, CLEANUP-01  
**Phase:** Distributed Test Cases  
**Estimated Effort:** 10-12 hours  
**Priority:** HIGH  
**Dependencies:** BATCH-01 (Infrastructure)

---

## 📋 Onboarding & Workflow

### Developer Instructions
Now that the logging and testing infrastructure is in place (BATCH-01), your goal is to verify that the distributed system actually works. You will implement the core test cases defined in the design document, proving that replication, lifecycle management, and ownership transfer function correctly across nodes.

**You also have a cleanup task:** The previous batch left some `Console.WriteLine` calls in `NetworkDemoApp` and library code. You must replace these to ensure clean test output.

### Required Reading (IN ORDER)
1. **Design Document:** `docs/LOGGING-AND-TESTING-DESIGN.md` - Review "Test Case Specifications" (Section 6).
2. **Task Details:** `docs/LOGGING-AND-TESTING-TASK-DETAILS.md` - Implementation details for FDPLT-013 through FDPLT-017.
3. **Previous Review:** `.dev-workstream/reviews/BATCH-01-REVIEW.md` - See "Issues Found" regarding Console logs.

### Source Code Location
- **Test Project:** `Fdp.Examples.NetworkDemo.Tests/`
- **Application:** `Fdp.Examples.NetworkDemo/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-02-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests.**

1. **Task 1 (Cleanup):** Remove Console.WriteLine → Verify clean output ✅
2. **Task 2 (Replication):** Test Basic Replication (FDPLT-013) → Pass ✅
3. **Task 3 (Lifecycle):** Test Entity Lifecycle (FDPLT-014) → Pass ✅
4. **Task 4 (Orphan):** Test Orphan Protection (FDPLT-015) → Pass ✅
5. **Task 5 (Ownership):** Test Partial Ownership (FDPLT-016) → Pass ✅
6. **Task 6 (Advanced):** Test Additional Cases (FDPLT-017) → Pass ✅

**DO NOT** move to the next task until the current test passes reliably.

---

## Context

We have a distributed tank game where Node A drives and Node B shoots. We need to prove this works without running two manual terminals. The `DistributedTestEnv` built in BATCH-01 allows us to run both nodes in memory.

**Related Tasks:**
- [FDPLT-013](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-013-test---basic-entity-replication) - Does an entity appear on the other node?
- [FDPLT-015](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-015-test---orphan-protection) - Do we correctly wait for mandatory data?
- [FDPLT-016](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-016-test---partial-ownership) - Can two nodes control one entity?

---

## 🎯 Batch Objectives
1. **Clean Output:** Ensure tests produce only structured NLog output (no Console noise).
2. **Verify Replication:** Prove entities replicate across the in-memory network.
3. **Verify Protocols:** Prove Ghost/Orphan/Ownership protocols work as designed.
4. **Reliability:** Ensure tests are not flaky (use `WaitForCondition` correctly).

---

## ✅ Tasks

### Task 1: Cleanup Console Logs (CLEANUP-01)

**Files:**
- `Fdp.Examples.NetworkDemo/NetworkDemoApp.cs`
- `ModuleHost.Network.Cyclone/Services/DdsWrappers.cs`
- `ModuleHost/FDP.Toolkit.Replication/Systems/GhostPromotionSystem.cs`

**Description:**
Replace all remaining `Console.WriteLine` calls with `FdpLog<T>`.
- In `NetworkDemoApp`, replace initialization/status banners with `Info` logs.
- In `DdsWrappers`, replace error print with `Error` log.
- In `GhostPromotionSystem`, replace warning with `Warn` log.

**Verification:**
Run `dotnet test`. The output window should NOT show raw console text from the app, only the test runner output.

---

### Task 2: Test Basic Replication (FDPLT-013)

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/ReplicationTests.cs`  
**Task Definition:** [FDPLT-013](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-013-test---basic-entity-replication)

**Description:**
Verify that when Node A spawns a tank, Node B sees it.
- Use `env.NodeA.SpawnTank()`
- Wait for `env.NodeB.TryGetEntityByNetId`
- Assert logs: "Published descriptor" (A) and "Created ghost" (B)

---

### Task 3: Test Entity Lifecycle (FDPLT-014)

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/LifecycleTests.cs`  
**Task Definition:** [FDPLT-014](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-014-test---entity-lifecycle)

**Description:**
Verify the full cycle: Create → Ghost Detect → Activate → Destroy → Ghost Cleanup.
- Key check: Ghost should transition `Constructing` → `Active` only when data arrives.
- Key check: Destruction on A causes removal on B.

---

### Task 4: Test Orphan Protection (FDPLT-015)

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/GhostProtocolTests.cs`  
**Task Definition:** [FDPLT-015](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-015-test---orphan-protection)

**Description:**
Verify that if mandatory data (Chassis) is missing, the ghost **never activates**.
- You may need to add a filter mechanism to `CycloneEgressSystem` or `GenericDescriptorTranslator` to artificially drop packets for this test.
- Or, configure Node A to not have authority over Chassis for this specific test entity.

---

### Task 5: Test Partial Ownership (FDPLT-016)

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/OwnershipTests.cs`  
**Task Definition:** [FDPLT-016](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-016-test---partial-ownership)

**Description:**
The "Composite Tank" scenario.
- Node A owns Chassis. Node B owns Turret.
- Verify A's position updates reach B.
- Verify B's turret updates reach A.
- Verify NO overwrites (A doesn't overwrite B's turret with stale data).

---

### Task 6: Advanced Scenarios (FDPLT-017)

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/AdvancedTests.cs`  
**Task Definition:** [FDPLT-017](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-017-additional-test-cases)

**Description:**
Implement at least one advanced case:
1. **Deterministic Time Switch:** Switch both nodes to deterministic mode and verify frame counts match.
2. **Distributed Replay:** (Optional/Bonus) Verify replay triggers network egress.

---

## ⚠️ Quality Standards

**❗ TEST QUALITY EXPECTATIONS**
- **No Sleep:** Never use `Thread.Sleep`. Always use `WaitForCondition`.
- **Log Verification:** Every test MUST verify logs confirm *why* something happened (e.g., "Created ghost"), not just that the state changed.
- **Cleanup:** Ensure `env.Dispose()` is called (use `using` block).

---

## 📊 Report Requirements

In your report:
1. **Pass Rate:** Confirm all implemented tests pass.
2. **Timing:** Average execution time for the full suite.
3. **Orphan Test Strategy:** Explain how you simulated the missing data for Task 4.

---

## 📚 Reference Materials
- **Task Specs:** [LOGGING-AND-TESTING-TASK-DETAILS.md](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md)
- **Test Env Code:** `Fdp.Examples.NetworkDemo.Tests/Infrastructure/DistributedTestEnv.cs`
