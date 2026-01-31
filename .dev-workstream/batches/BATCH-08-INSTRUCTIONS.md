# BATCH-08: Restore Network Plugin Functionality

**Batch Number:** BATCH-08  
**Tasks:** FIX-NET-RESTORATION  
**Phase:** Phase 6 (Correction/Restoration)  
**Estimated Effort:** 6-8 Hours  
**Priority:** CRITICAL  
**Dependencies:** BATCH-07 (Complete)  

---

## 📋 Onboarding & Workflow

### Context
In BATCH-07, we deleted legacy types from Core (`Position`, `NetworkIdentity`, etc.).
The Cyclone Plugin (`ModuleHost.Network.Cyclone`) relied on these, so the Translators and Integration tests were deleted to make it compile.
Now we must **RESTORE** this functionality by defining these types **inside the Plugin** (Bridge Pattern).

### Objective
Make `ModuleHost.Network.Cyclone` a fully functional, self-contained Network Plugin that outputs generic network data.

---

## ✅ Tasks

### Task 1: Define Network Components (FIX-1)
**Goal:** Define the missing components inside the Plugin.

**Specs:**
1.  **Create Folder:** `ModuleHost.Network.Cyclone/Components/`
2.  **Create Components:**
    - `NetworkIdentity.cs`: Struct wrapping a value (GUID or similar).
    - `NetworkSpawnRequest.cs`: Struct containing `DisType` (ulong) and `OwnerId`.
    - `NetworkPosition.cs`: Struct `{ Vector3 Value; }`.
    - `NetworkOrientation.cs`: Struct `{ Quaternion Value; }`.
    - `NetworkVelocity.cs`: Struct `{ Vector3 Value; }`.
3.  **Rationale:** The Plugin writes to these "Shadow Components". The Application (e.g. BattleRoyale) will sync them to its own Local Components (`Position`, etc.).

### Task 2: Restore Translators (FIX-2)
**Goal:** Re-implement the Translators using the new Plugin Components.

**Specs:**
1.  **Re-create `EntityMasterTranslator.cs`:**
    - It must write `NetworkIdentity`, `NetworkOwnership` (Core), and `NetworkSpawnRequest` (Cyclone).
2.  **Re-create `EntityStateTranslator.cs`:**
    - It must map `EntityStateTopic` -> `NetworkPosition`, `NetworkVelocity`.
    - Do **NOT** rely on `ModuleHost.Core.Network.Position` (it doesn't exist).
3.  **Fix Dependencies:**
     - Reuse `NodeIdMapper` and `TypeIdMapper`.

### Reference Implementation (EntityMasterTranslator)
Since the file was deleted, use this structure. Note the use of `NetworkIdentity` (local shadow component).

```csharp
public class EntityMasterTranslator : IDescriptorTranslator
{
    private readonly NodeIdMapper _nodeMapper;
    private readonly TypeIdMapper _typeMapper;

    public void ScanAndPublish(ISimulationView view, DdsWriter<EntityMasterTopic> writer) {
        // Query generic "NetworkSpawnRequest" (which we just defined in Task 1)
        // Publish to DDS
    }

    public void ProcessIncoming(EntityRepository repo, ReadOnlySpan<EntityMasterTopic> samples) {
        foreach(var sample in samples) {
            // 1. Map ID -> Entity
            // 2. Add Component: NetworkIdentity { Value = sample.NetworkId }
            // 3. Add Component: NetworkOwnership { PrimaryOwnerId = ... }
            // 4. Add Component: NetworkSpawnRequest { DisType = ... }
        }
    }
}
```

### Task 3: Restore Integration Tests (FIX-3)
**Goal:** Bring back the 13 migration tests.

**Specs:**
1.  **Recover `ReliableInitializationScenarios.cs`** (and others):
    - Example content can be found in `BATCH-04` history or reconstructed.
    - Update them to reference `ModuleHost.Network.Cyclone.Components`.
2.  **Verification:**
    - Tests must pass.
    - They must verify that `NetworkPosition` is updated when a message arrives.

---

## 🧪 Testing Requirements

**Success Criteria:**
1.  `ModuleHost.Network.Cyclone` compiles.
2.  `ModuleHost.Network.Cyclone.Tests` passes (including restored Integration tests).
3.  `ModuleHost.Core` clearly remains clean (no new files added there).

**Deliverable:**
- A working Network Plugin that provides `NetworkPosition` data.
