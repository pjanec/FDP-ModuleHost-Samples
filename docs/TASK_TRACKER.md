# FDP Engine Refactoring - Task Tracker

**Version:** 1.0  
**Date:** 2026-02-04  
**Last Updated:** 2026-02-04

This document tracks the status of all tasks in the FDP refactoring project.

**Legend**:
- ⬜ **To Do**: Not started
- 🟦 **In Progress**: Currently being worked on
- ✅ **Done**: Completed and verified
- 🟨 **Blocked**: Waiting on dependency
- ❌ **Cancelled**: No longer needed

---

## Summary Statistics

| Phase | Total Tasks | Done | In Progress | To Do | Blocked |
|-------|------------|------|-------------|-------|---------|
| Phase 0: Foundation | 13 | 0 | 0 | 13 | 0 |
| Phase 1: Lifecycle | 8 | 0 | 0 | 8 | 0 |
| Phase 2: Time | 5 | 0 | 0 | 5 | 0 |
| Phase 3: Replication Core | 8 | 0 | 0 | 8 | 0 |
| Phase 4: Ghost Protocol | 8 | 0 | 0 | 8 | 0 |
| Phase 5: Ownership + Egress | 8 | 0 | 0 | 8 | 0 |
| Phase 6: Sub-Entities | 5 | 0 | 0 | 5 | 0 |
| Phase 7: Plugin Refactor | 6 | 0 | 0 | 6 | 0 |
| Phase 8: NetworkDemo | 11 | 0 | 0 | 11 | 0 |
| Phase 9: Integration | 6 | 0 | 0 | 6 | 0 |
| **TOTAL** | **78** | **0** | **0** | **78** | **0** |


---

## Phase 0: Foundation & Interfaces

**Goal**: Create base infrastructure and interfaces without breaking existing code  
**Duration**: ~1.5 weeks  
**Status**: ⬜ Not Started

| Task ID | Description | Status | Assignee | Start Date | End Date | Notes |
|---------|-------------|--------|----------|------------|----------|-------|
| FDP-IF-001 | Create FDP.Interfaces Project | ⬜ | - | - | - | Foundation for all toolkit contracts |
| FDP-IF-002 | Define ITkbDatabase Interface | ⬜ | - | - | - | Depends on FDP-IF-001 |
| FDP-IF-003 | Define INetworkTopology Interface | ⬜ | - | - | - | Depends on FDP-IF-001 |
| FDP-IF-004 | Define INetworkMaster Interface | ⬜ | - | - | - | Depends on FDP-IF-001 |
| FDP-IF-005 | Define IDescriptorTranslator Interface | ⬜ | - | - | - | Depends on FDP-IF-001 |
| FDP-IF-006 | Define ISerializationProvider Interface | ⬜ | - | - | - | UPDATED: Uses IEntityCommandBuffer, not EntityRepository |
| **FDP-IF-007** | **Move Transport Interfaces to FDP.Interfaces** | ⬜ | - | - | - | **CRITICAL: Prevents circular dependency** |
| FDP-TKB-001 | Create FDP.Toolkit.Tkb Project | ⬜ | - | - | - | Depends on FDP-IF-002 |
| FDP-TKB-002 | Implement PackedKey Utilities | ⬜ | - | - | - | (Ordinal << 32) \| InstanceId |
| FDP-TKB-003 | Implement MandatoryDescriptor Type | ⬜ | - | - | - | Hard/Soft requirement tracking |
| FDP-TKB-004 | Enhance TkbTemplate with TkbType Support | ⬜ | - | - | - | Add mandatory descriptor checking |
| FDP-TKB-005 | Enhance TkbDatabase with TkbType Lookup | ⬜ | - | - | - | Dual-key indexing |
| **FDP-TKB-006** | **Add Sub-Entity Blueprint Support** | ⬜ | - | - | - | **NEW: Define child blueprints for auto-spawning parts** |


---

## Phase 1: Lifecycle Extraction

