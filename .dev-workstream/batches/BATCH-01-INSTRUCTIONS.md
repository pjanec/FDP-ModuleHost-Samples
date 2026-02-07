# BATCH-01: Logging & Test Infrastructure Foundation

**Batch Number:** BATCH-01  
**Tasks:** FDPLT-004, FDPLT-005, FDPLT-006, FDPLT-007, FDPLT-008, FDPLT-009, FDPLT-010, FDPLT-011, FDPLT-012  
**Phase:** Logging & Testing Foundation  
**Estimated Effort:** 8-10 hours  
**Priority:** HIGH  
**Dependencies:** None (Greenfield phase for testing)

---

## 📋 Onboarding & Workflow

### Developer Instructions
This batch establishes the critical infrastructure for distributed testing and high-performance logging. You will complete the logging integration started in previous work and build the test harness that allows running multiple nodes in parallel.

### Required Reading (IN ORDER)
1. **Onboarding Guide:** [`ONBOARDING.md`](../../ONBOARDING.md) - Review the new sections on Logging and Testing.
2. **Workflow Guide:** `.dev-workstream/guides/DEV-GUIDE.md` - Standard workflow.
3. **Design Document:** `docs/LOGGING-AND-TESTING-DESIGN.md` - Understand the architecture (Architecture sections 2.1 and 2.2).
4. **Task Details:** `docs/LOGGING-AND-TESTING-TASK-DETAILS.md` - Specific implementation steps.

### Source Code Location
- **Primary Work Area:** `Fdp.Examples.NetworkDemo/` and `ModuleHost/`
- **New Test Project:** `Fdp.Examples.NetworkDemo.Tests/` (To be created)

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-01-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/BATCH-01-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (Logging):** Implement Scope Context → Verify with manual run ✅
2. **Task 2 (Refactor):** Refactor NetworkDemoApp → Verify interactive mode still works ✅
3. **Task 3 (Test Infra):** Create Project & TestLogCapture → Unit Test Capture ✅
4. **Task 4 (Orchestrator):** Implement DistributedTestEnv → Unit Test Environment ✅
5. **Task 5 (Verification):** Implement Scope Verification Test (FDPLT-012) → Verify Node Isolation ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written (where applicable)
- ✅ **ALL tests passing**

---

## Context

We are extending the FDP Engine with distributed recording and playback capabilities. To validate this complex behavior (multi-node synchronization, time modes, ownership transfer), we need a robust testing framework that can simulate a distributed environment in-memory.

The logging foundation has been started (`FdpLog` and `LogSetup` exist), but is not fully integrated. The testing infrastructure does not exist yet.

**Related Tasks:**
- [FDPLT-004](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-004-add-scope-context-to-networkdemoapp) - Critical for identifying nodes in logs
- [FDPLT-008](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-008-refactor-networkdemoapp-for-testability) - Critical for deterministic testing
- [FDPLT-011](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-011-implement-distributedtestenv) - The test orchestrator

---

## 🎯 Batch Objectives
1. **Finalize Logging:** Ensure every log line identifies its source node via `AsyncLocal` scope.
2. **Clean Code:** Remove all `Console.WriteLine` calls from library code.
3. **Enable Testing:** Refactor the application entry point to allow headless, deterministic execution.
4. **Build Framework:** Create the `DistributedTestEnv` that can run multiple nodes in parallel tests.

---

## ✅ Tasks

### Task 1: Add Scope Context to NetworkDemoApp (FDPLT-004)

**File:** `Fdp.Examples.NetworkDemo/NetworkDemoApp.cs`  
**Task Definition:** See [FDPLT-004](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-004-add-scope-context-to-networkdemoapp)

**Description:**
Wrap the application execution in a logging scope to attach the `NodeId` to all log messages. This is the "magic" that allows us to separate logs from different nodes running in the same process.

**Requirements:**
- Wrap `Start()` logic in `using (ScopeContext.PushProperty("NodeId", nodeId))`
- Ensure this scope remains active for the entire duration of the app's life.
- Verify context flows across `await` calls.

**Verification:**
Run the demo with `--node 100`. The log file `logs/node_100.log` must exist and every line should have `|Node-100|`.

---

### Task 2: Refactor NetworkDemoApp for Testability (FDPLT-008)

**File:** `Fdp.Examples.NetworkDemo/NetworkDemoApp.cs`  
**Task Definition:** See [FDPLT-008](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-008-refactor-networkdemoapp-for-testability)

**Description:**
The current `Start()` method blocks until the app exits. This makes testing impossible. You must split initialization from execution.

**Requirements:**
- Rename `Start` → `InitializeAsync` (setup only, returns when ready).
- Add `Update(float dt)` for single-frame manual stepping.
- Add `RunLoopAsync(CancellationToken)` for the interactive game loop.
- Update `Program.cs` to call these new methods.

