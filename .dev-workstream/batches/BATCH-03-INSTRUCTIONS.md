# BATCH-03: Systems & Integration

**Batch Number:** BATCH-03  
**Tasks:** FDP-DRP-011, FDP-DRP-012, FDP-DRP-013, FDP-DRP-014, FDP-DRP-015  
**Phase:** Phase 4 (Systems) & Phase 5 (Integration)  
**Estimated Effort:** 10-14 hours  
**Priority:** CRITICAL  
**Dependencies:** BATCH-02

---

## 📋 Onboarding & Workflow

### Developer Instructions
This is the core logic batch. You will implement the systems that make the demo actually work: bridging replay data, synchronizing transforms, handling time, and wiring it all up in `Program.cs`.

### Required Reading (IN ORDER)
1.  **Task Definitions:** [`docs/TASK-DETAIL.md`](../../docs/TASK-DETAIL.md) - Specs for tasks 011-015.
2.  **Design Document:** [`docs/DESIGN.md`](../../docs/DESIGN.md) - Section 9 (Systems) and 10 (Integration).

### Source Code Location
- **Systems:** `Fdp.Examples.NetworkDemo/Systems/`
- **Entry Point:** `Fdp.Examples.NetworkDemo/Program.cs`
- **Tests:** `Fdp.Examples.NetworkDemo.Tests/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-03-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1.  **Transform Sync:** Implement `TransformSyncSystem` → Tests ✅
2.  **Replay Bridge:** Implement `ReplayBridgeSystem` → Tests ✅
3.  **Time Input:** Implement `TimeInputSystem` → Tests ✅
4.  **Integration:** Wire up `Program.cs` (Live & Replay modes) → Manual Verification ✅

**DO NOT** start wiring `Program.cs` until the systems are tested in isolation.

---

## ✅ Tasks

### Task 1: Transform Sync System (FDP-DRP-011)
**File:** `Fdp.Examples.NetworkDemo/Systems/TransformSyncSystem.cs`

**Description:**
Bridge application state (`DemoPosition`) and network buffer (`NetworkPosition`).
- **Owned:** Copy `DemoPosition` -> `NetworkPosition`.
- **Remote:** Smooth `NetworkPosition` -> `DemoPosition` (using Lerp).

**Requirements:**
1.  Check `HasAuthority(entity, CHASSIS_KEY)` to determine direction.
2.  Use `SMOOTHING_RATE` (e.g. 10.0f) for remote entities.

**Tests:**
- ✅ `TransformSync_Owned_CopiesToBuffer`: Set DemoPos, run system, check NetPos.
- ✅ `TransformSync_Remote_SmoothsPosition`: Set NetPos, run system, check DemoPos moved towards it.

### Task 2: Replay Bridge System (FDP-DRP-012)
**File:** `Fdp.Examples.NetworkDemo/Systems/ReplayBridgeSystem.cs`

**Description:**
The heart of the replay mechanism. Reads from `ShadowRepo` (Recording) and injects into `LiveRepo` (Simulation) based on **original ownership**.

**Requirements:**
1.  Load recording using `PlaybackController`.
2.  Advance shadow world frame-by-frame.
3.  **Selective Injection:**
    - Iterate Shadow entities.
    - If `ShadowRepo.HasAuthority(shadowEnt, KEY)`, copy component to Live entity.
    - **CRITICAL:** Copy `NetworkIdentity` and `NetworkAuthority` on first encounter.
4.  Handle Play/Pause/Speed input.

**Tests:**
- ✅ `ReplayBridge_InjectsOwnedComponents`: Mock recording with Owned Chassis & Unowned Turret. Verify ONLY Chassis injected.
- ✅ `ReplayBridge_CopiesIdentity`: Verify `NetworkIdentity` is copied to live entity.

### Task 3: Time Mode Input System (FDP-DRP-013)
**File:** `Fdp.Examples.NetworkDemo/Systems/TimeInputSystem.cs`

**Description:**
Handle `T` (Toggle Mode) and `Arrow Keys` (Time Scale) input.

**Requirements:**
1.  Inject `DistributedTimeCoordinator`.
2.  Call `SwitchToDeterministic()` / `SwitchToContinuous()`.

**Tests:**
- ✅ `TimeInput_Toggle_CallsCoordinator`: Mock coordinator, verify method call.

### Task 4: Integration (FDP-DRP-014 & FDP-DRP-015)
**File:** `Fdp.Examples.NetworkDemo/Program.cs`

**Description:**
Wire everything together.
- **Live Mode:** Register Physics, Input, Recorder (exclude system IDs), Sync. Save Metadata on exit.
- **Replay Mode:** Load Metadata, Reserve IDs, Register ReplayBridge, Sync. **Disable Physics**.

**Requirements:**
1.  **ID Reservation:** Use `FdpConfig.SYSTEM_ID_RANGE` (Live) or `Meta.MaxEntityId` (Replay).
2.  **Modules:** Register CycloneDDS, Geographic Module.
3.  **Translators:** Register Geodetic & Auto-translators.

---

## 🧪 Testing Requirements

**Test Project:** `Fdp.Examples.NetworkDemo.Tests/`

**Quality Standards:**
- **Replay Bridge:** This is the most complex system. Ensure unit tests cover the "Partial Authority" case (owning one component but not another).
- **Mocking:** You will need to mock `PlaybackController` or create a small real recording file for the Bridge tests.

---

## ⚠️ Common Pitfalls to Avoid
- **Replay ID Collision:** If `ReserveIdRange` isn't called before `ReplayBridge` starts, the live world might allocate an ID that the recording needs.
- **Physics in Replay:** Ensure Physics system is **NOT** registered in Replay mode, or time scale is 0. Otherwise, physics will fight the replay injection.
- **Missing Metadata:** Replay will fail if `.fdp.meta` isn't saved/loaded correctly.

---

## 📚 Reference Materials
- [TASK-DETAIL.md](../../docs/TASK-DETAIL.md) - Tasks 011-015
- [DESIGN.md](../../docs/DESIGN.md) - Section 9 (Systems)