**Goal**: Extract ELM to standalone toolkit  
**Duration**: ~1.5 weeks  
**Status**: ⬜ Not Started

| Task ID | Description | Status | Assignee | Start Date | End Date | Notes |
|---------|-------------|--------|----------|------------|----------|-------|
| FDP-LC-001 | Create FDP.Toolkit.Lifecycle Project | ⬜ | - | - | - | Depends on FDP-TKB-005 |
| FDP-LC-002 | Move Lifecycle Events | ⬜ | - | - | - | UPDATED: Use TkbType (not BlueprintId) for consistency |
| FDP-LC-003 | Implement BlueprintApplicationSystem | ⬜ | - | - | - | Applies TKB with preserveExisting flag |
| FDP-LC-004 | Move EntityLifecycleModule | ⬜ | - | - | - | Main coordination module |
| FDP-LC-005 | Move LifecycleSystem | ⬜ | - | - | - | ACK processing and timeouts |
| FDP-LC-006 | Implement LifecycleCleanupSystem | ⬜ | - | - | - | Remove transient construction components |
| FDP-LC-007 | Clean Up ModuleHost.Core | ⬜ | - | - | - | Delete ELM folder, verify no breakage |
| FDP-LC-008 | Integration Test - Lifecycle Toolkit | ⬜ | - | - | - | Full lifecycle with direct injection |


---

## Phase 2: Time Extraction

**Goal**: Extract time synchronization to standalone toolkit  
**Duration**: ~1 week  
**Status**: ⬜ Not Started

| Task ID | Description | Status | Assignee | Start Date | End Date | Notes |
|---------|-------------|--------|----------|------------|----------|-------|
| FDP-TM-001 | Create FDP.Toolkit.Time Project | ⬜ | - | - | - | Independent of lifecycle |
| FDP-TM-002 | Move Time Controllers | ⬜ | - | - | - | Master, Slave, Stepped controllers |
| FDP-TM-003 | Move Time Messages/Descriptors | ⬜ | - | - | - | TimePulse, FrameOrder, etc. |
| FDP-TM-004 | Clean Up ModuleHost.Core Time Code | ⬜ | - | - | - | Keep only ITimeController interface |
| FDP-TM-005 | Integration Test - Time Synchronization | ⬜ | - | - | - | Master/Slave PLL sync test |

---

## Phase 3: Replication - Core Infrastructure

**Goal**: Create replication toolkit foundation  
**Duration**: ~2 weeks  
**Status**: ⬜ Not Started

| Task ID | Description | Status | Assignee | Start Date | End Date | Notes |
|---------|-------------|--------|----------|------------|----------|-------|
| FDP-REP-001 | Create FDP.Toolkit.Replication Project | ⬜ | - | - | - | Depends on LC-008, TM-005 |
| FDP-REP-002 | Implement Core Network Components | ⬜ | - | - | - | NetworkIdentity, NetworkAuthority, DescriptorOwnership |
| FDP-REP-003 | Implement NetworkEntityMap | ⬜ | - | - | - | With graveyard for timeout handling |
| FDP-REP-004 | Implement BlockIdManager | ⬜ | - | - | - | Block-based ID allocation |
| FDP-REP-005 | Implement ID Allocation Messages | ⬜ | - | - | - | IdBlockRequest/Response |
| FDP-REP-006 | Implement IdAllocationMonitorSystem | ⬜ | - | - | - | Watches pool level, requests refills |
| FDP-REP-007 | Test ID Allocation | ⬜ | - | - | - | Block pattern prevents collisions |
| **FDP-REP-008** | **Implement Reflection-Based Auto-Discovery** | ⬜ | - | - | - | **NEW: ReplicationBootstrap + AutoTranslator for zero boilerplate** |


---

## Phase 4: Replication - Ghost Protocol

**Goal**: Implement zero-allocation ghost handling  
**Duration**: ~2 weeks  
**Status**: ⬜ Not Started