**Design Reference:**
See [Section 7.1 in Design Doc](../../docs/LOGGING-AND-TESTING-DESIGN.md#71-refactoring-networkdemoapp).

---

### Task 3: Complete Console.WriteLine Replacements (FDPLT-005, 006, 007)

**Files:**
- `ModuleHost.Network.Cyclone/CycloneNetworkModule.cs`
- `FDP.Toolkit.Replication/Translators/GenericDescriptorTranslator.cs`
- `ModuleHost.Network.Cyclone/Systems/CycloneNetworkEgressSystem.cs`
- `ModuleHost.Network.Cyclone/Systems/CycloneNetworkIngressSystem.cs`

**Task Definitions:** [FDPLT-005](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-005-replace-consolewriteline-in-cyclonenetworkmodule), [006](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-006-replace-consolewriteline-in-genericdescriptortranslator), [007](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-007-replace-consolewriteline-in-network-systems)

**Description:**
Replace all `Console.WriteLine` calls with `FdpLog<T>`.

**Requirements:**
- Use `Info` for lifecycle events.
- Use `Debug` or `Trace` for per-frame or per-packet logs.
- **CRITICAL:** Guard all `Debug`/`Trace` calls with `if (FdpLog<T>.IsDebugEnabled)` checks to prevent allocation in hot paths.
- Ensure `GenericDescriptorTranslator` logs detailed authority checks (trace level) to help diagnose "Remote: 0" issues.

---

### Task 4: Build Test Infrastructure (FDPLT-009, 010, 011)

**New Project:** `Fdp.Examples.NetworkDemo.Tests`  
**Task Definitions:** [FDPLT-009](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-009-create-test-project), [010](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-010-implement-testlogcapture), [011](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-011-implement-distributedtestenv)

**Description:**
Create the testing project and the harness for distributed tests.

**Requirements:**
1. **Project:** Create xUnit project, add NLog dependency.
2. **Capture:** Implement `TestLogCapture` (NLog target that stores logs in `ConcurrentQueue<string>`).
3. **Environment:** Implement `DistributedTestEnv`:
   - Can start Node A and Node B in separate Tasks.
   - Assigns distinct `NodeId` scopes to each.
   - Provides `WaitForCondition(predicate, timeout)` helper.
   - Provides `AssertLogContains(nodeId, message)` helper.

**Verification:**
This task builds the *framework* for testing. The actual verification that it works will be done in **Task 5**, where you write the `FDPLT-012` test case.

---

### Task 5: Verify Distributed Logging (FDPLT-012)

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/InfrastructureTests.cs`
**Task Definition:** See [FDPLT-012](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md#fdplt-012-test---asynclocal-scope-verification)

**Description:**
This is the acceptance test for the entire batch. You must verify that the logging system correctly marks logs from different instances.

**Requirements:**
- Create `Scenarios/InfrastructureTests.cs`.
- Implement `Logging_Scope_Flows_Through_Async_And_Tasks`.
- Start two tasks with different `NodeId` scopes.
- Verify logs from Task 1 *only* contain NodeId 100.
- Verify logs from Task 2 *only* contain NodeId 200.
- Verify context flows through `await` boundaries.

**Verification:**
Run `dotnet test`. This test MUST pass. It proves that our distributed testing strategy is viable.

---

## ⚠️ Quality Standards

**❗ CODE QUALITY EXPECTATIONS**
- **Zero Allocation:** Logging in hot paths (Egress/Ingress) MUST be guarded by `IsXxxEnabled`.
- **Async Correctness:** `ScopeContext` must be disposed properly (use `using`).
- **Thread Safety:** `TestLogCapture` uses `ConcurrentQueue`.

**❗ TEST QUALITY EXPECTATIONS**
- **Reliability:** `WaitForCondition` must handle variable timing (don't use `Thread.Sleep`).
- **Isolation:** Logs from Node A must NEVER appear in Node B's capture.

---

## 📊 Report Requirements

In your report (`.dev-workstream/reports/BATCH-01-REPORT.md`), please answer:

**Developer Insights:**
1. **Refactoring Risks:** Did splitting `Start()` in `NetworkDemoApp` reveal any initialization order dependencies?
2. **Log Volume:** With `Trace` enabled, how noisy are the logs? Do we need finer-grained filtering?
3. **Test Reliability:** Did the `InfrastructureTests` pass consistently? Did you have to adjust timeouts?

---

## 📚 Reference Materials
- **Task Specs:** [LOGGING-AND-TESTING-TASK-DETAILS.md](../../docs/LOGGING-AND-TESTING-TASK-DETAILS.md)
- **Design:** [LOGGING-AND-TESTING-DESIGN.md](../../docs/LOGGING-AND-TESTING-DESIGN.md)
- **Onboarding:** [ONBOARDING.md](../../ONBOARDING.md)
