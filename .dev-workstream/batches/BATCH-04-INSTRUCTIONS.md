# BATCH-04: Lift & Shift - Network Layer Extraction

**Batch Number:** BATCH-04  
**Tasks:** EXT-NET-MOVE, EXT-NET-TESTS  
**Phase:** Phase 2 (Network Layer Extraction)  
**Estimated Effort:** 4-6 Hours  
**Priority:** HIGH  
**Dependencies:** BATCH-03 (Core Fixed)  

---

## 📋 Onboarding & Workflow

### Context
Great job fixing the Core logic. However, the **Network Implementation (Translators & Gateway)** is currently sitting inside `ModuleHost.Core`.
The goal of this project is to **EXTRACT** that code, so Core has zero knowledge of "EntityMasterTranslator" or "Cyclone".

### Objective
Move the network logic from Core to the Cyclone project, and migrate the relevant tests.

### Source Code Location
- **Source (To Delete):** `ModuleHost/ModuleHost.Core/Network/`
- **Destination:** `ModuleHost.Network.Cyclone/`

---

## ✅ Tasks

### Task 1: Move Translators (EXT-NET-MOVE-1)
**Goal:** Move translator implementations to the plugin.

**Specs:**
1.  **Move** the entire `Translators` folder:
    - From: `ModuleHost/ModuleHost.Core/Network/Translators/`
    - To: `ModuleHost.Network.Cyclone/Translators/`
2.  **Update Namespaces:**
    - Change `ModuleHost.Core.Network.Translators` → `ModuleHost.Network.Cyclone.Translators`
3.  **Fix Dependencies:**
    - Ensure they reference `ModuleHost.Network.Cyclone.Topics` (DDS structs) instead of Core descriptors where applicable.
    - If `EntityMasterDescriptor` is used, consider if it should be `EntityMasterTopic`. (Note: If `IDataReader` yields DDS types, you must use DDS types).

**Verification:**
- `ModuleHost.Network.Cyclone` compiles.

---

### Task 2: Move NetworkGatewayModule (EXT-NET-MOVE-2)
**Goal:** Finalize the move of the Gateway.

**Specs:**
1.  **Check** `ModuleHost.Network.Cyclone/Modules/NetworkGatewayModule.cs`:
    - Does it have the FIXES you made in BATCH-03? (e.g. ACK logic, Ghost handling).
    - If not, copy the fixed logic from Core.
2.  **Delete** `ModuleHost/ModuleHost.Core/Network/NetworkGatewayModule.cs`.
    - ⚠️ **Impact:** This will break `ModuleHost.Core` if it references the concrete class.
    - `ModuleHost.Core` should only use `IModule` or `INetworkTopology` interfaces.
    - If `ModuleHostKernel` references it manually, remove it (it should be injected/registered by the App).

---

### Task 3: Migrate Tests (EXT-NET-TESTS)
**Goal:** Move integration tests that depend on the concrete Network implementation.

**Specs:**
1.  **Identify** tests in `ModuleHost.Core.Tests` that fail after deleting Gateway/Translators.
    - Likely candidates: `NetworkELMIntegrationTests`, `NetworkELMIntegrationScenarios`.
2.  **Move** these tests to `ModuleHost.Network.Cyclone.Tests`.
3.  **Refactor** tests to verify the *Plugin* behavior, not Core behavior.

**Verification:**
- ✅ `ModuleHost.Core.Tests` passes (Clean, generic kernel tests only).
- ✅ `ModuleHost.Network.Cyclone.Tests` passes (Includes the migrated 13 tests).

---

## 🧪 Testing Requirements

**Success Criteria:**
1.  **Separation:** `ModuleHost.Core` must **NOT** contain:
    - `NetworkGatewayModule.cs`
    - `Translators/` folder
    - References to `CycloneDDS`.
2.  **Stability:** All tests must pass.
3.  **Structure:** `ModuleHost.Network.Cyclone` must be a self-contained plugin.

Good luck. Clean architecture is the goal.
