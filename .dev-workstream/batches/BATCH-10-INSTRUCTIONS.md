# BATCH-10: Release Candidate & Documentation

**Batch Number:** BATCH-10  
**Tasks:** EXT-7-1, EXT-7-2  
**Phase:** Phase 7 (Documentation & Handover)  
**Estimated Effort:** 2 Hours  
**Priority:** LOW  
**Dependencies:** BATCH-09 (Complete)  

---

## 📋 Onboarding & Workflow

### Context
Code is complete. We need to leave the repository in a good state for the next team or workstream.
Documentation is currently out of date (it still refers to Core containing Network logic).

### Objective
Update READMEs and Architecture documents to reflect the new state.

---

## ✅ Tasks

### Task 1: Update Architecture Documentation (EXT-7-2)
**Goal:** Update `ARCHITECTURE-NOTES.md` and `README.md`.

**Specs:**
1.  **Update `d:\Work\FDP-ModuleHost-Samples\docs\ARCHITECTURE-NOTES.md`**:
    - Describe the new 3-layer architecture:
        - **Kernel:** `ModuleHost.Core` (Generic ECS).
        - **Plugins:** `ModuleHost.Network.Cyclone`, `Fdp.Modules.Geographic`.
        - **Application:** `Fdp.Examples.BattleRoyale` (Component Definitions, Wiring).
    - Remove references to "Core Network Layer".

### Task 2: Update Project READMEs (EXT-7-1)
**Goal:** Ensure each project explains its purpose.

**Specs:**
1.  **ModuleHost.Core/README.md**: "Generic ECS Kernel."
2.  **ModuleHost.Network.Cyclone/README.md**: "CycloneDDS Network Plugin for ModuleHost."
3.  **Fdp.Modules.Geographic/README.md**: "Geospatial extensions for ModuleHost."

---

## 🧪 Testing Requirements

**Success Criteria:**
1.  Documentation accurately describes the codebase state.
2.  `EXTRACTION-TASK-TRACKER.md` updated to 100%.

**Deliverable:**
- Polished documentation.
