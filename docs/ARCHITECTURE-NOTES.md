# ARCHITECTURE-NOTES

## Overview

The FDP-ModuleHost-Samples project demonstrates a **three-layer architecture** for building high-performance, distributed simulation systems:

1. **Kernel Layer** - Generic ECS engine (`ModuleHost.Core`)
2. **Plugin Layer** - Domain-specific functionality (Network, Geographic)
3. **Application Layer** - Domain-specific simulations and component schemas

---

## Layer 1: Kernel (ModuleHost.Core)

**Purpose:** Generic Entity-Component-System (ECS) kernel with module orchestration.

**Responsibilities:**
- Entity and component lifecycle management
- Module registration, scheduling, and execution
- Snapshot isolation (Double Buffer, On-Demand strategies)
- Command buffer pattern for safe mutations
- Event accumulation and history

**Key Abstractions:**
- `IModule` - Module interface
- `ISimulationView` - Read-only snapshot interface
- `IModuleSystem` - System execution interface
- `ISnapshotProvider` - Snapshot strategy interface
- `ExecutionPolicy` - Module execution configuration

**Does NOT contain:**
- Network-specific logic
- Domain-specific components (Position, Velocity, etc.)
- DDS or CycloneDDS integration

---

## Layer 2: Plugins

Plugins extend the kernel with specific functionality. They define their own components, systems, and modules.

### ModuleHost.Network.Cyclone

**Purpose:** DDS-based networking plugin using CycloneDDS.

**Provides:**
- `NetworkPosition`, `NetworkIdentity`, `NetworkOwnership` components
- `NetworkGatewayModule` - Manages network lifecycle
- `EntityMasterTranslator`, `EntityStateTranslator` - DDS bridges
- `DdsIdAllocator` - Network entity ID allocation
- CycloneDDS topic definitions

**Dependencies:**
- `ModuleHost.Core` (Kernel APIs)
- `CycloneDDS.Runtime` (DDS implementation)

### Fdp.Modules.Geographic

**Purpose:** Geospatial coordinate transformations.

**Provides:**
- Coordinate system abstractions
- Geographic projection systems
- Spatial indexing components

**Dependencies:**
- `ModuleHost.Core` (Kernel APIs)

---

## Layer 3: Applications

Applications combine plugins and define domain-specific components.

### Fdp.Examples.BattleRoyale

**Purpose:** Battle Royale simulation demonstrating fast/slow modules and network integration.

**Defines:**
- `Position`, `Velocity`, `Health`, `AIState` (Local components)
- `AIModule`, `PhysicsModule`, `AnalyticsModule`
- `NetworkSyncSystem` - Bridges local and network state

**Uses:**
- `ModuleHost.Core` - ECS kernel
- `ModuleHost.Network.Cyclone` - Networking
- Wires network components to local simulation

### Fdp.Examples.CarKinem

**Purpose:** Vehicle kinematics simulation with geographic coordinates.

**Uses:**
- `ModuleHost.Core` - ECS kernel
- `Fdp.Modules.Geographic` - Coordinate transforms

---

## Design Principles

### 1. Separation of Concerns
- **Kernel** is domain-agnostic
- **Plugins** are reusable across applications
- **Applications** define domain schemas

### 2. Plugin Isolation
- Plugins never reference each other directly
- Integration happens at the application layer
- Example: `BattleRoyale` bridges `Position` (local) and `NetworkPosition` (plugin)

### 3. Extensibility
- New plugins can be added without modifying Core
- Applications choose which plugins to use
- Plugins can be versioned independently

---

## Migration from Legacy Architecture

**Before (Batch 0-8):**
- Network logic was in `ModuleHost.Core`
- `EntityStateDescriptor`, DDS types in Core
- Tight coupling between kernel and networking

**After (Batch 9+):**
- Network logic in `ModuleHost.Network.Cyclone` plugin
- Core exports only abstractions (`INetworkTopology`, `INetworkIdAllocator`)
- Applications wire components explicitly

---

## Future Directions

- Additional plugins: `ModuleHost.Network.Enet`, `Fdp.Modules.Physics`
- Plugin discovery and hot-loading
- Cross-plugin communication via shared event types
