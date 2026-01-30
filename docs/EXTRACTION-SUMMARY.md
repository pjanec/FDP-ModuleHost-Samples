# Extraction Plan - FINAL with Network Demo

**Last Updated:** 2026-01-30 19:38  
**Status:** ✅ Complete - Ready for Implementation

---

## 📚 Complete Document Set (7 documents)

### Core Planning
1. **EXTRACTION-DESIGN.md** (21.4 KB) - Architecture
2. **EXTRACTION-TASK-DETAILS.md** (39.2 KB) - 29 tasks
3. **EXTRACTION-TASK-TRACKER.md** (15.9 KB) - Progress tracking

### Implementation Guidance  
4. **EXTRACTION-REFINEMENTS.md** (14.2 KB) ⚠️ - Critical warnings
5. **TASK-EXT-2-7-IdAllocatorServer.md** (6.8 KB) - ID server
6. **TASK-EXT-6-4-NetworkIntegrationDemo.md** (8.1 KB) 🆕 - P2P demo

### Summary
7. **EXTRACTION-SUMMARY.md** (you are here)

---

## 🆕 NEW: Network Integration Demo (EXT-6-4)

**User Request:**
> "Demo application for DDS network, two instances producing entities detected by each other, with geo translation, smoothing, testable console output"

**Solution: Peer-to-Peer Network Demo**

### Features
- **Two Instances:** Alpha (ID 100) and Bravo (ID 200)
- **Each Spawns:** 3 entities (Tank, Jeep, Helicopter)
- **Each Receives:** 3 entities from peer (total 6 visible)
- **Demonstrates:**
  - ✅ DDS networking (EntityMaster, EntityState)
  - ✅ Geographic transforms (WGS84 → Local Cartesian)
  - ✅ Network smoothing
  - ✅ Entity lifecycle
  - ✅ ID allocation
  - ✅ Peer discovery

### Console Output (Testable)
```
[STATUS] Frame snapshot:
  [LOCAL]  Tank         Pos: (523.1, -234.5) NetID: 1 Owner: 100
  [REMOTE] Tank         Pos: (-123.4, 234.5) NetID: 4 Owner: 200
[STATUS] Local: 3, Remote: 3
TEST_OUTPUT: LOCAL=3 REMOTE=3  ← Automated test parses this
```

### Automated Tests
```csharp
[Fact]
public async Task TwoInstances_ExchangeEntities_WithinTimeout()
{
    // Validates: TEST_OUTPUT: LOCAL=3 REMOTE=3
}
```

### How to Run
```bash
# Terminal 1: ID Server
dotnet run --project Fdp.Examples.IdAllocatorDemo

# Terminal 2: Alpha Node
dotnet run --project Fdp.Examples.NetworkDemo -- 100

# Terminal 3: Bravo Node
dotnet run --project Fdp.Examples.NetworkDemo -- 200
```

---

## 📊 Updated Statistics

| Metric | Value |
|--------|-------|
| **Total Documents** | 7 |
| **Total Tasks** | **30** (was 29) |
| **Phase 2 Tasks** | 7 (ID allocator + server) |
| **Phase 6 Tasks** | **4** (was 3, added P2P demo) |
| **Demo Apps** | 3 (IdAllocator, Network P2P, Minimal) |
| **Estimated Duration** | 14-18 days |

---

## 🎯 Complete Task List

### Phase 1: Foundation (4 tasks)
- EXT-1-1: Create Cyclone project
- EXT-1-2: Create Geographic project
- EXT-1-3: Define Core interfaces
- EXT-1-4: Migration smoke test

### Phase 2: Network Extraction (7 tasks)
- EXT-2-1: NodeIdMapper
- EXT-2-2: DDS Topics
- EXT-2-3: DdsIdAllocator ⚠️
- **EXT-2-4:** NetworkGatewayModule ⚠️
- EXT-2-5: Descriptor Translators
- EXT-2-6: TypeIdMapper ⚠️
- **EXT-2-7:** ID Allocator Server 🆕

### Phase 3: Geographic Extraction (4 tasks)
- EXT-3-1: Move geographic components
- EXT-3-2: Move geographic systems
- EXT-3-3: Move transforms
- EXT-3-4: Create GeographicModule

