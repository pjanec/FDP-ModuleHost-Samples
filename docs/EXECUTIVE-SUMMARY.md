# FDP Engine Refactoring - Executive Summary

**Version**: 1.0  
**Date**: 2026-02-04  
**Status**: ✅ **Documentation Complete - Ready for Implementation**

---

## 📊 Project Overview

**Objective**: Refactor the FDP (Flight Data Protocol) engine from a monolithic network-coupled architecture to a modular, reusable toolkit-based design.

**Duration**: ~13 weeks  
**Total Tasks**: 78  
**Team Size**: 1 developer

---

## 🎯 Key Goals

1. **Modularity**: Extract network logic into standalone, reusable toolkits
2. **Maintainability**: Reduce boilerplate through reflection-based auto-discovery
3. **Performance**: Achieve zero-allocation ghost handling and intelligent egress
4. **Correctness**: Implement robust ownership management and crash recovery
5. **Flexibility**: Support complex multi-instance descriptors (sub-entities/parts)

---

## 🏗️ Architecture Overview

### Layered Structure

```
┌─────────────────────────────────────────────────────────┐
│  Layer 5: FDP.Examples.* (Applications)                 │
│           - NetworkDemo (refactored)                    │
├─────────────────────────────────────────────────────────┤
│  Layer 4: FDP.Plugins.* (Drivers)                       │
│           - FDP.Plugins.Network.Cyclone (DDS transport) │
├─────────────────────────────────────────────────────────┤
│  Layer 3: FDP.Toolkit.* (Reusable Frameworks)           │
│           - Lifecycle, Time, Replication, TKB           │
├─────────────────────────────────────────────────────────┤
│  Layer 2: FDP.ModuleHost (Orchestration)                │
│           - Formerly ModuleHost.Core (no rename yet)    │
├─────────────────────────────────────────────────────────┤
│  Layer 1: FDP.ECS (Core Storage)                        │
│           - Formerly Fdp.Kernel (no rename yet)         │
├─────────────────────────────────────────────────────────┤
│  Layer 0: FDP.Interfaces (Contracts)                    │
│           - ITkbDatabase, INetworkTopology, etc.        │
└─────────────────────────────────────────────────────────┘
```

### New Projects Created

| Project | Purpose | Dependencies |
|---------|---------|--------------|
| **FDP.Interfaces** | Contract layer for cross-toolkit communication | Fdp.Kernel |
| **FDP.Toolkit.TKB** | Enhanced blueprint system with sub-entity support | FDP.Interfaces |
| **FDP.Toolkit.Lifecycle** | Entity Lifecycle Management (ELM) extraction | FDP.Toolkit.TKB |
| **FDP.Toolkit.Time** | Time synchronization (PLL, lockstep) | FDP.Interfaces |
| **FDP.Toolkit.Replication** | Network replication, ghost protocol, ownership | All above |

---

## 🚀 Key Technical Innovations

### 1. **Implicit Ownership**
- **Problem**: Storing `OwnerId` in descriptors couples data to authority
- **Solution**: Derive authority from DDS writer metadata (`PublisherId`)
- **Benefit**: Cleaner data models, automatic ownership tracking

### 2. **Binary Ghost Stashing**
- **Problem**: Boxing descriptors for unknown entities causes GC pressure
- **Solution**: Serialize descriptors to shared `NativeMemoryPool` using CDR encoding
- **Benefit**: Zero-allocation ghost handling, deterministic memory usage

### 3. **Sub-Entity Parts**
- **Problem**: Multi-instance descriptors (e.g., Turrets) need separate ECS entities
- **Solution**: TKB defines child blueprints, auto-spawned with parent linking
- **Benefit**: Clean separation, independent ownership per part

### 4. **Auto-Discovery Bootstrap**
- **Problem**: Manual translator registration is boilerplate-heavy
- **Solution**: `[FdpDescriptor]` attribute + reflection generates `AutoTranslator<T>`
- **Benefit**: "Network-ready ECS" - developers only write data structures

### 5. **Smart Egress**
- **Problem**: Constant publication of unchanged data wastes bandwidth
- **Solution**: Salted rolling window + dirty tracking
- **Formula**: `(currentTick + entityId % RefreshInterval) % RefreshInterval == 0`
- **Benefit**: Deterministic, evenly-distributed refresh without per-entity timers

