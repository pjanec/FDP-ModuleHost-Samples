# BATCH-03: Fix Core & Complete Network Layer

**Batch Number:** BATCH-03  
**Tasks:** FIX-CORE-NET, EXT-2-3  
**Phase:** Phase 2 (Network Layer Extraction)  
**Estimated Effort:** 6-8 Hours  
**Priority:** CRITICAL  
**Dependencies:** BATCH-02 (Complete)  

---

## 📋 Onboarding & Workflow

### Developer Instructions
**Priority Shift:** Before we proceed with extraction, we must stabilize the Core. The reported 13 failures in `ModuleHost.Core.Tests` indicate broken baseline functionality. We cannot extract broken code.

**Your Mission:**
1.  **Diagnose & Fix** the pre-existing failures in `ModuleHost.Core`.
2.  **Propagate** any logic fixes to the new `ModuleHost.Network.Cyclone` code (since we copied `NetworkGatewayModule` from Core).
3.  **Complete** the Network Layer by implementing `DdsIdAllocator`.

### Required Reading (IN ORDER)
1.  **[EXTRACTION-REFINEMENTS.md](../../docs/EXTRACTION-REFINEMENTS.md)** - Logic for ID Allocator.

### Source Code Location
- **Fixes:** `ModuleHost/ModuleHost.Core/` & `ModuleHost.Core.Tests/`
- **Propagation:** `ModuleHost.Network.Cyclone/`
- **Implementation:** `ModuleHost.Network.Cyclone/Services/DdsIdAllocator.cs`

---

## 🔄 MANDATORY WORKFLOW: Fix-Then-Feature

1.  **Task 1 (Fix):** Analyze Core Failures → Fix Logic → **Core Tests Pass** ✅
2.  **Task 2 (Sync):** Apply same fixes to Cyclone `NetworkGatewayModule` → **Cyclone Tests Pass** ✅
3.  **Task 3 (Feature):** Implement DdsIdAllocator → Write Tests → **pass** ✅

---

## ✅ Tasks

### Task 1: Fix Core Network Tests (Priority 1)
**Goal:** Resolve the 13 failing tests in verifying Network/ELM integration.

**Focus Areas (from Report):**
- `NetworkELMIntegrationTests`
- `NetworkELMIntegrationScenarios`
- `ReliableInitializationTests`
- Issues likely involve `NetworkGatewayModule` logic, ACK handling, or Ghost state transitions.

**Specs:**
- Run `dotnet test ModuleHost\ModuleHost.Core.Tests\ModuleHost.Core.Tests.csproj` to reproduce.
- Fix the bugs in `ModuleHost.Core`.
- **Constraint:** Do NOT disable tests. The functionality must work.

**Verification:**
- ✅ All `ModuleHost.Core.Tests` passed (0 failures).

---

### Task 2: Propagate Fixes to Cyclone Module
**Goal:** Ensure the new plugin doesn't inherit the old bugs.

**Specs:**
- Review the changes you made to `ModuleHost.Core.Network.NetworkGatewayModule`.
- Apply equivalent fixes to `ModuleHost.Network.Cyclone.Modules.NetworkGatewayModule`.
- Note: The Cyclone version uses `ModuleHost.Network.Cyclone.Abstractions` (or Core interface) for Topology, so adapt carefully.

**Verification:**
- ✅ `ModuleHost.Network.Cyclone.Tests` still passing.

---

### Task 3: Implement DdsIdAllocator (EXT-2-3)
**Goal:** Implement the distributed ID allocation protocol.

**Specs:**
- Follow **EXT-2-3** in [EXTRACTION-TASK-DETAILS.md](../../docs/EXTRACTION-TASK-DETAILS.md).
- Create `ModuleHost.Network.Cyclone/Topics/IdAllocTopics.cs` (Request, Response structs).
- Implement `ModuleHost.Network.Cyclone/Services/DdsIdAllocator.cs` implementing `INetworkIdAllocator`.
- **Constraint:** Must handle "Reset" signal as per [EXTRACTION-REFINEMENTS.md](../../docs/EXTRACTION-REFINEMENTS.md).

**Tests Required:**
- ✅ `AllocateId_WithMockServer_ReturnsSequentialIds`
- ✅ `Reset_SendsGlobalRequest`
- ✅ `ResponseReset_ClearsPool_RequestsNew`

---

## 🧪 Testing Requirements

**Total Expected Status:**
- **Core:** 100% Passing (Recovery)
- **Cyclone:** 100% Passing (Stability + New Feature)

**Quality Gates:**
1.  **Core Stability:** If Core tests fail, the batch is rejected.
2.  **Code Consistency:** Logic between Core and Cyclone Gateway modules must be aligned (until we delete Core's version later).

Good luck. Stabilize the patient.
