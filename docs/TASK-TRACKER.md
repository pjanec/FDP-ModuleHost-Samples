# FDP Engine Distributed Recording and Playback - Task Tracker

**Reference:** See [TASK-DETAIL.md](./TASK-DETAIL.md) for detailed task descriptions and success criteria.

---

## Phase 1: Kernel Foundation

**Goal:** Enable safe entity ID management for distributed replay scenarios

- [ ] **FDP-DRP-001** Entity Index ID Reservation [details](./TASK-DETAIL.md#fdp-drp-001-entity-index-id-reservation)
- [ ] **FDP-DRP-002** Entity Hydration for Replay [details](./TASK-DETAIL.md#fdp-drp-002-entity-hydration-for-replay)
- [ ] **FDP-DRP-003** Recorder Minimum ID Filter [details](./TASK-DETAIL.md#fdp-drp-003-recorder-minimum-id-filter)

---

## Phase 2: Replication Toolkit

**Goal:** Build zero-boilerplate networking infrastructure with proper recording policies

- [ ] **FDP-DRP-004** Data Policy Enforcement [details](./TASK-DETAIL.md#fdp-drp-004-data-policy-enforcement)
- [ ] **FDP-DRP-005** FdpDescriptor Attribute [details](./TASK-DETAIL.md#fdp-drp-005-fdpdescriptor-attribute)
- [ ] **FDP-DRP-006** Generic Descriptor Translator [details](./TASK-DETAIL.md#fdp-drp-006-generic-descriptor-translator)
- [ ] **FDP-DRP-007** Assembly Scanning for Auto-Registration [details](./TASK-DETAIL.md#fdp-drp-007-assembly-scanning-for-auto-registration)

---

## Phase 3: Network Demo - Infrastructure

**Goal:** Define components, translators, and metadata structures for the tank demo

- [ ] **FDP-DRP-008** Recording Metadata Structure [details](./TASK-DETAIL.md#fdp-drp-008-recording-metadata-structure)
- [ ] **FDP-DRP-009** Demo Component Definitions [details](./TASK-DETAIL.md#fdp-drp-009-demo-component-definitions)
- [ ] **FDP-DRP-010** Geographic Translator Implementation [details](./TASK-DETAIL.md#fdp-drp-010-geographic-translator-implementation)

---

## Phase 4: Network Demo - Systems

**Goal:** Implement core simulation systems for replay and synchronization

- [ ] **FDP-DRP-011** Transform Sync System [details](./TASK-DETAIL.md#fdp-drp-011-transform-sync-system)
- [ ] **FDP-DRP-012** Replay Bridge System [details](./TASK-DETAIL.md#fdp-drp-012-replay-bridge-system)
- [ ] **FDP-DRP-013** Time Mode Input System [details](./TASK-DETAIL.md#fdp-drp-013-time-mode-input-system)
- [ ] **FDP-DRP-018** Advanced Demo Modules (Radar & Damage) [details](./TASK-DETAIL.md#fdp-drp-018-advanced-demo-modules-radar--damage)
- [ ] **FDP-DRP-019** Dynamic Ownership Transfer System [details](./TASK-DETAIL.md#fdp-drp-019-dynamic-ownership-transfer-system)
- [ ] **FDP-DRP-020** TKB Mandatory Requirements Configuration [details](./TASK-DETAIL.md#fdp-drp-020-tkb-mandatory-requirements-configuration)

---

## Phase 5: Integration & Configuration

**Goal:** Wire up complete application for both live and replay modes

- [ ] **FDP-DRP-014** Program.cs Live Mode Setup [details](./TASK-DETAIL.md#fdp-drp-014-programcs-live-mode-setup)
- [ ] **FDP-DRP-015** Program.cs Replay Mode Setup [details](./TASK-DETAIL.md#fdp-drp-015-programcs-replay-mode-setup)

---

## Phase 6: Testing & Validation

**Goal:** Validate complete system with integration tests and performance benchmarks

- [ ] **FDP-DRP-016** End-to-End Integration Test [details](./TASK-DETAIL.md#fdp-drp-016-end-to-end-integration-test)
- [ ] **FDP-DRP-017** Performance Validation [details](./TASK-DETAIL.md#fdp-drp-017-performance-validation)

---

## Progress Summary

**Phase 1:** 0/3 tasks complete (0%)  
**Phase 2:** 0/4 tasks complete (0%)  
**Phase 3:** 0/3 tasks complete (0%)  
**Phase 4:** 0/6 tasks complete (0%)  
**Phase 5:** 0/2 tasks complete (0%)  
**Phase 6:** 0/2 tasks complete (0%)  

**Overall:** 0/20 tasks complete (0%)

---

## Critical Path

The following tasks form the critical path and should be prioritized:

1. FDP-DRP-001 (Entity ID Reservation)
2. FDP-DRP-002 (Entity Hydration)
3. FDP-DRP-005 (FdpDescriptor Attribute)
4. FDP-DRP-006 (Generic Translator with Ghost Stash)
5. FDP-DRP-009 (Component Definitions)
6. FDP-DRP-011 (Transform Sync System)
7. FDP-DRP-012 (Replay Bridge System with Identity Copy)
8. FDP-DRP-020 (TKB Configuration)
9. FDP-DRP-014 (Live Mode Setup)
10. FDP-DRP-015 (Replay Mode Setup with Tick fix)
11. FDP-DRP-016 (Integration Test)

**Estimated Timeline:** 22-26 developer-days

---

## Notes

- Tasks can be worked on in parallel within each phase
- Phase 1 must complete before Phase 5
- Phase 2 can proceed in parallel with Phase 1 (except task 006 needs 001)
- Phase 3 requires task 005 from Phase 2
- Phase 4 requires Phase 3 completion
- Phase 5 requires Phase 1, 3, and 4 completion
- Phase 6 requires all previous phases

---

## Milestones

- **M1: Kernel Ready** - Phase 1 complete, ID management functional
- **M2: Toolkit Ready** - Phase 2 complete, zero-boilerplate networking available
- **M3: Components Ready** - Phase 3 complete, demo structures defined
- **M4: Systems Ready** - Phase 4 complete, replay logic functional
- **M5: Integration Complete** - Phase 5 complete, application runs in both modes
- **M6: Validated** - Phase 6 complete, tested and benchmarked

---

## Risk Register

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Chunk overlap despite ID reservation | Critical | Use 65,536 gap (max chunk size) |
| **Authority Paradox (NetworkAuthority not recorded)** | **Critical** | **MUST record NetworkAuthority, NetworkIdentity, DescriptorOwnership** |
| **Stagnant Tick (no version advancement in replay)** | **Critical** | **Call world.Tick() in replay loop** |
| Shadow World memory bloat | Medium | Implement streaming/windowed playback for long recordings |
| **Ghost Stash missing in Generic Translator** | **High** | **Implement BinaryGhostStore check before applying data** |
| Network identity mapping overhead | Low | Use direct ID mapping where safe |
| Geographic transform precision | Medium | Use double precision internally, test round-trip accuracy |
| Time mode switch desync | High | Implement distributed barrier with ACK protocol |
| Partial ownership complexity | High | Thorough unit tests for authority checking |

---

**Last Updated:** {{ Current Date }}  
**Status:** Not Started  
**Lead:** {{ Project Lead }}
