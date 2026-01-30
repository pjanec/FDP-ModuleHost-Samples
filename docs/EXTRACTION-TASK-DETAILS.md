# ModuleHost.Core Extraction - Task Details

**Document Version:** 1.0  
**Date:** 2026-01-30  
**Reference**: See [EXTRACTION-DESIGN.md](EXTRACTION-DESIGN.md) for architectural vision

---

## Table of Contents

1. [Overview](#overview)
2. [Phase 1: Foundation Setup](#phase-1-foundation-setup)
3. [Phase 2: Network Layer Extraction](#phase-2-network-layer-extraction)
4. [Phase 3: Geographic Module Extraction](#phase-3-geographic-module-extraction)
5. [Phase 4: Component Migration](#phase-4-component-migration)
6. [Phase 5: Core Simplification](#phase-5-core-simplification)
7. [Phase 6: Example Application Updates](#phase-6-example-application-updates)
8. [Phase 7: Cleanup and Documentation](#phase-7-cleanup-and-documentation)

---

## Overview

### Transformation Strategy

This document describes the step-by-step transformation of the ModuleHost.Core from a domain-specific framework into a generic game engine kernel. The transformation is divided into **7 phases**, each containing **2-5 tasks** with clear success criteria.

### Execution Principles

1. **Incremental**: Each phase builds on the previous one
2. **Testable**: Each task has defined unit tests
3. **Reversible**: Git commits per task allow rollback
4. **Non-Breaking**: Existing tests must pass after each task (with namespace updates only)

### Reading This Document

- **🎯 Task ID**: Unique identifier (format: `EXT-<Phase>-<Task>`)
- **📋 Description**: What needs to be done
- **✅ Success Criteria**: Definition of done (unit tests listed)
- **🔧 Implementation Notes**: Technical guidance
- **📚 References**: Links to design document sections

---

## Phase 1: Foundation Setup

**Goal**: Create new project structures and establish interfaces in Core  
**Duration**: 1-2 days  
**Dependencies**: None

---

### Task EXT-1-1: Create ModuleHost.Network.Cyclone Project

**📋 Description**:
Create a new C# class library project for the Cyclone DDS network implementation.

**🔧 Implementation**:

1. Create project directory: `D:\Work\FDP-ModuleHost-Samples\ModuleHost.Network.Cyclone\`
2. Create `ModuleHost.Network.Cyclone.csproj`:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net8.0</TargetFramework>
       <Nullable>enable</Nullable>
       <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
     </PropertyGroup>

     <ItemGroup>
       <ProjectReference Include="..\ModuleHost.Core\ModuleHost.Core.csproj" />
       <ProjectReference Include="..\..\FastCycloneDdsCsharpBindings\src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj" />
       <ProjectReference Include="..\..\FastCycloneDdsCsharpBindings\src\CycloneDDS.Schema\CycloneDDS.Schema.csproj" />
     </ItemGroup>
   </Project>
   ```

3. Create folder structure:
   ```
   ModuleHost.Network.Cyclone\
   ├── Modules\
   ├── Services\
   ├── Topics\
   └── Translators\
   ```

4. Add to solution:
   ```bash
   dotnet sln ModuleHost.sln add ModuleHost.Network.Cyclone\ModuleHost.Network.Cyclone.csproj
   ```

**✅ Success Criteria**:
- ✅ Project builds successfully
- ✅ Project appears in solution explorer
- ✅ Can reference `ModuleHost.Core`
- ✅ Can reference `CycloneDDS.Runtime`

**📚 Reference**: [EXTRACTION-DESIGN.md § Project Structure](EXTRACTION-DESIGN.md#example-application-integration)

---

### Task EXT-1-2: Create Fdp.Modules.Geographic Project

**📋 Description**:
Create a new C# class library project for geographic/GIS functionality.

**🔧 Implementation**:

1. Create project directory: `D:\Work\FDP-ModuleHost-Samples\Fdp.Modules.Geographic\`
2. Create `Fdp.Modules.Geographic.csproj`:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net8.0</TargetFramework>
       <Nullable>enable</Nullable>
     </PropertyGroup>

     <ItemGroup>
       <ProjectReference Include="..\ModuleHost\ModuleHost.Core\ModuleHost.Core.csproj" />
       <ProjectReference Include="..\ModuleHost\FDP\Fdp.Kernel\Fdp.Kernel.csproj" />
     </ItemGroup>
   </Project>
   ```

3. Create folder structure:
   ```
   Fdp.Modules.Geographic\
   ├── Components\
   ├── Systems\
   └── Transforms\
   ```

4. Add to solution:
   ```bash
   dotnet sln Samples.sln add Fdp.Modules.Geographic\Fdp.Modules.Geographic.csproj
   ```

**✅ Success Criteria**:
- ✅ Project builds successfully
- ✅ Can reference `ModuleHost.Core`
- ✅ Can reference `Fdp.Kernel`

**📚 Reference**: [EXTRACTION-DESIGN.md § Project Structure](EXTRACTION-DESIGN.md#example-application-integration)

---

### Task EXT-1-3: Define Core Interfaces

**📋 Description**:
Create abstraction interfaces in ModuleHost.Core that will be implemented by plugins.

**🔧 Implementation**:

1. Create `ModuleHost.Core/Network/Interfaces/INetworkIdAllocator.cs`:
   ```csharp
   namespace ModuleHost.Core.Network.Interfaces
   {
       public interface INetworkIdAllocator : IDisposable
       {
           long AllocateId();
           void Reset(long startId = 0);
       }
   }
   ```

2. Create `ModuleHost.Core/Network/Interfaces/INetworkTopology.cs`:
   ```csharp
   namespace ModuleHost.Core.Network.Interfaces
   {
       public enum ReliableInitType
       {
           None,
           PhysicsServer,
           AllPeers
       }

       public interface INetworkTopology
       {
           IEnumerable<int> GetExpectedPeers(ReliableInitType type);
       }
   }
   ```

3. Verify `IDescriptorTranslator` exists (it should already be in Core)

**✅ Success Criteria**:
- ✅ Interfaces compile successfully
- ✅ XML documentation complete
- ✅ Unit test to verify interface can be mocked:
  - `Core.Tests/Network/Interfaces/NetworkInterfacesTests.cs`
  - Test: `INetworkIdAllocator_CanBeMocked`
  - Test: `INetworkTopology_CanBeMocked`

**📚 Reference**: [EXTRACTION-DESIGN.md § Interface Definitions](EXTRACTION-DESIGN.md#interface-definitions)

---

### Task EXT-1-4: Create Migration Smoke Test

**📋 Description**:
Create a baseline test that verifies current functionality before migration.

**🔧 Implementation**:

1. Create `ModuleHost.Core.Tests/Extraction/MigrationSmokeTests.cs`:
   ```csharp
   using Xunit;
   using ModuleHost.Core;
   using Fdp.Kernel;

   namespace ModuleHost.Core.Tests.Extraction
   {
       public class MigrationSmokeTests
       {
           [Fact]
           public void KernelCreation_BeforeMigration_Succeeds()
           {
               var world = new EntityRepository();
               using var kernel = new ModuleHostKernel(world);
               Assert.NotNull(kernel);
           }

           [Fact]
           public void ComponentRegistration_BeforeMigration_Succeeds()
           {
               var world = new EntityRepository();
               // This test will fail after we remove Position from Core
               // That's expected - we'll update it then
               world.RegisterComponent<ModuleHost.Core.Network.Position>();
               Assert.True(true);
           }
       }
   }
   ```

2. Run baseline tests:
   ```bash
   dotnet test ModuleHost.Core.Tests --filter "FullyQualifiedName~MigrationSmokeTests"
   ```

**✅ Success Criteria**:
- ✅ Tests compile
- ✅ Tests pass
- ✅ Serves as regression baseline

**📚 Reference**: [EXTRACTION-DESIGN.md § Migration Impact Analysis](EXTRACTION-DESIGN.md#migration-impact-analysis)

---

## Phase 2: Network Layer Extraction 🔵

**Goal**: Move DDS-specific network code to `ModuleHost.Network.Cyclone`  
**Duration**: 3-4 days  
**Dependencies**: Phase 1 complete

---

### Task EXT-2-1: Create NodeIdMapper Service

**📋 Description**:
Implement the service that maps between DDS's complex owner IDs (`NetworkAppId` struct) and Core's simple `int` owner IDs.

**🔧 Implementation**:

1. Create `ModuleHost.Network.Cyclone/Services/NodeIdMapper.cs`:
   ```csharp
   using System.Collections.Concurrent;
   using ModuleHost.Network.Cyclone.Topics;

   namespace ModuleHost.Network.Cyclone.Services
   {
       public class NodeIdMapper
       {
           private readonly ConcurrentDictionary<NetworkAppId, int> _externalToInternal = new();
           private readonly ConcurrentDictionary<int, NetworkAppId> _internalToExternal = new();
           private int _nextId = 1;

           public NodeIdMapper(int localDomain, int localInstance)
           {
               var local = new NetworkAppId { 
                   AppDomainId = localDomain, 
                   AppInstanceId = localInstance 
               };
               RegisterMapping(local, 1); // Reserve 1 for local
           }

           public int GetOrRegisterInternalId(NetworkAppId externalId) { /* ... */ }
           public NetworkAppId GetExternalId(int internalId) { /* ... */ }
           private void RegisterMapping(NetworkAppId ext, int intern) { /* ... */ }
       }
   }
   ```

2. Create unit tests in `ModuleHost.Network.Cyclone.Tests/Services/NodeIdMapperTests.cs`:
   - `LocalNode_AlwaysHasId1`
   - `NewExternalId_GetsUniqueInternalId`
   - `Bidirectional_Mapping_Consistent`
   - `ConcurrentAccess_ThreadSafe`

**✅ Success Criteria**:
- ✅ `NodeIdMapper` compiles
- ✅ All 4 unit tests pass
- ✅ Thread-safety verified

**📚 Reference**: [EXTRACTION-DESIGN.md § Core Principles #2](EXTRACTION-DESIGN.md#2-zero-knowledge-of-wire-format)

---

### Task EXT-2-2: Define DDS Topics

**📋 Description**:
Create DDS topic definitions using CycloneDDS.Schema DSL.

**🔧 Implementation**:

1. Create `ModuleHost.Network.Cyclone/Topics/CommonTypes.cs`:
   ```csharp
   using CycloneDDS.Schema;

   namespace ModuleHost.Network.Cyclone.Topics
   {
       [DdsStruct]
       public partial struct NetworkAppId : IEquatable<NetworkAppId>
       {
           [DdsId(0)] public int AppDomainId;
           [DdsId(1)] public int AppInstanceId;
           public bool Equals(NetworkAppId other) => 
               AppDomainId == other.AppDomainId && AppInstanceId == other.AppInstanceId;
       }

       public enum NetworkAffiliation : int
       {
           Neutral = 0,
           Friend_Blue = 1,
           Hostile_Red = 2,
           Unknown = 3
       }

       public enum NetworkLifecycleState : int
       {
           Ghost = 0,
           Constructing = 1,
           Active = 2,
           TearDown = 3
       }
   }
   ```

2. Create `ModuleHost.Network.Cyclone/Topics/EntityMasterTopic.cs`:
   ```csharp
   [DdsTopic("SST_EntityMaster")]
   [DdsQos(
       Reliability = DdsReliability.Reliable,
       Durability = DdsDurability.TransientLocal,
       HistoryKind = DdsHistoryKind.KeepLast,
       HistoryDepth = 100
   )]
   public partial struct EntityMasterTopic
   {
       [DdsKey, DdsId(0)] public long EntityId;
       [DdsId(1)] public NetworkAppId OwnerId;
       [DdsId(2)] public ulong DisTypeValue;
       [DdsId(3)] public int Flags;
   }
   ```

3. Create `EntityStateTopic.cs`, `EntityInfoTopic.cs`, `LifecycleStatusTopic.cs` similarly

4. Create tests:
   - `ModuleHost.Network.Cyclone.Tests/Topics/TopicSchemaTests.cs`
   - Test: `CommonTypes_ValidateEnums`
   - Test: `EntityMasterTopic_HasCorrectKeys`
   - Test: `NetworkAppId_Equality_Works`

**✅ Success Criteria**:
- ✅ All topics compile
- ✅ Schema validation tests pass
- ✅ DDS code generator can process topics (run `dotnet build`)

**📚 Reference**: [extracting-custom-stuff-out-of-modulehost-design-talk.md lines 600-900](extracting-custom-stuff-out-of-modulehost-design-talk.md)

---

### Task EXT-2-3: Implement DdsIdAllocator

**📋 Description**:
Implement `INetworkIdAllocator` using the DDS-based ID allocation protocol.

**⚠️ CRITICAL: Read [EXTRACTION-REFINEMENTS.md § ID Allocator Protocol](EXTRACTION-REFINEMENTS.md#2-id-allocator-protocol-detailed-spec) FIRST**

**🔧 Implementation**:

0. **READ REFINEMENTS DOC:**
   - Open [EXTRACTION-REFINEMENTS.md § 2. ID Allocator Protocol](EXTRACTION-REFINEMENTS.md#2-id-allocator-protocol-detailed-spec)
   - Understand: Request/Response types, Reset protocol, chunk management
   - See complete implementation example

1. Create `ModuleHost.Network.Cyclone/Topics/IdAllocTopics.cs`:
   - Define `IdRequest`, `IdResponse`, `IdStatus` structs
   - Define enums: `EIdRequestType`, `EIdResponseType`
   - Apply correct QoS settings (Reliable, Volatile/TransientLocal)

2. Create `ModuleHost.Network.Cyclone/Services/DdsIdAllocator.cs`:
   ```csharp
   using CycloneDDS.Runtime;
   using ModuleHost.Core.Network.Interfaces;

   namespace ModuleHost.Network.Cyclone.Services
   {
       public class DdsIdAllocator : INetworkIdAllocator
       {
           private readonly DdsWriter<IdRequestTopic> _writer;
           private readonly DdsReader<IdResponseTopic, IdResponseTopic> _reader;
           private readonly Queue<long> _availableIds = new();
           private const int CHUNK_SIZE = 100;

           public DdsIdAllocator(DdsParticipant participant, string clientId) { /* ... */ }
           public long AllocateId() { /* ... */ }
           public void Reset(long startId) { /* ... */ }
           private void ProcessResponses() { /* ... */ }
           public void Dispose() { /* ... */ }
       }
   }
   ```

3. Create tests:
   - `ModuleHost.Network.Cyclone.Tests/Services/DdsIdAllocatorTests.cs`
   - Test: `AllocateId_WithMockServer_ReturnsSequentialIds`
   - Test: `AllocateId_PoolExhausted_ThrowsException`
   - Test: `Reset_SendsGlobalRequest` (verify empty ClientId)
   - Test: `ResponseReset_ClearsPool_RequestsNew`

**✅ Success Criteria**:
- ✅ Implements `INetworkIdAllocator`
- ✅ All 4 unit tests pass (added Reset tests)
- ✅ Integration test with mock DDS server succeeds
- ✅ **Reviewed EXTRACTION-REFINEMENTS.md** ⚠️

**📚 References**: 
- **[EXTRACTION-REFINEMENTS.md § ID Allocator Protocol](EXTRACTION-REFINEMENTS.md#2-id-allocator-protocol-detailed-spec)** ⚠️ REQUIRED READING
- [extracting-custom-stuff-out-of-modulehost-design-talk.md lines 914-1161](extracting-custom-stuff-out-of-modulehost-design-talk.md)

---

### Task EXT-2-4: Move NetworkGatewayModule

**📋 Description**:
Move `NetworkGatewayModule` from Core to the Cyclone plugin, updating it to use interfaces.

**⚠️ WARNING: INetworkTopology namespace changed - see [EXTRACTION-REFINEMENTS.md § Warning 1](EXTRACTION-REFINEMENTS.md#warning-1-inetworktopology-namespace-shift)**

**🔧 Implementation**:

1. **Copy** `ModuleHost.Core/Network/NetworkGatewayModule.cs` → `ModuleHost.Network.Cyclone/Modules/NetworkGatewayModule.cs`

2. **CRITICAL: Update using statements:**
   ```csharp
   // ❌ WRONG (will break - interface moved!):
   // using ModuleHost.Core.Network.Interfaces;
   
   // ✅ CORRECT:
   using ModuleHost.Network.Cyclone.Abstractions; // INetworkTopology moved here!
   using ModuleHost.Core.Network.Interfaces;      // INetworkIdAllocator still here
   ```

3. Update constructor to accept interfaces:
   ```csharp
   public NetworkGatewayModule(
       int moduleId,
       int localNodeId,
       INetworkTopology topology,         // Now from Cyclone.Abstractions
       EntityLifecycleModule elm)         // From Core
   {
       // Implementation unchanged
   }
   ```

4. Update namespace:
   ```csharp
   namespace ModuleHost.Network.Cyclone.Modules
   ```

4. Create tests:
   - `ModuleHost.Network.Cyclone.Tests/Modules/NetworkGatewayModuleTests.cs`
   - Test: `Constructor_WithValidDependencies_Succeeds`
   - Test: `ProcessConstructionOrders_WithNoPeers_AcksImmediately`
   - Test: `ReceiveLifecycleStatus_AllPeersAcked_CompletesConstruction`

**✅ Success Criteria**:
- ✅ Module compiles in new location
- ✅ **using statements updated correctly** (INetworkTopology from Cyclone.Abstractions)
- ✅ All 3 unit tests pass
- ✅ **DO NOT DELETE** original yet (Phase 5)

**📚 References**: 
- **[EXTRACTION-REFINEMENTS.md § INetworkTopology Namespace Gotcha](EXTRACTION-REFINEMENTS.md#warning-1-inetworktopology-namespace-shift)** ⚠️ CRITICAL
- [EXTRACTION-DESIGN.md § Module Extraction Strategy #1](EXTRACTION-DESIGN.md#1-networkgatewaymodule)

---

### Task EXT-2-5: Create Descriptor Translators

**📋 Description**:
Implement translators that convert between DDS topics and Core components.

**🔧 Implementation**:

1. Create `ModuleHost.Network.Cyclone/Translators/EntityMasterTranslator.cs`:
   ```csharp
   public class EntityMasterTranslator : IDescriptorTranslator
   {
       private readonly NodeIdMapper _nodeMapper;
       private readonly int _localInternalId;

       public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, ISimulationView view)
       {
           foreach (var sample in reader.TakeSamples())
           {
               if (sample.Data is EntityMasterTopic desc)
               {
                   // Translate DDS NetworkAppId → Core int
                   int coreOwnerId = _nodeMapper.GetOrRegisterInternalId(desc.OwnerId);

                   // Create/update entity with simple int owner
                   cmd.AddComponent(entity, new NetworkOwnership 
                   { 
                       OwnerNodeId = coreOwnerId,
                       IsLocallyOwned = (coreOwnerId == _localInternalId)
                   });
               }
           }
       }

       public void ScanAndPublish(ISimulationView view, IDataWriter writer)
       {
           // Opposite direction: int → NetworkAppId
           NetworkAppId ddsOwner = _nodeMapper.GetExternalId(coreOwnership.OwnerNodeId);
           // ...
       }
   }
   ```

2. Create `EntityStateTranslator.cs`, `EntityInfoTranslator.cs`, `LifecycleStatusTranslator.cs`

3. Create tests:
   - `ModuleHost.Network.Cyclone.Tests/Translators/EntityMasterTranslatorTests.cs`
   - Test: `PollIngress_NewEntity_CreatesWithMappedOwnerId`
   - Test: `ScanAndPublish_LocalEntity_WritesCorrectDdsTopic`
   - Test: `NodeIdMapping_Bidirectional_Consistent`

**✅ Success Criteria**:
- ✅ All 4 translators compile
- ✅ Integration test: DDS → Core → DDS roundtrip succeeds
- ✅ NodeIdMapper integration verified

**📚 Reference**: [extracting-custom-stuff-out-of-modulehost-design-talk.md lines 1426-1662](extracting-custom-stuff-out-of-modulehost-design-talk.md)

---

### Task EXT-2-6: Create TypeIdMapper (CRITICAL)

**📋 Description**:
Create service that maps between domain-specific type identifiers (DIS, GUIDs, etc.) and Core's generic `int TypeId`.

**⚠️ LIMITATION: Not deterministic across sessions - see [EXTRACTION-REFINEMENTS.md § Warning 2](EXTRACTION-REFINEMENTS.md#warning-2-typeid-determinism-save-games)**

**🔧 Implementation**:

1. Create `ModuleHost.Network.Cyclone/Services/TypeIdMapper.cs`:
   ```csharp
   namespace ModuleHost.Network.Cyclone.Services
   {
       public class TypeIdMapper
       {
           // DDS uses ulong DISEntityType (64-bit)
           // Core uses int TypeId (32-bit)
           
           private readonly Dictionary<ulong, int> _disToCore = new();
           private readonly Dictionary<int, ulong> _coreToDis = new();
           
           public int GetCoreTypeId(ulong disType)
           {
               if (!_disToCore.TryGetValue(disType, out int id))
               {
                   id = _disToCore.Count + 1;
                   _disToCore[disType] = id;
                   _coreToDis[id] = disType;
               }
               return id;
           }
           
           public ulong GetDISType(int coreTypeId) => _coreToDis[coreTypeId];
       }
   }
   ```

2. Update `EntityMasterTranslator` to use `TypeIdMapper`:
   ```csharp
   public class EntityMasterTranslator : IDescriptorTranslator
   {
       private readonly TypeIdMapper _typeMapper;
       
       public void PollIngress(IDataReader reader, ...)
       {
           // Convert DIS type → Core type
           int coreTypeId = _typeMapper.GetCoreTypeId(desc.DisTypeValue);
           
           // Core only sees the int
           cmd.AddComponent(entity, new NetworkSpawnRequest {
               TypeId = coreTypeId,  // Generic int, NOT DISEntityType
               // ...
           });
       }
   }
   ```

3. Create tests:
   - `ModuleHost.Network.Cyclone.Tests/Services/TypeIdMapperTests.cs`
   - Test: `GetCoreTypeId_NewDISType_ReturnsUniqueId`
   - Test: `GetCoreTypeId_SameDISType_ReturnsSameId`
   - Test: `BidirectionalMapping_Consistent`

**✅ Success Criteria**:
- ✅ TypeIdMapper compiles
- ✅ All 3 unit tests pass
- ✅ **Core never sees `DISEntityType` or `ulong` types**
- ✅ ConstructionOrder uses `int TypeId` only
- ✅ **TODO comment added about determinism**

**🚨 Critical**: This prevents the DISEntityType leak into Core (Critical Flaw #1)

**📚 References**: 
- **[EXTRACTION-REFINEMENTS.md § TypeId Determinism Warning](EXTRACTION-REFINEMENTS.md#warning-2-typeid-determinism-save-games)** ⚠️ MUST ADD TODO
- [EXTRACTION-DESIGN.md § Type System Abstraction](EXTRACTION-DESIGN.md#type-system-abstraction-critical)

---

## Phase 3: Geographic Module Extraction

**Goal**: Move GIS functionality to separate module  
**Duration**: 2 days  
**Dependencies**: Phase 1 complete

---

### Task EXT-3-1: Move Geographic Components

**📋 Description**:
Extract all geographic components from `ModuleHost.Core/Geographic/` to new module.

**🔧 Implementation**:

1. **Copy** files to new locations:
   - `ModuleHost.Core/Geographic/GeographicComponents.cs` → `Fdp.Modules.Geographic/Components/GeographicComponents.cs`

2. Create individual component files:
   - `Fdp.Modules.Geographic/Components/PositionGeodetic.cs`
   - `Fdp.Modules.Geographic/Components/LatitudeLongitude.cs`

3. Update namespaces:
   ```csharp
   namespace Fdp.Modules.Geographic.Components
   ```

**✅ Success Criteria**:
- ✅ All components compile in new location
- ✅ No dependencies on ModuleHost.Core (except IModule)
- ✅ Unit test: `GeographicComponentsTests.PositionGeodetic_Roundtrip`

**📚 Reference**: [EXTRACTION-DESIGN.md § Modules to Remove from Core](EXTRACTION-DESIGN.md#modules-to-remove-from-core)

---

### Task EXT-3-2: Move Geographic Systems

**📋 Description**:
Move geographic transformation and smoothing systems.

**🔧 Implementation**:

1. **Copy** files:
   - `ModuleHost.Core/Geographic/NetworkSmoothingSystem.cs` → `Fdp.Modules.Geographic/Systems/GeodeticSmoothingSystem.cs`
   - `ModuleHost.Core/Geographic/CoordinateTransformSystem.cs` → `Fdp.Modules.Geographic/Systems/CoordinateTransformSystem.cs`

2. Rename `NetworkSmoothingSystem` → `GeodeticSmoothingSystem` (more descriptive)

3. Update to use `Fdp.Modules.Geographic.Components`

**✅ Success Criteria**:
- ✅ Systems compile
- ✅ Unit test: `GeodeticSmoothingSystem_AppliesSmoothing`
- ✅ Test: `CoordinateTransformSystem_ConvertsCoordinates`

**📚 Reference**: [EXTRACTION-DESIGN.md § Geographic Systems](EXTRACTION-DESIGN.md#2-geographic-systems)

---

### Task EXT-3-3: Move Transforms

**📋 Description**:
Move transform implementations and interface.

**🔧 Implementation**:

1. **Copy** files:
   - `ModuleHost.Core/Geographic/IGeographicTransform.cs` → `Fdp.Modules.Geographic/Transforms/IGeographicTransform.cs`
   - `ModuleHost.Core/Geographic/WGS84Transform.cs` → `Fdp.Modules.Geographic/Transforms/WGS84Transform.cs`

2. No code changes needed, just namespace updates

**✅ Success Criteria**:
- ✅ Interface and implementation compile
- ✅ Unit test: `WGS84Transform_LatLonToCartesian_Accurate`
- ✅ Test: `WGS84Transform_RoundTrip_WithinTolerance`

**📚 Reference**: [EXTRACTION-DESIGN.md § Geographic Systems](EXTRACTION-DESIGN.md#2-geographic-systems)

---

### Task EXT-3-4: Create GeographicModule

**📋 Description**:
Create the module class that registers geographic systems.

**🔧 Implementation**:

1. Create `Fdp.Modules.Geographic/GeographicModule.cs`:
   ```csharp
   using ModuleHost.Core.Abstractions;
   using Fdp.Modules.Geographic.Systems;
   using Fdp.Modules.Geographic.Transforms;

   namespace Fdp.Modules.Geographic
   {
       public class GeographicModule : IModule
       {
           public string Name => "GeographicServices";
           public ExecutionPolicy Policy => ExecutionPolicy.Synchronous();

           private readonly IGeographicTransform _transform;

           public GeographicModule(IGeographicTransform implementation)
           {
               _transform = implementation;
           }

           public void RegisterSystems(ISystemRegistry registry)
           {
               registry.RegisterSystem(new GeodeticSmoothingSystem(_transform));
               registry.RegisterSystem(new CoordinateTransformSystem(_transform));
           }

           public void Tick(ISimulationView view, float dt) { }
       }
   }
   ```

**✅ Success Criteria**:
- ✅ Module compiles
- ✅ Can be instantiated with `WGS84Transform`
- ✅ Unit test: `GeographicModule_Registration_RegistersBothSystems`

**📚 Reference**: [extracting-custom-stuff-out-of-modulehost-design-talk.md lines 1587-1623](extracting-custom-stuff-out-of-modulehost-design-talk.md)

---

## Phase 4: Component Migration

**Goal**: Move concrete components from Core to example projects  
**Duration**: 2 days  
**Dependencies**: Phase 2-3 complete

---

### Task EXT-4-1: Create Component Definitions in BattleRoyale

**📋 Description**:
Create concrete component definitions in the example application.

**🔧 Implementation**:

1. Create `Fdp.Examples.BattleRoyale/Components/Position.cs`:
   ```csharp
   namespace Fdp.Examples.BattleRoyale.Components
   {
       public struct Position
       {
           public float X;
           public float Y;
           public float Z; // or use System.Numerics.Vector3
       }
   }
   ```

2. Create `Velocity.cs`, `Health.cs`, etc.

3. Update `EntityFactory.RegisterAllComponents`:
   ```csharp
   public static void RegisterAllComponents(EntityRepository world)
   {
       world.RegisterComponent<Position>();
       world.RegisterComponent<Velocity>();
       world.RegisterComponent<Health>();
       // etc.
   }
   ```

**✅ Success Criteria**:
- ✅ Components compile in application
- ✅ `EntityFactory` test passes: `RegisterAllComponents_Succeeds`

**📚 Reference**: [EXTRACTION-DESIGN.md § Components to Remove from Core](EXTRACTION-DESIGN.md#components-to-remove-from-core)

---

### Task EXT-4-2: Update BattleRoyale to use Local Components

**📋 Description**:
Update all systems in BattleRoyale to use locally-defined components.

**🔧 Implementation**:

1. Update all `using` statements:
   ```csharp
   // Before
   using ModuleHost.Core.Network;

   // After
   using Fdp.Examples.BattleRoyale.Components;
   ```

2. Update systems (e.g., `PhysicsModule.cs`):
   ```csharp
   var query = view.Query()
       .With<Position>()  // Now from local namespace
       .With<Velocity>()
       .Build();
   ```

3. Run application to verify

**✅ Success Criteria**:
- ✅ BattleRoyale compiles
- ✅ All integration tests pass
- ✅ Application runs successfully

---

### Task EXT-4-3: Create Shared Components Library (Optional)

**📋 Description**:
Create an optional `Fdp.Components.Standard` library for common components.

**🔧 Implementation**:

1. Create `Fdp.Components.Standard/Position.cs`, etc.

2. Both BattleRoyale and other examples can reference this

**✅ Success Criteria**:
- ✅ Library compiles
- ✅ Can be used by multiple applications

**📚 Reference**: [EXTRACTION-DESIGN.md § Component Extraction Strategy](EXTRACTION-DESIGN.md#component-extraction-strategy)

---

### Task EXT-4-4: Refactor Core Unit Tests (CRITICAL)

**📋 Description**:
Create mock components in `ModuleHost.Core.Tests` to prevent compilation errors when concrete components are deleted from Core in Phase 5.

**🔧 Implementation**:

1. Create `ModuleHost.Core.Tests/Mocks/TestComponents.cs`:
   ```csharp
   namespace ModuleHost.Core.Tests.Mocks
   {
       // Mock components for testing Core infrastructure
       // These replace the real Position/Velocity that will be deleted
       
       public struct TestPosition
       {
           public float X;
           public float Y;
           public float Z;
       }
       
       public struct TestVelocity
       {
           public float X;
           public float Y;
       }
       
       public struct TestHealth
       {
           public int Value;
       }
   }
   ```

2. Update all Core unit tests:
   ```csharp
   // Before
   using ModuleHost.Core.Network;
   world.RegisterComponent<Position>();

   // After
   using ModuleHost.Core.Tests.Mocks;
   world.RegisterComponent<TestPosition>();
   ```

3. Update affected test files:
   - `ModuleHostKernelTests.cs`
   - `SystemSchedulerTests.cs`
   - `EntityRepositoryTests.cs`
   - Any test using `Position`, `Velocity`, etc.

4. Verify tests still pass:
   ```bash
   dotnet test ModuleHost.Core.Tests
   ```

**✅ Success Criteria**:
- ✅ Mock components created in test project
- ✅ All Core tests updated to use mock components
- ✅ All Core tests pass
- ✅ **Core tests no longer depend on concrete domain components**
- ✅ Preparation for Phase 5 deletions complete

**🚨 Critical**: This fixes the Test Project Paradox (Critical Flaw #2)

**📚 Reference**: User feedback - Critical Flaw #2

---

## Phase 5: Core Simplification

**Goal**: Simplify Core types and remove duplicates  
**Duration**: 2 days  
**Dependencies**: Phase 2-4 complete

---

### Task EXT-5-1: Simplify NetworkOwnership

**📋 Description**:
Simplify `NetworkOwnership` component to use simple `int` instead of complex structs.

**🔧 Implementation**:

1. Update `ModuleHost.Core/Network/NetworkOwnership.cs`:
   ```csharp
   namespace ModuleHost.Core.Network
   {
       /// <summary>
       /// Simple ownership component.
       /// OwnerNodeId is an opaque integer mapped by the network layer.
       /// </summary>
       public struct NetworkOwnership
       {
           public int OwnerNodeId;
           public bool IsLocallyOwned;
       }
   }
   ```

2. Remove `PrimaryOwnerId` and `LocalNodeId` separate fields

3. Update `EntityLifecycleModule` if it references ownership

**✅ Success Criteria**:
- ✅ Simplified component compiles
- ✅ All Core tests pass
- ✅ Unit test: `NetworkOwnership_SimplifiedStructure_Works`

**📚 Reference**: [EXTRACTION-DESIGN.md § Simplified Core NetworkOwnership](EXTRACTION-DESIGN.md#simplified-core-networkownership)

---

### Task EXT-5-2: Remove DescriptorOwnership from Core

**📋 Description**:
Move `DescriptorOwnership` to the Cyclone plugin (it's DDS-specific).

**🔧 Implementation**:

1. **Copy** to `ModuleHost.Network.Cyclone/Components/DescriptorOwnership.cs`

2. Update namespace:
   ```csharp
   namespace ModuleHost.Network.Cyclone.Components
   ```

3. **Delete** from `ModuleHost.Core/Network/NetworkComponents.cs`

**✅ Success Criteria**:
- ✅ Core compiles without `DescriptorOwnership`
- ✅ Cyclone plugin tests pass

---

### Task EXT-5-3: Delete Old Files from Core

**📋 Description**:
Remove files that were copied to new locations in Phase 2-3.

**🔧 Implementation**:

1. Delete from `ModuleHost.Core/`:
   - `Geographic/` (entire folder)
   - `Network/NetworkGatewayModule.cs`
   - `Network/EntityMasterDescriptor.cs` (if exists)
   - `Network/Position.cs`, `Velocity.cs` (concrete components)

2. Verify Core still compiles

**✅ Success Criteria**:
- ✅ Core compiles after deletions
- ✅ All Core unit tests pass
- ✅ No broken references

**📚 Reference**: [EXTRACTION-DESIGN.md § Migration Path](EXTRACTION-DESIGN.md#migration-path)

---

## Phase 6: Example Application Updates

**Goal**: Update all example applications to use new structure  
**Duration**: 2-3 days  
**Dependencies**: Phase 5 complete

---

### Task EXT-6-1: Update BattleRoyale Bootstrap

**📋 Description**:
Update `Fdp.Examples.BattleRoyale/Program.cs` to manually wire dependencies.

**🔧 Implementation**:

1. Add references:
   ```xml
   <ItemGroup>
     <ProjectReference Include="..\ModuleHost.Network.Cyclone\ModuleHost.Network.Cyclone.csproj" />
     <ProjectReference Include="..\Fdp.Modules.Geographic\Fdp.Modules.Geographic.csproj" />
   </ItemGroup>
   ```

2. Update `Main()`:
   ```csharp
   static void Main()
   {
       var world = new EntityRepository();
       EntityFactory.RegisterAllComponents(world);

       var kernel = new ModuleHostKernel(world);

       // Network setup
       var participant = new DdsParticipant(domainId: 0);
       var nodeMapper = new NodeIdMapper(appDomain: 1, appInstance: 100);
       var idAllocator = new DdsIdAllocator(participant, "Client_100");
       var topology = new StaticNetworkTopology(localNodeId: 1);

       var networkModule = new CycloneNetworkModule(
           participant, nodeMapper, idAllocator, topology,
           kernel.GetEntityLifecycleModule()
       );
       kernel.RegisterModule(networkModule);

       // Optional: Geographic
       var geoModule = new GeographicModule(
           new WGS84Transform(originLat: 52.0, originLon: 13.0)
       );
       kernel.RegisterModule(geoModule);

       kernel.Initialize();
       
       // Run loop...
   }
   ```

**✅ Success Criteria**:
- ✅ Application compiles
- ✅ Application runs successfully
- ✅ All existing functionality works

**📚 Reference**: [EXTRACTION-DESIGN.md § Application Bootstrap Example](EXTRACTION-DESIGN.md#application-bootstrap-example)

---

### Task EXT-6-2: Update CarKinem Example

**📋 Description**:
Apply same pattern to `Fdp.Examples.CarKinem`.

**🔧 Implementation**:

1. Create local components
2. Update bootstrap
3. Decide: Use Geographic module or not?

**✅ Success Criteria**:
- ✅ CarKinem compiles and runs

---

### Task EXT-6-3: Create Minimal Example (No Geographic)

**📋 Description**:
Create a new minimal example that doesn't use geographic module.

**🔧 Implementation**:

1. Create `Fdp.Examples.Minimal/Program.cs`:
   ```csharp
   var kernel = new ModuleHostKernel(world);
   var networkModule = new CycloneNetworkModule(...);
   kernel.RegisterModule(networkModule);
   // No GeographicModule registered!
   kernel.RegisterGlobalSystem(new SimpleSmoothingSystem());
   kernel.Initialize();
   ```

**✅ Success Criteria**:
- ✅ Runs without geographic module
- ✅ Demonstrates modularity

**📚 Reference**: [EXTRACTION-DESIGN.md § Alternative Bootstrap](EXTRACTION-DESIGN.md#alternative-bootstrap-no-geographic-custom-smoothing)

---

## Phase 7: Cleanup and Documentation

**Goal**: Polish, document, and verify extraction  
**Duration**: 1-2 days  
**Dependencies**: Phase 6 complete

---

### Task EXT-7-1: Update README Files

**📋 Description**:
Create README.md for each new project explaining purpose and usage.

**🔧 Implementation**:

1. Create `ModuleHost.Network.Cyclone/README.md`
2. Create `Fdp.Modules.Geographic/README.md`
3. Update `ModuleHost/README.md` to reflect new structure

**✅ Success Criteria**:
- ✅ All READMEs complete
- ✅ Include quickstart examples

---

### Task EXT-7-2: Update Design Documents

**📋 Description**:
Ensure design documents reflect final state.

**🔧 Implementation**:

1. Review `EXTRACTION-DESIGN.md` for accuracy
2. Add "Post-Extraction Architecture" diagram
3. Document any deviations from original plan

**✅ Success Criteria**:
- ✅ Documentation matches implementation

---

### Task EXT-7-3: Run Full Test Suite

**📋 Description**:
Verify all tests pass across all projects.

**🔧 Implementation**:

```bash
dotnet test Samples.sln --verbosity minimal
```

**✅ Success Criteria**:
- ✅ All Core tests pass
- ✅ All Cyclone plugin tests pass
- ✅ All Geographic module tests pass
- ✅ All example application tests pass
- ✅ **Zero regressions**

---

### Task EXT-7-4: Performance Verification

**📋 Description**:
Verify no performance degradation from extraction.

**🔧 Implementation**:

1. Run benchmarks (if they exist)
2. Verify ECS hot paths unchanged
3. Measure module initialization time

**✅ Success Criteria**:
- ✅ No regression in ECS performance
- ✅ Module initialization within 5% of baseline

---

## Appendix A: Useful Commands

### Build Commands

```bash
# Build entire solution
dotnet build Samples.sln

# Build specific project
dotnet build ModuleHost\ModuleHost.Core\ModuleHost.Core.csproj

# Clean
dotnet clean Samples.sln
```

### Test Commands

```bash
# Run all tests
dotnet test Samples.sln

# Run specific test project
dotnet test ModuleHost.Core.Tests

# Run specific test class
dotnet test --filter "FullyQualifiedName~MigrationSmokeTests"

# Run with coverage
dotnet test /p:CollectCoverage=true
```

### Project Commands

```bash
# Add project to solution
dotnet sln Samples.sln add NewProject\NewProject.csproj

# Add project reference
dotnet add ModuleHost.Network.Cyclone\ModuleHost.Network.Cyclone.csproj reference ModuleHost\ModuleHost.Core\ModuleHost.Core.csproj
```

---

## Appendix B: Task Summary

| Phase | Tasks | Estimated Duration |
|-------|-------|-------------------|
| Phase 1: Foundation | 4 tasks | 1-2 days |
| Phase 2: Network Extraction | 5 tasks | 3-4 days |
| Phase 3: Geographic Extraction | 4 tasks | 2 days |
| Phase 4: Component Migration | 3 tasks | 2 days |
| Phase 5: Core Simplification | 3 tasks | 2 days |
| Phase 6: Application Updates | 3 tasks | 2-3 days |
| Phase 7: Cleanup | 4 tasks | 1-2 days |
| **Total** | **26 tasks** | **13-17 days** |

---

## Appendix C: Risk Mitigation

### Risk: Breaking Changes

**Mitigation**: 
- Keep old files until Phase 5
- Git commit after each task
- Maintain smoke tests

### Risk: Performance Regression

**Mitigation**:
- Run benchmarks before/after
- Profile hot paths
- Verify ECS unchanged

### Risk: Test Failures

**Mitigation**:
- Run tests after each task
- Update namespace references immediately
- Maintain regression baseline

