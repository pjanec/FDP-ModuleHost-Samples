# BATCH-02: Type Mapping, Translators, and Gateway

**Batch Number:** BATCH-02  
**Tasks:** EXT-2-6, EXT-2-5, EXT-2-4  
**Phase:** Phase 2 (Network Layer Extraction)  
**Estimated Effort:** 6-8 Hours  
**Priority:** HIGH  
**Dependencies:** BATCH-01 (Complete)  

---

## 📋 Onboarding & Workflow

### Developer Instructions
With the foundation and topics in place, we now implement the "Brain" of the network layer. This batch focuses on **Data Translation**: converting between Core's generic components and the DDS topics you just created.

### Required Reading (IN ORDER)
1. **[EXTRACTION-TASK-DETAILS.md](../../docs/EXTRACTION-TASK-DETAILS.md)** - Review EXT-2-6, EXT-2-5, EXT-2-4.
2. **[EXTRACTION-REFINEMENTS.md](../../docs/EXTRACTION-REFINEMENTS.md)** - **CRITICAL:** Read "Warning 1: INetworkTopology namespace shift" and "Warning 2: TypeId Determinism".

### Source Code Location
- **Work Area:** `ModuleHost.Network.Cyclone/`
- **Tests:** `ModuleHost.Network.Cyclone.Tests/`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (Types):** Implement TypeIdMapper → Write Tests → **pass** ✅
2. **Task 2 (Translators):** Implement Translators → Write Tests → **pass** ✅
3. **Task 3 (Gateway):** Move NetworkGatewayModule → Write Tests → **pass** ✅

---

## ✅ Tasks

### Task 1: TypeIdMapper Service (EXT-2-6)
**Goal:** Map between Core's `int TypeId` and DDS's `ulong DisTypeValue`.

**Specs:**
- Create `ModuleHost.Network.Cyclone/Services/TypeIdMapper.cs`.
- Implement `GetCoreTypeId(ulong)` and `GetDISType(int)`.
- **CRITICAL:** You MUST add a `TODO` comment about determinism as per **EXTRACTION-REFINEMENTS.md**.

**Tests Required:**
- ✅ `GetCoreTypeId_NewDISType_ReturnsUniqueId`
- ✅ `GetCoreTypeId_SameDISType_ReturnsSameId`
- ✅ `BidirectionalMapping_Consistent`

---

### Task 2: Descriptor Translators (EXT-2-5)
**Goal:** Implement the `IDescriptorTranslator` classes that move data between DDS Topics (DataReader/Writer) and Core Entities (EntityRepository).

**Specs:**
- Create `ModuleHost.Network.Cyclone/Translators/`.
- Implement `EntityMasterTranslator.cs` (Handles `EntityMasterTopic`).
- Implement `EntityStateTranslator.cs` (Handles `EntityStateTopic`).
- **Logic:**
    - `PollIngress`: Read from DDS, use `NodeIdMapper` to resolve Owner, use `TypeIdMapper` to resolve Type, write to Core.
    - `ScanAndPublish`: Read from Core, use Mappers, write to DDS.

**Tests Required:**
- ✅ `EntityMasterTranslator_PollIngress_MapsOwnerAndTypeCorrectly`
- ✅ `EntityMasterTranslator_ScanAndPublish_WritesCorrectTopic`
- ✅ `EntityStateTranslator_PollIngress_UpdatesPosition`

---

### Task 3: Move NetworkGatewayModule (EXT-2-4)
**Goal:** Extract the main coordination module.

**Specs:**
- **Copy** `NetworkGatewayModule.cs` from Core to `ModuleHost.Network.Cyclone/Modules/`.
- **Refactor:**
    - Update Namespace to `ModuleHost.Network.Cyclone.Modules`.
    - Change dependencies to use `INetworkTopology` (from `Abstractions` if moved, or `Core.Network.Interfaces` if not yet moved). 
    - **Note:** Check where `INetworkTopology` currently lives. If it's in Core, use it from Core. Task details mention a namespace shift potential—be careful.
- **Do NOT delete** it from Core yet (that is Phase 5).

**Tests Required:**
- ✅ `Constructor_WithValidDependencies_Succeeds`
- ✅ `ProcessConstructionOrders_WithNoPeers_AcksImmediately`

---

## 🧪 Testing Requirements

**Total Expected New Tests:** >10

**Quality Gates:**
1.  **Strict Isolation:** Translators should NOT leak DDS types (`NetworkAppId`, `DISEntityType`) into Core components. They must convert to `int` immediately.
2.  **Thread Safety:** Mappers must stay thread-safe (check `NodeIdMapper` pattern).

---

## ⚠️ Common Pitfalls

1.  **Circular Dependencies:**
    - Ensure Translators depend on Mappers, not the other way around.
    - Mappers are Services.
2.  **Schema Attributes:**
    - When testing Translators, you are mocking the DataReader/DataWriter. Ensure you test the *logic* of translation, not the DDS middleware itself.

Good luck.