| Task ID | Description | Status | Assignee | Start Date | End Date | Notes |
|---------|-------------|--------|----------|------------|----------|-------|
| FDP-REP-101 | Implement NetworkSpawnRequest Component | ⬜ | - | - | - | Tag: Master arrived, waiting for mandatory set |
| FDP-REP-102 | Implement BinaryGhostStore Component | ⬜ | - | - | - | UPDATED: Added IdentifiedAtFrame field for soft timeout logic |
| FDP-REP-103 | Implement Shared NativeMemoryPool | ⬜ | - | - | - | Shared byte buffer for all ghosts |
| FDP-REP-104 | Implement SerializationRegistry | ⬜ | - | - | - | Maps ordinal → serialization provider |
| FDP-REP-105 | Implement GhostCreationSystem | ⬜ | - | - | - | Creates ghosts on first descriptor arrival |
| FDP-REP-106 | Implement GhostPromotionSystem | ⬜ | - | - | - | UPDATED: Time-budgeted with FIFO queue to prevent starvation |
| FDP-REP-107 | Implement GhostTimeoutSystem | ⬜ | - | - | - | Cleanup stale ghosts, populate graveyard |
| FDP-REP-108 | Test Ghost Protocol | ⬜ | - | - | - | Out-of-order packets, timeouts, hard/soft requirements |



---

## Phase 5: Replication - Ownership Management & Smart Egress

**Goal**: Implement ownership transfer, crash recovery, and intelligent egress optimization  
**Duration**: ~2 weeks  
**Status**: ⬜ Not Started

| Task ID | Description | Status | Assignee | Start Date | End Date | Notes |
|---------|-------------|--------|----------|------------|----------|-------|
| FDP-REP-201 | Define OwnershipUpdate Message | ⬜ | - | - | - | EntityId, PackedDescriptorKey, NewOwnerId |
| FDP-REP-202 | Define DescriptorAuthorityChanged Event | ⬜ | - | - | - | Local event for system reactivity |
| FDP-REP-203 | Implement OwnershipIngressSystem | ⬜ | - | - | - | 4-step handshake protocol |
| FDP-REP-204 | Implement DisposalMonitoringSystem | ⬜ | - | - | - | Crash recovery, return to primary owner |
| FDP-REP-205 | Implement OwnershipEgressSystem | ⬜ | - | - | - | Force confirmation writes |
| FDP-REP-206 | Test Ownership Transfer | ⬜ | - | - | - | Normal transfer, crash recovery, multi-part |
| **FDP-REP-207** | **Implement Smart Egress Tracking** | ⬜ | - | - | - | **NEW: Salted rolling window + dirty tracking** |
| **FDP-REP-306** | **Implement Hierarchical Authority Extensions** | ⬜ | - | - | - | **HasAuthority extension with parent-link fallback** |



---

## Phase 6: Replication - Sub-Entity Parts

**Goal**: Handle multi-instance descriptors as sub-entities  
**Duration**: ~1 week  
**Status**: ⬜ Not Started

| Task ID | Description | Status | Assignee | Start Date | End Date | Notes |
|---------|-------------|--------|----------|------------|----------|-------|
| FDP-REP-301 | Implement PartMetadata Component | ⬜ | - | - | - | InstanceId, ParentEntity, DescriptorOrdinal (for authority lookup) |
| FDP-REP-302 | Implement ChildMap Component | ⬜ | - | - | - | On parent: InstanceId → Entity |
| FDP-REP-303 | Implement Part Spawning Logic | ⬜ | - | - | - | Create sub-entities for InstanceId > 0 |
| FDP-REP-304 | Implement Parent-Child Linking System | ⬜ | - | - | - | Maintain bidirectional links |
| FDP-REP-305 | Test Multi-Instance Descriptors | ⬜ | - | - | - | Turrets, attachments, independent ownership |


---

## Phase 7: Plugin Refactoring

**Goal**: Slim down Cyclone plugin to pure transport  
**Duration**: ~1 week  
**Status**: ⬜ Not Started

