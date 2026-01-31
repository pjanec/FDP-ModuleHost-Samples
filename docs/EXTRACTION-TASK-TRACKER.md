# ModuleHost.Core Extraction - Task Tracker

**Project:** ModuleHost.Core Architectural Refactoring  
**Status:** 🔵 Not Started  
**Last Updated:** 2026-01-30

**Reference Documents:**
- [EXTRACTION-DESIGN.md](EXTRACTION-DESIGN.md) - Architectural vision and detailed design
- [EXTRACTION-TASK-DETAILS.md](EXTRACTION-TASK-DETAILS.md) - Implementation details and test definitions

---

## Progress Overview

**Total Tasks:** 29  
**Completed:** 0 ✅  
**In Progress:** 0 ⏳  
**Blocked:** 0 🔴  
**Not Started:** 29 🔵

**Overall Progress:** 0% (0/29 tasks complete)

---

## Phase Status

| Phase | Status | Progress | Start Date | End Date | Duration |
|-------|--------|----------|------------|----------|----------|
| **Phase 1: Foundation Setup** | ✅ Complete | 4/4 | - | - | 1-2 days |
| **Phase 2: Network Layer Extraction** | ⏳ In Progress | 2/7 | - | - | 3-4 days |
| **Phase 3: Geographic Module Extraction** | 🔵 Not Started | 0/4 | - | - | 2 days |
| **Phase 4: Component Migration** | 🔵 Not Started | 0/3 | - | - | 2 days |
| **Phase 5: Core Simplification** | 🔵 Not Started | 0/3 | - | - | 2 days |
| **Phase 6: Application Updates** | 🔵 Not Started | 0/3 | - | - | 2-3 days |
| **Phase 7: Cleanup and Documentation** | 🔵 Not Started | 0/4 | - | - | 1-2 days |

**Estimated Total Duration:** 13-17 days

---

## Phase 1: Foundation Setup 🔵

**Goal:** Create new project structures and establish interfaces in Core  
**Status:** Not Started  
**Progress:** 4/4 tasks (100%)

| Task ID | Description | Status | Assignee | Start | End | Notes |
|---------|-------------|--------|----------|-------|-----|-------|
| **EXT-1-1** | Create ModuleHost.Network.Cyclone Project | ✅ | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-1-1-create-modulehostnetworkcyclone-project) |
| **EXT-1-2** | Create Fdp.Modules.Geographic Project | ✅ | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-1-2-create-fdpmodulesgeographic-project) |
| **EXT-1-3** | Define Core Interfaces | ✅ | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-1-3-define-core-interfaces) |
| **EXT-1-4** | Create Migration Smoke Test | ✅ | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-1-4-create-migration-smoke-test) |

### Success Criteria (Phase 1)
- [ ] ModuleHost.Network.Cyclone project builds
- [ ] Fdp.Modules.Geographic project builds
- [ ] INetworkIdAllocator interface defined
- [ ] INetworkTopology interface defined
- [ ] Baseline smoke tests pass

### Dependencies
- None

---

## Phase 2: Network Layer Extraction 🔵

**Goal:** Move DDS-specific network code to ModuleHost.Network.Cyclone  
**Status:** Not Started  
**Progress:** 5/7 tasks (85%)  
**Depends On:** Phase 1 complete

| Task ID | Description | Status | Assignee | Start | End | Notes |
|---------|-------------|--------|----------|-------|-----|-------|
| **EXT-2-1** | Create NodeIdMapper Service | ✅ | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-2-1-create-nodeidmapper-service) |
| **EXT-2-2** | Define DDS Topics | ✅ | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-2-2-define-dds-topics) |
| **EXT-2-3** | Implement DdsIdAllocator | ✅ | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-2-3-implement-ddsidallocator) |
| **EXT-2-4** | Move NetworkGatewayModule | ✅ | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-2-4-move-networkgatewaymodule) ⚠️ |
| **EXT-2-5** | Create Descriptor Translators | ✅ | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-2-5-create-descriptor-translators) |
| **EXT-2-6** | Create TypeIdMapper (CRITICAL) | ✅ | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-2-6-create-typeidmapper-critical) ⚠️ |
| **EXT-2-7** | Create ID Allocator Server (Test) | 🔵 | - | - | - | [details](TASK-EXT-2-7-IdAllocatorServer.md) 🆕 |