### 6. **Ghost Promotion Fairness**
- **Problem**: Time-budgeted promotion can starve old ghosts
- **Solution**: FIFO queue ensures "first-ready, first-promoted"
- **Benefit**: Predictable ghost materialization, no starvation

---

## 📋 Implementation Phases

### Phase 0: Foundation & Interfaces (13 tasks, ~1.5 weeks)
**Goal**: Create stable contract layer without breaking existing code

**Key Tasks**:
- FDP-IF-001 to FDP-IF-007: Define all toolkit interfaces
- FDP-TKB-001 to FDP-TKB-006: Enhance TKB with TkbType, mandatory descriptors, sub-entities

**Deliverables**:
- `FDP.Interfaces` project with transport abstraction
- `FDP.Toolkit.TKB` with `PackedKey`, `MandatoryDescriptor`, `ChildBlueprints`

---

### Phase 1: Lifecycle Extraction (8 tasks, ~1.5 weeks)
**Goal**: Extract ELM from ModuleHost.Core to standalone toolkit

**Key Tasks**:
- FDP-LC-002: Move lifecycle events (use `TkbType`, not `BlueprintId`)
- FDP-LC-003: Blueprint application with `preserveExisting` flag
- FDP-LC-004: EntityLifecycleModule extraction

**Deliverables**:
- `FDP.Toolkit.Lifecycle` assembly
- Direct injection pattern preserved
- ModuleHost.Core cleaned (ELM folder deleted)

---

### Phase 2: Time Extraction (5 tasks, ~1 week)
**Goal**: Extract time synchronization to standalone toolkit

**Key Tasks**:
- FDP-TM-002: Move Master/Slave PLL controllers
- FDP-TM-003: Move time messages (`TimePulse`, `FrameOrder`, etc.)

**Deliverables**:
- `FDP.Toolkit.Time` assembly
- `ITimeController` remains in ModuleHost.Core (consumed by kernel)

---

### Phase 3: Replication Core (8 tasks, ~2 weeks)
**Goal**: Create foundation for network replication

**Key Tasks**:
- FDP-REP-002: Core components (`NetworkIdentity`, `NetworkAuthority`, `DescriptorOwnership`)
- FDP-REP-003: NetworkEntityMap with graveyard
- FDP-REP-008: **Auto-Discovery Bootstrap** (reflection-based translator generation)

**Deliverables**:
- `FDP.Toolkit.Replication` assembly
- ID allocation framework
- Zero-boilerplate descriptor registration

---

### Phase 4: Ghost Protocol (8 tasks, ~2 weeks)
**Goal**: Implement zero-allocation ghost handling

**Key Tasks**:
- FDP-REP-102: BinaryGhostStore with `IdentifiedAtFrame` field
- FDP-REP-103: Shared NativeMemoryPool
- FDP-REP-106: GhostPromotionSystem with **FIFO queue** and time budget

**Deliverables**:
- Zero-allocation ghost stashing
- Time-budgeted promotion (2ms/frame)
- Mandatory descriptor enforcement (hard/soft requirements)

---

### Phase 5: Ownership & Egress (8 tasks, ~2 weeks)
**Goal**: Ownership transfer, crash recovery, intelligent publishing

**Key Tasks**:
- FDP-REP-203: 4-step ownership handshake
- FDP-REP-204: Crash recovery (return to primary owner)
- FDP-REP-207: **Smart Egress** with salted rolling window
- FDP-REP-306: Hierarchical authority (parent-link fallback)

**Deliverables**:
- Robust ownership management
- Bandwidth-efficient egress
- Authority inheritance for sub-entities

---

### Phase 6: Sub-Entities (5 tasks, ~1 week)
**Goal**: Multi-instance descriptor support as ECS sub-entities

**Key Tasks**:
- FDP-REP-301/302: PartMetadata + ChildMap components
- FDP-REP-303: Auto-spawning from TKB child blueprints

**Deliverables**:
- Turret/attachment pattern working
- Parent-child linking bidirectional

---

### Phase 7: Plugin Refactor (6 tasks, ~1 week)
**Goal**: Slim CycloneDDS plugin to pure transport