| Task ID | Description | Status | Assignee | Start Date | End Date | Notes |
|---------|-------------|--------|----------|------------|----------|-------|
| FDP-PLG-001 | Move NetworkIdentity to Replication Toolkit | ⬜ | - | - | - | From plugin components to toolkit |
| FDP-PLG-002 | Move NetworkPosition/Velocity to Toolkit | ⬜ | - | - | - | Generic network components |
| FDP-PLG-003 | Implement CycloneSerializationProvider | ⬜ | - | - | - | Uses CycloneDDS native serialization |
| FDP-PLG-004 | Refactor Plugin to Use Toolkit Interfaces | ⬜ | - | - | - | Remove generic networking logic |
| FDP-PLG-005 | Move Relevant Tests | ⬜ | - | - | - | Update test references |
| FDP-PLG-006 | Verify Plugin Integration | ⬜ | - | - | - | All plugin tests pass with new structure |

---

## Phase 8: NetworkDemo Refactoring

**Goal**: Demonstrate new architecture with real application  
**Duration**: ~1.5 weeks  
**Status**: ⬜ Not Started

| Task ID | Description | Status | Assignee | Start Date | End Date | Notes |
|---------|-------------|--------|----------|------------|----------|-------|
| FDP-DEMO-001 | Restructure NetworkDemo Folders | ⬜ | - | - | - | Descriptors/, Systems/, Configuration/ |
| FDP-DEMO-002 | Define DemoMasterDescriptor | ⬜ | - | - | - | Implements INetworkMaster |
| FDP-DEMO-003 | Define PhysicsDescriptor | ⬜ | - | - | - | With [FdpDescriptor] attribute |
| FDP-DEMO-004 | Define TurretDescriptor | ⬜ | - | - | - | Multi-instance example |
| FDP-DEMO-005 | Implement TkbSetup Configuration | ⬜ | - | - | - | Define blueprints and mandatory descriptors |
| FDP-DEMO-006 | Implement DemoTopology | ⬜ | - | - | - | Static or dynamic peer discovery |
| FDP-DEMO-007 | Refactor SimplePhysicsSystem | ⬜ | - | - | - | Use view.HasAuthority pattern |
| FDP-DEMO-008 | Refactor PlayerInputSystem | ⬜ | - | - | - | Authority-aware input handling |
| FDP-DEMO-009 | Implement Auto-Discovery Bootstrap | ⬜ | - | - | - | Reflection-based translator registration |
| FDP-DEMO-010 | Update Program.cs | ⬜ | - | - | - | Clean bootstrap with minimal code |
| FDP-DEMO-011 | Test NetworkDemo | ⬜ | - | - | - | Run with 2+ nodes, verify functionality |

---

## Phase 9: Integration & Documentation

**Goal**: Ensure everything works together  
**Duration**: ~1 week  
**Status**: ⬜ Not Started

| Task ID | Description | Status | Assignee | Start Date | End Date | Notes |
|---------|-------------|--------|----------|------------|----------|-------|
| FDP-INT-001 | Cross-Toolkit Integration Tests | ⬜ | - | - | - | Lifecycle + Replication + Time together |
| FDP-INT-002 | Performance Benchmarking | ⬜ | - | - | - | Compare before/after refactoring |
| FDP-INT-003 | Memory Profiling | ⬜ | - | - | - | Verify zero-allocation ghost stashing |
| FDP-INT-004 | Update Architecture Documentation | ⬜ | - | - | - | README, diagrams, UML |
| FDP-INT-005 | Create Migration Guide | ⬜ | - | - | - | How to migrate other examples |
| FDP-INT-006 | Final Verification | ⬜ | - | - | - | All tests pass, no warnings, documentation complete |

---

## Future Work (Post-Refactoring)

**Status**: ⬜ Not Scheduled

