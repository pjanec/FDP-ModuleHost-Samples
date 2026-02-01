# Developer Onboarding Guide

Welcome to the **ModuleHost.Core Extraction Refactoring** project.
This guide ensures you have the correct environment and workflow to contribute effectively.

---

## 🏗️ Work Environment

### 1. Repository Structure
The repository contains two main solution areas:

- **`ModuleHost.sln`**: The core engine solution.
  - Location: `ModuleHost/`
  - Contains: `ModuleHost.Core`, `Fdp.Kernel`, `ModuleHost.Network.Cyclone`, and `Fdp.Modules.Geographic`.
  - **Primary Workspace** for extraction mechanism.

- **`Samples.sln`**: The example applications solution.
  - Location: Root (`.`)
  - Contains: `Fdp.Examples.*`, `CarKinem`, and references to Core.
  - **Validation Workspace** to ensure changes don't break consumers.

### 2. Architecture Overview
The project follows a **3-Layer Architecture**:
1.  **Kernel Layer** (`ModuleHost.Core`): Generic ECS engine. No domain logic.
2.  **Plugin Layer**:
    - `ModuleHost.Network.Cyclone`: CycloneDDS-based networking.
    - `Fdp.Modules.Geographic`: Geospatial transforms.
3.  **Application Layer** (`Fdp.Examples.*`): Domain-specific logic and component wiring.

### 3. External Dependencies
- **FastCycloneDDS**: The project references `FastCycloneDDS` components.
  - Ensure `..\FastCycloneDDS` or related paths are accessible if you need to debug dependencies, though mostly they are referenced via project or NuGet.
- **FD**: `fd` is available for file searching.
- **Ripgrep**: `rg` is available for text searching.

---

## 🚀 Build & Test Workflow

### Building
Always verify both solutions build cleanly:

```powershell
# Build Core Engine & Plugins
dotnet build ModuleHost/ModuleHost.sln

# Build Examples (Validates consumers)
dotnet build Samples.sln
```

### Testing
We use `xUnit` for testing. You must ensure tests pass in **both** scopes.

```powershell
# Run All Tests (Recommended)
dotnet test Samples.sln --nologo --verbosity minimal
```

> **💡 Pro Tip:** Use the "Smart Terminal" workflow to filter test output:
> ```powershell
> dotnet test ModuleHost/ModuleHost.sln --filter "FullyQualifiedName~Cyclone" --nologo --verbosity minimal
> ```

---

## 🛠️ The Extraction Mission (Completed)

We have successfully refactored `ModuleHost.Core` to be a generic game engine kernel.
**Current Goal:** Maintain the clean architecture and build new features (like Demo applications) on top of the plugins.

### Key Documents (Must Read)
1.  **[ARCHITECTURE-NOTES.md](../docs/ARCHITECTURE-NOTES.md)**: The definitive guide to the new architecture.
2.  **[EXTRACTION-TASK-TRACKER.md](../docs/EXTRACTION-TASK-TRACKER.md)**: History of the refactoring work.
3.  **Module READMEs**: Read `README.md` in each project root for specific details.

---

## 📦 Batch Workflow

We use a **Batch System** for tasks.
1. You receive a `BATCH-XX-INSTRUCTIONS.md` file.
2. You implement the tasks.
3. You create a `reports/BATCH-XX-REPORT.md` file.

**Report Requirements:**
- **Issues Encountered:** Technical blockers you solved.
- **Design Decisions:** Why you chose implementation X over Y.
- **Test Coverage:** Explicit proof of testing (names of tests added).

### ⚠️ Quality Standards
- **Zero Regressions:** All existing tests must pass.
- **Architecture Compliance:** Do NOT add domain logic (Position, Velocity) back into `ModuleHost.Core`.
- **Plugin Isolation:** Plugins should not depend on each other directly unless via Core abstractions.

---

## 🆘 Getting Help

If you are blocked:
1. Check `docs/` for relevant design docs.
2. Search the codebase for similar patterns (`grep_search`).
3. Create a `questions/BATCH-XX-QUESTIONS.md` file if clarification is needed.
