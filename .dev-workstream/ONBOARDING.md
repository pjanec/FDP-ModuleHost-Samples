# Developer Onboarding Guide

Welcome to the **ModuleHost.Core Extraction Refactoring** project.
This guide ensures you have the correct environment and workflow to contribute effectively.

---

## 🏗️ Work Environment

### 1. Repository Structure
The repository contains two main solution areas:

- **`ModuleHost.sln`**: The core engine solution.
  - Location: `ModuleHost/`
  - Contains: `ModuleHost.Core`, `Fdp.Kernel`, and Core Unit Tests.
  - **Primary Workspace** for extraction mechanism.

- **`Samples.sln`**: The example applications solution.
  - Location: Root (`.`)
  - Contains: `Fdp.Examples.*`, `CarKinem`, and references to Core.
  - **Validation Workspace** to ensure changes don't break consumers.

### 2. External Dependencies
- **FastCycloneDDS**: The project references `FastCycloneDDS` components.
  - Ensure `..\FastCycloneDDS` or related paths are accessible if you need to debug dependencies, though mostly they are referenced via project or NuGet.
- **FD**: `fd` is available for file searching.
- **Ripgrep**: `rg` is available for text searching.

---

## 🚀 Build & Test Workflow

### Building
Always verify both solutions build cleanly:

```powershell
# Build Core Engine
dotnet build ModuleHost/ModuleHost.sln

# Build Examples (Validates consumers)
dotnet build Samples.sln
```

### Testing
We use `xUnit` for testing. You must ensure tests pass in **both** scopes.

```powershell
# Run Core Tests (The primary suite for extraction work)
dotnet test ModuleHost/ModuleHost.sln

# Run Integration/Example Tests
dotnet test Samples.sln
```

> **💡 Pro Tip:** Use the "Smart Terminal" workflow to filter test output:
> ```powershell
> dotnet test ModuleHost/ModuleHost.sln --nologo --verbosity minimal
> ```

---

## 🛠️ The Extraction Mission

We are refactoring `ModuleHost.Core` to be a generic game engine kernel.
**Your Goal:** Remove domain-specific (DDS, Geographic, Concrete Components) code from Core and move them to plugins/modules.

### Key Documents (Must Read)
1. **[EXTRACTION-TASK-TRACKER.md](../docs/EXTRACTION-TASK-TRACKER.md)**: The master plan.
2. **[EXTRACTION-DESIGN.md](../docs/EXTRACTION-DESIGN.md)**: The architectural vision.
3. **[EXTRACTION-REFINEMENTS.md](../docs/EXTRACTION-REFINEMENTS.md)**: Critical technical details and warnings.

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
- **Zero Regressions:** Existing tests must pass.
- **Namespace Hygiene:** Verify namespaces match the new folder structure.
- **Test Reality:** Tests must verify *behavior* (values, state changes), not just compilation.

---

## 🆘 Getting Help

If you are blocked:
1. Check `docs/` for relevant design docs.
2. Search the codebase for similar patterns (`grep_search`).
3. Create a `questions/BATCH-XX-QUESTIONS.md` file if clarification is needed.