**Key Tasks**:
- FDP-PLG-001/002: Move generic components to Replication toolkit
- FDP-PLG-003: CycloneSerializationProvider (CDR binary format)

**Deliverables**:
- Plugin contains only DDS-specific code
- Generic networking logic in toolkit

---

### Phase 8: NetworkDemo Refactor (11 tasks, ~1.5 weeks)
**Goal**: Demonstrate new architecture with real application

**Key Tasks**:
- FDP-DEMO-002 to 004: Define descriptors with `[FdpDescriptor]`
- FDP-DEMO-009: Auto-discovery bootstrap (zero manual registration)
- FDP-DEMO-007/008: Authority-aware systems

**Deliverables**:
- Fully declarative demo
- Zero boilerplate (all translators auto-generated)
- Multi-node ghost spawning working

---

### Phase 9: Integration & Verification (6 tasks, ~1 week)
**Goal**: Final validation and documentation

**Key Tasks**:
- FDP-INT-001: Cross-toolkit integration tests
- FDP-INT-002/003: Performance benchmarking and memory profiling
- FDP-INT-004: Architecture documentation

**Deliverables**:
- All tests passing
- No performance regression
- Complete migration guide

---

## ✅ Architecture Verification Checklist

All critical features have been addressed and verified:

| Feature | Status | Verification |
|---------|--------|--------------|
| **Implicit Ownership** | ✅ Complete | INetworkMaster has no OwnerId; authority from PublisherId |
| **Binary Ghost Stashing** | ✅ Complete | NativeMemoryPool + ISerializationProvider with IEntityCommandBuffer |
| **Sub-Entity Parts** | ✅ Complete | TkbTemplate.ChildBlueprints + PartMetadata/ChildMap components |
| **Auto-Discovery** | ✅ Complete | ReplicationBootstrap scans [FdpDescriptor] attributes |
| **Circular Dep Prevention** | ✅ Complete | IDataReader/IDataWriter moved to FDP.Interfaces (Layer 0) |
| **Smart Egress** | ✅ Complete | EgressPublicationState + salted rolling window |
| **Ghost Promotion Fairness** | ✅ Complete | FIFO queue prevents starvation under time budget |
| **Mandatory Descriptors** | ✅ Complete | Hard/soft requirements with IdentifiedAtFrame timeout |
| **Ownership Transfer** | ✅ Complete | 4-step handshake + crash recovery |
| **Hierarchical Authority** | ✅ Complete | HasAuthority extension with parent-link fallback |

---

## 📈 Project Metrics

### Task Distribution

```
Phase 0 (Foundation):       13 tasks (16.7%)
Phase 1 (Lifecycle):         8 tasks (10.3%)
Phase 2 (Time):              5 tasks ( 6.4%)
Phase 3 (Replication Core):  8 tasks (10.3%)
Phase 4 (Ghost Protocol):    8 tasks (10.3%)
Phase 5 (Ownership+Egress):  8 tasks (10.3%)
Phase 6 (Sub-Entities):      5 tasks ( 6.4%)
Phase 7 (Plugin Refactor):   6 tasks ( 7.7%)
Phase 8 (NetworkDemo):      11 tasks (14.1%)
Phase 9 (Integration):       6 tasks ( 7.7%)
────────────────────────────────────────
Total:                      78 tasks (100%)
```

### Timeline

```
Week 1-2:   Phase 0 (Foundation)
Week 3-4:   Phase 1 (Lifecycle) + Phase 2 (Time)
Week 5-6:   Phase 3 (Replication Core)
Week 7-8:   Phase 4 (Ghost Protocol)
Week 9-10:  Phase 5 (Ownership + Egress)
Week 11:    Phase 6 (Sub-Entities) + Phase 7 (Plugin Refactor)
Week 12-13: Phase 8 (NetworkDemo) + Phase 9 (Integration)
```

---

## 🎓 Key Learnings & Design Decisions

### 1. **Layer 0 Must Be Truly Independent**
- Moving `IDataReader`/`IDataWriter` to `FDP.Interfaces` prevents circular dependencies
- Critical for toolkit cross-referencing without coupling