### Success Criteria (Phase 2)
- [ ] NodeIdMapper with 4 passing tests
- [ ] All DDS topics compile and validate
- [ ] DdsIdAllocator implements INetworkIdAllocator with 3 passing tests
- [ ] NetworkGatewayModule moved to Cyclone plugin with 3 passing tests
- [ ] All 4 translators created with integration tests passing

### Key Deliverables
- `ModuleHost.Network.Cyclone/Services/NodeIdMapper.cs`
- `ModuleHost.Network.Cyclone/Topics/*.cs` (5 topic files)
- `ModuleHost.Network.Cyclone/Services/DdsIdAllocator.cs`
- `ModuleHost.Network.Cyclone/Modules/NetworkGatewayModule.cs`
- `ModuleHost.Network.Cyclone/Translators/*.cs` (4 translator files)

---

## Phase 3: Geographic Module Extraction 🔵

**Goal:** Move GIS functionality to separate module  
**Status:** Not Started  
**Progress:** 0/4 tasks (0%)  
**Depends On:** Phase 1 complete

| Task ID | Description | Status | Assignee | Start | End | Notes |
|---------|-------------|--------|----------|-------|-----|-------|
| **EXT-3-1** | Move Geographic Components | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-3-1-move-geographic-components) |
| **EXT-3-2** | Move Geographic Systems | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-3-2-move-geographic-systems) |
| **EXT-3-3** | Move Transforms | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-3-3-move-transforms) |
| **EXT-3-4** | Create GeographicModule | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-3-4-create-geographicmodule) |

### Success Criteria (Phase 3)
- [ ] All geographic components moved with tests passing
- [ ] GeodeticSmoothingSystem and CoordinateTransformSystem moved with tests
- [ ] IGeographicTransform and WGS84Transform moved with accuracy tests
- [ ] GeographicModule created and can register both systems

### Key Deliverables
- `Fdp.Modules.Geographic/Components/PositionGeodetic.cs`
- `Fdp.Modules.Geographic/Systems/GeodeticSmoothingSystem.cs`
- `Fdp.Modules.Geographic/Transforms/WGS84Transform.cs`
- `Fdp.Modules.Geographic/GeographicModule.cs`

---

## Phase 4: Component Migration 🔵

**Goal:** Move concrete components from Core to example projects  
**Status:** Not Started  
**Progress:** 0/4 tasks (0%)  
**Depends On:** Phase 2-3 complete

| Task ID | Description | Status | Assignee | Start | End | Notes |
|---------|-------------|--------|----------|-------|-----|-------|
| **EXT-4-1** | Create Component Definitions in BattleRoyale | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-4-1-create-component-definitions-in-battleroyale) |
| **EXT-4-2** | Update BattleRoyale to use Local Components | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-4-2-update-battleroyale-to-use-local-components) |
| **EXT-4-3** | Create Shared Components Library (Optional) | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-4-3-create-shared-components-library-optional) |
| **EXT-4-4** | Refactor Core Unit Tests (CRITICAL) | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-4-4-refactor-core-unit-tests-critical) |

### Success Criteria (Phase 4)
- [ ] Position, Velocity, Health components defined in BattleRoyale
- [ ] All BattleRoyale systems updated to use local components
- [ ] BattleRoyale compiles and runs successfully
- [ ] All integration tests pass

### Key Deliverables
- `Fdp.Examples.BattleRoyale/Components/Position.cs`
- `Fdp.Examples.BattleRoyale/Components/Velocity.cs`
- `Fdp.Examples.BattleRoyale/Components/Health.cs`

---

## Phase 5: Core Simplification 🔵

**Goal:** Simplify Core types and remove duplicates  
**Status:** Not Started  
**Progress:** 0/3 tasks (0%)  
**Depends On:** Phase 2-4 complete

| Task ID | Description | Status | Assignee | Start | End | Notes |
|---------|-------------|--------|----------|-------|-----|-------|
| **EXT-5-1** | Simplify NetworkOwnership | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-5-1-simplify-networkownership) |
| **EXT-5-2** | Remove DescriptorOwnership from Core | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-5-2-remove-descriptorownership-from-core) |
| **EXT-5-3** | Delete Old Files from Core | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-5-3-delete-old-files-from-core) |