### Phase 4: Component Migration (4 tasks)
- EXT-4-1: BattleRoyale components
- EXT-4-2: Update BattleRoyale systems
- EXT-4-3: Create Fdp.Components.Standard (optional)
- EXT-4-4: Refactor Core tests (mock components)

### Phase 5: Core Simplification (3 tasks)
- EXT-5-1: Simplify NetworkOwnership
- EXT-5-2: Remove DescriptorOwnership
- EXT-5-3: Delete old files

### Phase 6: Application Updates (4 tasks)
- EXT-6-1: Update BattleRoyale bootstrap
- EXT-6-2: Update CarKinem
- EXT-6-3: Create minimal example
- **EXT-6-4: Network Integration Demo (P2P)** 🆕

### Phase 7: Cleanup (4 tasks)
- EXT-7-1: Update READMEs
- EXT-7-2: Update design docs
- EXT-7-3: Run full test suite
- EXT-7-4: Performance verification

---

## ✅ Validation Strategy

### Unit Tests
- Core tests (with mocks)
- Cyclone plugin tests
- Geographic module tests

### Integration Tests
- **ID Allocator:** Server + Client roundtrip (EXT-2-7)
- **Network Demo:** Peer-to-peer entity exchange (EXT-6-4)

### Demo Applications
1. **IdAllocatorDemo** - Shows chunked allocation, reset protocol
2. **NetworkDemo (P2P)** - Shows complete stack working end-to-end
3. **Minimal Example** - Shows Core without geographic module

### CI/CD Automation
```bash
# All demos produce testable output
dotnet test Fdp.Examples.NetworkDemo.Tests
# Validates: TEST_OUTPUT: LOCAL=3 REMOTE=3
```

---

## 🚀 Implementation Path

### Week 1: Foundation + Network
- **Days 1-2:** Phase 1 (Foundation)
- **Days 3-5:** Phase 2 (Network + ID Server + Tests)

### Week 2: Geographic + Components  
- **Days 6-7:** Phase 3 (Geographic module)
- **Days 8-9:** Phase 4 (Component migration + Core test mocks)

### Week 3: Simplify + Applications
- **Days 10-11:** Phase 5 (Core simplification)
- **Days 12-14:** Phase 6 (Applications + **Network Demo**)

### Week 3-4: Polish
- **Days 15-17:** Phase 7 (Cleanup + Documentation)

**Total: 14-18 days**

---

## 🎓 Key Design Decisions

### 1. TypeId Abstraction
- Core uses `int TypeId` (opaque)
- Network layer maps DIS → int
- ⚠️ Not deterministic (save games deferred to v2)

### 2. ID Allocation Protocol
- Chunked (100 IDs per request)
- Reset support (live → replay transitions)
- Server required (one per exercise session)

### 3. Network Demo Design
- **Two peer instances** (not client-server)
- **Structured output** (`TEST_OUTPUT:` markers)
- **Automated tests** validate entity exchange
- **Complete stack demo** (Core + Network + Geographic)

---

## 📖 Reading Order

### For Developers:
1. **EXTRACTION-SUMMARY.md** (this file) - Overview
2. **EXTRACTION-DESIGN.md** - Architecture
3. **EXTRACTION-REFINEMENTS.md** ⚠️ - Critical warnings
4. **EXTRACTION-TASK-DETAILS.md** - Task-by-task guide
5. **TASK-EXT-2-7** - ID server implementation
6. **TASK-EXT-6-4** - Network demo implementation

### During Implementation:
- Start each task from EXTRACTION-TASK-DETAILS.md
- If task has ⚠️, read REFINEMENTS section first
- Update EXTRACTION-TASK-TRACKER.md after each task

---

## ✅ Final Checklist

### Documentation
- ✅ All 7 documents created
- ✅ All tasks have references
- ✅ Critical warnings integrated (⚠️)
- ✅ Standalone task files for complex items

### Integration
- ✅ Refinements linked from tasks
- ✅ Demo validates complete stack
- ✅ Automated testing possible

### Completeness
- ✅ 30 tasks cover entire extraction
- ✅ ID allocator server included
- ✅ **Network P2P demo included** 🆕
- ✅ Geographic transforms demonstrated
- ✅ Testable output for CI/CD

---

**STATUS: ✅ COMPLETE AND READY**

All documents integrated. All critical warnings visible. Complete demo application designed with automated tests. Ready to begin Phase 1.