| Task ID | Description | Priority | Notes |
|---------|-------------|----------|-------|
| FDP-FUT-001 | Rename Fdp.Kernel → FDP.ECS | Low | Deferred per user request |
| FDP-FUT-002 | Rename ModuleHost.Core → FDP.ModuleHost | Low | Deferred per user request |
| FDP-FUT-003 | Implement Smart Egress Optimization | Medium | Dirty tracking, rolling windows |
| FDP-FUT-004 | Create Alternative Network Plugins | Low | ENet, WebSockets |
| FDP-FUT-005 | Source Generator for Translators | Medium | Automatic descriptor → component mapping |
| FDP-FUT-006 | Dynamic TKB Loading from Files | Low | Runtime blueprint loading |
| FDP-FUT-007 | Enhanced Replay Integration | Medium | Better recording with new architecture |

---

## Risks & Issues

| ID | Description | Status | Mitigation | Owner |
|----|-------------|--------|------------|-------|
| RISK-001 | Breaking changes during extraction could destabilize existing code | Open | Phase 0 creates stable interfaces first; incremental migration | - |
| RISK-002 | Performance regression in ghost handling | Open | Early benchmarking in Phase 4; profiling at each step | - |
| RISK-003 | Complex ownership logic may have subtle bugs | Open | Comprehensive test scenarios; integration tests with real DDS | - |
| RISK-004 | Over-engineering with too many abstractions | Open | Each abstraction justified by reuse need; pragmatic approach | - |

---

## Build & Test Status

**Last Build**: N/A  
**Build Status**: N/A  
**Test Pass Rate**: N/A

```
Phase 0 Tests: 0/0 passing (N/A)
Phase 1 Tests: 0/0 passing (N/A)
Phase 2 Tests: 0/0 passing (N/A)
Phase 3 Tests: 0/0 passing (N/A)
```

---

## Notes & Decisions

### 2026-02-04 (18:40) - Final Technical Refinements
**Three Micro-Optimizations for Implementation Efficiency**

1. **IDataSample.InstanceId** (FDP-IF-007 update):
   - Added `long InstanceId` property to interface
   - Enables routing to sub-entity parts without reflection
   - Transport layer provides this from DDS metadata
   
2. **PartMetadata.DescriptorOrdinal** (FDP-REP-301 update):
   - Added `int DescriptorOrdinal` field to component
   - Removes need for reflection in `HasAuthority()` extension
   - Stored during part spawning, used during authority checks
   
3. **Chunk Version Early-Out** (FDP-REP-207 enhancement):
   - `ShouldPublishDescriptor()` now accepts `chunkVersion` parameter
   - Leverages existing ECS chunk versioning for early-out
   - Skip entire chunks with no changes since last publish
   - Critical optimization for egress performance

**Impact**: No task count change, only specification refinements

**Result**: Eliminates potential "TODO" blocks during implementation

### 2026-02-04 (18:31) - Final Technical Gaps Resolved

**Deep-Dive Review Complete - Documentation 100% Implementation-Ready**

**Three Critical Gaps Addressed**:
1. **Gap #1 - Smart Egress Tracking**: Added **FDP-REP-207** (Phase 5)
   - `EgressPublicationState` component with `LastPublishedTickMap` and `DirtyDescriptors`
   - Salted Rolling Window formula: `(currentTick + entityId % RefreshInterval) % RefreshInterval == 0`
   - Prevents unreliable descriptor spam while ensuring periodic refresh
   - Integration with `AutoTranslator.ScanAndPublish`

2. **Gap #2 - Promotion Priority Queue**: Enhanced **FDP-REP-106** (Phase 4)
   - Added FIFO `Queue<Entity>` for ghost promotion ordering
   - Prevents starvation when time budget (2ms) is exhausted
   - Deferred ghosts stay at front of queue for next frame
   - Ensures "First-Ready, First-Promoted" behavior

3. **Gap #3 - Naming Consistency**: Updated **FDP-LC-002** (Phase 1)
   - Changed `BlueprintId` → `TkbType` in all lifecycle events
   - Consistent with `INetworkMaster.TkbType`, `TkbTemplate.TkbType`, `TkbDatabase.GetByType(long tkbType)`
   - Type changed from `int` → `long` for network compatibility