### Success Criteria (Phase 5)
- [ ] NetworkOwnership simplified to use simple int
- [ ] DescriptorOwnership moved to Cyclone plugin
- [ ] Geographic/ folder deleted from Core
- [ ] NetworkGatewayModule deleted from Core
- [ ] All Core tests pass after cleanup

### Files to Delete from Core
- `ModuleHost.Core/Geographic/` (entire folder)
- `ModuleHost.Core/Network/NetworkGatewayModule.cs`
- `ModuleHost.Core/Network/NetworkSpawnerSystem.cs` **← CRITICAL (Flaw #3)**
- `ModuleHost.Core/Network/NetworkSpawnRequest.cs` **← CRITICAL (Flaw #3)**
- `ModuleHost.Core/Network/Position.cs`
- `ModuleHost.Core/Network/Velocity.cs`
- `ModuleHost.Core/Network/DescriptorOwnership.cs`

---

## Phase 6: Application Updates 🔵

**Goal:** Update all example applications to use new structure  
**Status:** Not Started  
**Progress:** 0/3 tasks (0%)  
**Depends On:** Phase 5 complete

| Task ID | Description | Status | Assignee | Start | End | Notes |
|---------|-------------|--------|----------|-------|-----|-------|
| **EXT-6-1** | Update BattleRoyale Bootstrap | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-6-1-update-battleroyale-bootstrap) |
| **EXT-6-2** | Update CarKinem Example | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-6-2-update-carkineme-example) |
| **EXT-6-3** | Create Minimal Example (No Geographic) | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-6-3-create-minimal-example-no-geographic) |

### Success Criteria (Phase 6)
- [ ] BattleRoyale uses manual dependency injection
- [ ] BattleRoyale runs with network and geographic modules
- [ ] CarKinem updated and runs
- [ ] Minimal example runs without geographic module
- [ ] All example applications compile and execute

### Key Updates
- Add project references to ModuleHost.Network.Cyclone
- Add project references to Fdp.Modules.Geographic (optional)
- Update Program.cs to manually wire dependencies

---

## Phase 7: Cleanup and Documentation 🔵

**Goal:** Polish, document, and verify extraction  
**Status:** Not Started  
**Progress:** 0/4 tasks (0%)  
**Depends On:** Phase 6 complete