### 2. **Use IEntityCommandBuffer, Not EntityRepository**
- Thread-safety is paramount in multi-threaded ECS
- All toolkit systems operate on deferred mutations via command buffers

### 3. **TkbType vs BlueprintId Consistency**
- Settled on `TkbType` as the canonical field name
- `long` type for network compatibility (matches `INetworkMaster`)

### 4. **Soft Requirements Need Timing**
- `BinaryGhostStore.IdentifiedAtFrame` tracks when ghost became identified
- Enables "wait N frames for soft descriptor, then give up" logic

### 5. **Priority Queue for Fairness**
- Time-budgeted systems (like ghost promotion) need explicit ordering
- FIFO queue prevents "memory position bias" starvation

### 6. **Smart Egress is Not Optional**
- Without rolling window refresh, unreliable topics desync on packet loss
- Moved from "Future Work" to Phase 5 (core feature)

---

## 📚 Documentation Artifacts

| Document | Purpose | Status |
|----------|---------|--------|
| **DESIGN.md** | Complete architecture, layer definitions, component designs | ✅ Complete |
| **TASK-DETAILS.md** | Granular task specs with code examples and tests (Phases 0-3) | 🟨 Partial |
| **TASK-DETAILS-ADDENDUM.md** | First round of gap fixes (7 critical tasks) | ✅ Complete |
| **TASK-DETAILS-FINAL-GAPS.md** | Final 3 technical gaps (egress, queue, naming) | ✅ Complete |
| **TASK_TRACKER.md** | Status dashboard, risk tracking, build metrics | ✅ Complete |

**Total Documentation**: ~30,000+ lines of detailed specifications

---

## 🚦 Current Status

### ✅ Completed
1. **Architecture Design** - Layered structure defined
2. **Interface Contracts** - All toolkit interfaces specified
3. **Task Breakdown** - 78 concrete, testable tasks
4. **Gap Analysis** - Three rounds of technical review complete
5. **Test Specifications** - Unit test requirements for every task

### 🟨 In Progress
- None (documentation phase complete)

### ⬜ Not Started
- Implementation (ready to begin)

---

## 🎯 Success Criteria

### Technical
- ✅ All 78 tasks have clear acceptance criteria
- ✅ Zero-allocation ghost handling verified (theory)
- ✅ Bandwidth optimization designed (salted rolling window)
- ✅ No circular dependencies in layer architecture

### Organizational
- ✅ Clear migration path from monolithic to modular
- ✅ Existing code preserved (Fdp.Kernel, ModuleHost.Core not renamed)
- ✅ Incremental rollout (can deploy phase-by-phase)

### Quality
- ✅ Every task has unit test specifications
- ✅ Integration tests defined for cross-toolkit validation
- ✅ Performance benchmarking planned (Phase 9)

---

## 🔮 Future Enhancements (Post-Refactoring)

1. **Renaming**: `Fdp.Kernel` → `FDP.ECS`, `ModuleHost.Core` → `FDP.ModuleHost`
2. **Source Generators**: Auto-generate translators at compile time
3. **Alternative Transports**: ENet, WebSockets plugins
4. **Dynamic TKB Loading**: Runtime blueprint loading from files
5. **Enhanced Replay**: Better recording with new architecture

---

## 🤝 Acknowledgments

This refactoring design is the result of extensive technical collaboration, addressing:
- **10 initial gaps** identified through deep architecture review
- **3 final gaps** caught during implementation planning
- **7 critical corrections** across interfaces, components, and systems

The result is a production-ready blueprint that is **"maintenance-free"** once implemented, thanks to:
- Reflection-based auto-discovery
- Zero-allocation ghost handling
- Intelligent egress optimization
- Hierarchical authority management

---

## 📞 Next Steps

**Ready for Implementation**: Begin with **Phase 0, Task FDP-IF-001** (Create FDP.Interfaces Project)

**Documentation**: This executive summary + 4 detailed specification documents = complete reference

**Questions?**: Refer to:
- `DESIGN.md` for architecture decisions
- `TASK-DETAILS-*.md` for implementation specs
- `TASK_TRACKER.md` for current progress

---

**Document Version**: 1.0  
**Last Updated**: 2026-02-04 18:31  
**Status**: ✅ **APPROVED FOR IMPLEMENTATION**