**Impact**: 
- Phase 5 extended: 7 → **8 tasks** (+0.5 weeks)
- Total project: 77 → **78 tasks** (~13 weeks)
- Phase 5 renamed: "Ownership Management" → "Ownership Management & Smart Egress"

**Architecture Verification Complete**:
- ✅ Implicit Ownership (no OwnerId in descriptors)
- ✅ Binary Ghost Stashing (zero-allocation with NativeMemoryPool)
- ✅ Sub-Entity Parts (child blueprints + hierarchical authority)
- ✅ Auto-Discovery (reflection-based translator generation)
- ✅ Circular Dependency Prevention (transport interfaces in Layer 0)
- ✅ Smart Egress (salted rolling window + dirty tracking)
- ✅ Ghost Promotion Fairness (FIFO queue with time budget)

**See**: `docs/TASK-DETAILS-FINAL-GAPS.md` for complete specifications.

**Status**: **DOCUMENTATION COMPLETE AND READY FOR IMPLEMENTATION** 🚀

### 2026-02-04 (18:26) - Critical Task Updates

**Technical Gap Analysis Complete**
- Added **FDP-IF-007**: Move IDataReader/IDataWriter to FDP.Interfaces (prevents circular dependency)
- Added **FDP-TKB-006**: Sub-entity blueprint support (enables auto-spawning of parts like Turrets)
- Added **FDP-REP-008**: Reflection-based auto-discovery (ReplicationBootstrap + AutoTranslator)
- Added **FDP-REP-306**: Hierarchical authority extensions (HasAuthority with parent-link fallback)
- Updated **FDP-IF-006**: Changed ISerializationProvider.Apply signature to use IEntityCommandBuffer (thread-safety)
- Updated **FDP-REP-102**: Added IdentifiedAtFrame field to BinaryGhostStore (soft timeout calculation)

**Impact**: Phase 0 extended from 11 to 13 tasks (+0.5 weeks). Total project now 77 tasks (~13 weeks).

**See**: `docs/TASK-DETAILS-ADDENDUM.md` for complete specifications of new tasks.

### 2026-02-04 (Initial)

- Initial task structure created
- Decided to keep `Fdp.Kernel` and `ModuleHost.Core` names unchanged for now
- Confirmed layering: FDP.Interfaces → Toolkits → Plugins → Application
- Established that `ITimeController` stays in ModuleHost.Core (consumed by kernel)
- Ghost stashing will use binary approach with CycloneDDS serialization
- Ownership is implicit (from DDS writer metadata), not explicit in descriptors

---

## Open Questions

1. **Q**: Should we batch-migrate tests or move them with each task?  
   **A**: Move tests with each task to ensure immediate verification.

2. **Q**: How rigorously should we refactor existing tests vs. just relocating them?  
   **A**: Relocate first, then enhance as needed for new features.

3. **Q**: Should we create wrapper scripts for running partial test suites by phase?  
   **A**: TBD - would be useful for CI/CD

---

## Completion Criteria

**Phase 0**: ✅ All interfaces compile, TKB enhanced, no existing code broken  
**Phase 1**: ✅ Lifecycle toolkit independent, all tests pass, ModuleHost.Core cleaned  
**Phase 2**: ✅ Time toolkit independent, PLL sync working, Core cleaned  
**Phase 3**: ✅ Core replication components working, ID allocation functional  
**Phase 4**: ✅ Ghost protocol handles out-of-order packets, zero GC allocations  
**Phase 5**: ✅ Ownership transfer working, crash recovery tested  
**Phase 6**: ✅ Multi-instance descriptors create sub-entities correctly  
**Phase 7**: ✅ Plugin slimmed down, only DDS-specific code remains  
**Phase 8**: ✅ NetworkDemo runs with new architecture, no manual translators  
**Phase 9**: ✅ All tests pass, no performance regression, documentation complete  

**Overall Project Completion**: All phases complete ✅

---

## Document Control

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-04 | AI Assistant | Initial task tracker created |