| Task ID | Description | Status | Assignee | Start | End | Notes |
|---------|-------------|--------|----------|-------|-----|-------|
| **EXT-7-1** | Update README Files | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-7-1-update-readme-files) |
| **EXT-7-2** | Update Design Documents | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-7-2-update-design-documents) |
| **EXT-7-3** | Run Full Test Suite | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-7-3-run-full-test-suite) |
| **EXT-7-4** | Performance Verification | 🔵 | - | - | - | [details](EXTRACTION-TASK-DETAILS.md#task-ext-7-4-performance-verification) |

### Success Criteria (Phase 7)
- [ ] All projects have README.md documentation
- [ ] EXTRACTION-DESIGN.md reflects final state
- [ ] All tests pass (zero regressions)
- [ ] Performance within 5% of baseline
- [ ] Documentation complete

### Final Validation
- [ ] `dotnet test Samples.sln` - All tests pass
- [ ] `dotnet build Samples.sln` - Clean build
- [ ] BattleRoyale runs successfully
- [ ] CarKinem runs successfully
- [ ] Minimal example runs successfully

---

## Milestones

### Milestone 1: Foundation Ready
**Target:** End of Phase 1  
**Criteria:**
- ✅ New projects created
- ✅ Core interfaces defined
- ✅ Baseline tests passing

**Status:** ✅ Complete

---

### Milestone 2: Network Extraction Complete
**Target:** End of Phase 2  
**Criteria:**
- ✅ All network code in Cyclone plugin
- ✅ DdsIdAllocator implementing INetworkIdAllocator
- ✅ Translators working with NodeIdMapper
- ✅ NetworkGatewayModule moved

**Status:** 🔵 Not Started

---

### Milestone 3: Geographic Extraction Complete  
**Target:** End of Phase 3  
**Criteria:**
- ✅ All geographic code in separate module
- ✅ GeographicModule can be optionally used
- ✅ WGS84Transform tests passing

**Status:** 🔵 Not Started

---

### Milestone 4: Component Migration Complete
**Target:** End of Phase 4  
**Criteria:**
- ✅ Concrete components moved to applications
- ✅ BattleRoyale using local components
- ✅ All integration tests passing

**Status:** 🔵 Not Started

---

### Milestone 5: Core Simplified
**Target:** End of Phase 5  
**Criteria:**
- ✅ NetworkOwnership simplified
- ✅ Old files deleted from Core
- ✅ Core compiles cleanly
- ✅ Zero domain-specific code in Core

**Status:** 🔵 Not Started

---

### Milestone 6: Applications Updated
**Target:** End of Phase 6  
**Criteria:**
- ✅ All examples use manual dependency injection
- ✅ Minimal example demonstrates modularity
- ✅ All applications run successfully

**Status:** 🔵 Not Started

---

### Milestone 7: Extraction Complete ⭐
**Target:** End of Phase 7  
**Criteria:**
- ✅ Full test suite passes
- ✅ Documentation complete
- ✅ Performance verified
- ✅ **ModuleHost.Core is a generic game engine**

**Status:** 🔵 Not Started

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation | Status |
|------|-----------|--------|------------|--------|
| Breaking changes | High | High | Keep old files until Phase 5, maintain smoke tests | 🔵 |
| Test failures | Medium | High | Run tests after each task, update namespaces immediately | 🔵 |
| Performance regression | Low | Medium | Run benchmarks before/after, profile hot paths | 🔵 |
| Missing dependencies | Medium | Medium | Carefully review all usings, verify build after each task | 🔵 |
| Merge conflicts | Low | Low | Commit frequently, work on feature branch | 🔵 |

---

## Quality Gates

### Gate 1: After Phase 1
- [ ] Both new projects build successfully
- [ ] Core interfaces compile
- [ ] Smoke tests pass

**Status:** 🔵 Pending

---

### Gate 2: After Phase 5 (Critical Gate)
- [ ] ModuleHost.Core compiles cleanly
- [ ] Zero references to CycloneDDS in Core
- [ ] Zero geographic code in Core
- [ ] Zero concrete components in Core
- [ ] All Core unit tests pass

**Status:** 🔵 Pending

---

### Gate 3: Final Verification (After Phase 7)
- [ ] All tests pass (100% success rate)
- [ ] All examples run successfully
- [ ] Performance within 5% of baseline
- [ ] Documentation complete and accurate
- [ ] Code review approved

**Status:** 🔵 Pending

---

## Statistics

### Test Coverage

| Project | Unit Tests | Integration Tests | Total |
|---------|-----------|------------------|-------|
| ModuleHost.Core | TBD | TBD | TBD |
| ModuleHost.Network.Cyclone | 0 | 0 | 0 (target: 20+) |
| Fdp.Modules.Geographic | 0 | 0 | 0 (target: 10+) |
| Fdp.Examples.BattleRoyale | TBD | TBD | TBD |

### Code Metrics

| Metric | Before | After | Target |
|--------|--------|-------|--------|
| ModuleHost.Core LOC | TBD | TBD | -30% |
| Total Projects | 10 | 12 | +2 |
| Core Dependencies | TBD | 0 (FastCycloneDDS) | Zero domain deps |

---

## Notes and Decisions

### Decision Log

| Date | Decision | Rationale | Impact |
|------|----------|-----------|--------|
| 2026-01-30 | Create two separate modules (Network + Geographic) instead of one | Better separation of concerns, allows using network without GIS | Phase 3 added |
| - | - | - | - |

### Open Questions

| ID | Question | Status | Owner |
|----|----------|--------|-------|
| Q1 | Should we create Fdp.Components.Standard for shared components? | Open | - |
| Q2 | Keep original NetworkGatewayModule in Core during Phase 2-4? | Yes (for compatibility) | - |
| Q3 | Geographic module mandatory or optional for all examples? | Optional | - |

---

## Changelog

| Date | Version | Changes |
|------|---------|---------|
| 2026-01-30 | 1.0 | Initial task tracker created |

---

## Legend

- ✅ Complete
- ⏳ In Progress
- 🔴 Blocked
- 🔵 Not Started
- ⭐ Milestone
- 🚧 At Risk

