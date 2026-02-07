# BATCH-04: Advanced Demo Modules & Polish

**Batch Number:** BATCH-04  
**Tasks:** FDPLT-021, FDPLT-022, FDPLT-023  
**Phase:** Advanced Features & Polish  
**Estimated Effort:** 6-8 hours  
**Priority:** MEDIUM  
**Dependencies:** BATCH-03 (Fixes & Core Features)

---

## 📋 Onboarding & Workflow

### Developer Instructions
With the core distributed system stable and tested (BATCH-03), we now move to implementing the "Advanced Demo Features" defined in the original design. These modules showcase the engine's capabilities in handling async/background tasks and reactive event-driven logic.

### Required Reading
1. **Design Document:** `docs/TANK-DESIGN.md` - Section 8 (Advanced Demo Features).
2. **Task Details:** `docs/LOGGING-AND-TESTING-TASK-DETAILS.md` - (You will create new details for these tasks).

### Source Code Location
- **Application:** `Fdp.Examples.NetworkDemo/Modules/` and `Systems/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-04-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW

1. **Task 1 (Feat):** Implement Radar Module (FDPLT-021) → Verify Async Logic ✅
2. **Task 2 (Feat):** Implement Damage Control (FDPLT-022) → Verify Reactive Logic ✅
3. **Task 3 (Feat):** Implement Distributed Replay (FDPLT-023) → Verify Replay Network Egress ✅

---

## ✅ Tasks

### Task 1: Radar Module (FDPLT-021)

**File:** `Fdp.Examples.NetworkDemo/Modules/RadarModule.cs`

**Description:**
Implement a module that runs in `SlowBackground` mode (1Hz). It should scan the `Snapshot` for entities within range and publish `RadarContactEvent`.

**Requirements:**
- Use `[ExecutionPolicy(ExecutionMode.SlowBackground, priority: 1)]`.
- Use `[SnapshotPolicy(SnapshotMode.OnDemand)]`.
- Query the *snapshot* (safe for background), not the live view.
- Publish events to the EventBus.

**Verification:**
- Add a test `Modules/AdvancedModulesTests.cs`.
- Run simulation for 2 seconds.
- Assert `RadarContactEvent` was published.

---

### Task 2: Damage Control Module (FDPLT-022)

**File:** `Fdp.Examples.NetworkDemo/Modules/DamageControlModule.cs`

**Description:**
Implement a reactive module that only wakes up when `DetonationEvent` occurs.

**Requirements:**
- Use `[WatchEvents(typeof(DetonationEvent))]`.
- Apply damage to the target entity's `Health` component.
- Ensure it does *not* run every frame (add a log to verify execution count).

**Verification:**
- In `AdvancedModulesTests.cs`, publish a `DetonationEvent`.
- Verify `Health` component decreases.
- Verify module `Execute` was called exactly once.

---

### Task 3: Distributed Replay (FDPLT-023)

**Description:**
Enable the "Composite Replay" scenario where each node replays its own recording while receiving the other's recording over the network.

**Requirements:**
- Ensure `ReplayBridgeSystem` correctly injects *only* owned components (using `NetworkAuthority`).
- Verify that injected components trigger `TransformSyncSystem` → `CycloneNetworkEgress`.
- This proves "Replay is just another input source".

**Verification:**
- Manual Test: Record a session on Node A.
- Restart Node A in Replay mode, Node B in Live mode.
- Verify Node B sees Node A's tank moving (received via network from A's replay).

---

## ⚠️ Quality Standards

**❗ PERFORMANCE EXPECTATIONS**
- **Radar:** Must NOT block the main thread. Use `Thread.Sleep` in the background task to simulate load during testing, ensuring FPS stays high.
- **Damage:** Must be zero-cost when no events are present.

---

## 📊 Report Requirements

In your report:
1. **Radar Performance:** Did the background task cause any frame spikes?
2. **Replay Fidelity:** Did the distributed replay look identical to the live session?

---

## 📚 Reference Materials
- **Design:** `docs/TANK-DESIGN.md` Section 8.
