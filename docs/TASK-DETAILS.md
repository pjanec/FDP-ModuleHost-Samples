# FDP Engine Refactoring - Task Details

**Version:** 1.0  
**Date:** 2026-02-04

This document provides detailed specifications for each task in the FDP refactoring project. Each task includes:
- **ID**: Unique identifier
- **Description**: What needs to be done
- **Dependencies**: What must be complete first
- **Success Criteria**: Specific, testable conditions for completion
- **Estimated Effort**: Rough time estimate
- **Test Requirements**: Required test coverage

**Cross-Reference**: See [DESIGN.md](./DESIGN.md) for overall architecture and phases.

---

## Phase 0: Foundation & Interfaces

### FDP-IF-001: Create FDP.Interfaces Project

**Description**:
Create the base `FDP.Interfaces` project to serve as the contract layer between toolkits.

**Steps**:
1. Create project at `ModuleHost/FDP.Interfaces/FDP.Interfaces.csproj`
2. Configure as .NET Standard 2.1 class library
3. Add reference to `Fdp.Kernel` (minimal - only for `Entity` type if needed)
4. Create folder structure: `Abstractions/`, `Constants/`

**Dependencies**: None

**Success Criteria**:
- ✅ Project compiles successfully
- ✅ No dependencies beyond Fdp.Kernel
- ✅ Project follows standard .csproj structure
- ✅ Namespace is `Fdp.Interfaces`

**Estimated Effort**: 0.5 day

**Tests**: N/A (infrastructure task)

---

### FDP-IF-002: Define ITkbDatabase Interface

**Description**:
Define the interface contract for TKB (blueprint) database implementations.

**File**: `ModuleHost/FDP.Interfaces/Abstractions/ITkbDatabase.cs`

**Interface Specification**:
```csharp
namespace Fdp.Interfaces
{
    public interface ITkbDatabase
    {
        // Template registration
        void Register(TkbTemplate template);
        
        // Lookup by TkbType (primary key)
        TkbTemplate GetByType(long tkbType);
        bool TryGetByType(long tkbType, out TkbTemplate template);
        
        // Lookup by name (secondary key)  
        TkbTemplate GetByName(string name);
        bool TryGetByName(string name, out TkbTemplate template);
        
        // Enumeration
        IEnumerable<TkbTemplate> GetAll();
    }
}
```

**Dependencies**: FDP-IF-001

**Success Criteria**:
- ✅ Interface defined with correct signature
- ✅ XML documentation comments added
- ✅ No concrete implementation in this project
- ✅ Compiles without errors

**Estimated Effort**: 0.25 day

**Tests**: N/A (interface definition)

---

### FDP-IF-003: Define INetworkTopology Interface

**Description**:
Define interface for network peer discovery and topology management.

**File**: `ModuleHost/FDP.Interfaces/Abstractions/INetworkTopology.cs`

**Interface Specification**:
```csharp
namespace Fdp.Interfaces
{
    public interface INetworkTopology
    {
        /// <summary>
        /// This node's unique identifier.
        /// </summary>
        int LocalNodeId { get; }
        
        /// <summary>
        /// Gets the list of peer node IDs that should acknowledge construction
        /// of an entity of the given type.
        /// </summary>
        /// <param name="tkbType">Entity type identifier</param>
        /// <returns>Collection of node IDs that must ACK</returns>
        IEnumerable<int> GetExpectedPeers(long tkbType);
        
        /// <summary>
        /// Gets all known node IDs in the network.
        /// </summary>
        IEnumerable<int> GetAllNodes();
    }
}
```

**Dependencies**: FDP-IF-001

**Success Criteria**:
- ✅ Interface defined correctly
- ✅ XML documentation complete
- ✅ Compiles without errors

**Estimated Effort**: 0.25 day

**Tests**: N/A (interface definition)

---

### FDP-IF-004: Define INetworkMaster Interface

**Description**:
Define the generic interface that all entity master descriptors must implement.

**File**: `ModuleHost/FDP.Interfaces/Abstractions/INetworkMaster.cs`

**Interface Specification**:
```csharp
namespace Fdp.Interfaces
{
    /// <summary>
    /// Contract for entity master descriptors.
    /// The master defines entity identity and type.
    /// </summary>
    public interface INetworkMaster
    {
        /// <summary>
        /// Globally unique entity identifier.
        /// </summary>
        long EntityId { get; }
        
        /// <summary>
        /// TKB type identifier for blueprint lookup.
        /// </summary>
        long TkbType { get; }
        
        // NOTE: OwnerId is NOT included - ownership is implicit from DDS writer
    }
}
```

**Dependencies**: FDP-IF-001

**Success Criteria**:
- ✅ Interface defined correctly
- ✅ XML documentation includes note about implicit ownership
- ✅ No `OwnerId` field present
- ✅ Compiles without errors

**Estimated Effort**: 0.25 day

**Tests**: N/A (interface definition)

---

### FDP-IF-005: Define IDescriptorTranslator Interface

**Description**:
Define interface for translating between DDS descriptors and ECS components.

**File**: `ModuleHost/FDP.Interfaces/Abstractions/IDescriptorTranslator.cs`

**Interface Specification**:
```csharp
namespace Fdp.Interfaces
{
    /// <summary>
    /// Translates between network descriptors and ECS components.
    /// </summary>
    public interface IDescriptorTranslator
    {
        /// <summary>
        /// Unique identifier for this descriptor type.
        /// </summary>
        long DescriptorOrdinal { get; }
        
        /// <summary>
        /// DDS topic name.
        /// </summary>
        string TopicName { get; }
        
        /// <summary>
        /// Processes incoming network data and updates ECS entities.
        /// </summary>
        void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, ISimulationView view);
        
        /// <summary>
        /// Scans ECS entities and publishes updates to the network.
        /// </summary>
        void ScanAndPublish(ISimulationView view, IDataWriter writer);
        
        /// <summary>
        /// Applies descriptor data to an entity (used during ghost promotion).
        /// </summary>
        void ApplyToEntity(Entity entity, object data, EntityRepository repo);
    }
}
```

**Dependencies**: FDP-IF-001

**Success Criteria**:
- ✅ Interface defined correctly
- ✅ Uses `long DescriptorOrdinal` (not `int`)
- ✅ Includes `ApplyToEntity` for ghost promotion
- ✅ XML documentation complete
- ✅ Compiles without errors

**Estimated Effort**: 0.5 day

**Tests**: N/A (interface definition)

---

### FDP-IF-006: Define ISerializationProvider Interface

**Description**:
Define interface for binary serialization of descriptors (used in ghost stashing).

**File**: `ModuleHost/FDP.Interfaces/Abstractions/ISerializationProvider.cs`

**Interface Specification**:
```csharp
namespace Fdp.Interfaces
{
    /// <summary>
    /// Provides binary serialization for descriptor types.
    /// Used for zero-allocation ghost stashing.
    /// </summary>
    public interface ISerializationProvider
    {
        /// <summary>
        /// Gets the serialized size in bytes for a descriptor.
        /// </summary>
        int GetSize(object descriptor);
        
        /// <summary>
        /// Encodes descriptor to binary buffer.
        /// </summary>
        void Encode(object descriptor, Span<byte> buffer);
        
        /// <summary>
        /// Applies serialized descriptor data to an entity.
        /// </summary>
        void Apply(Entity entity, ReadOnlySpan<byte> buffer, EntityRepository repo);
    }
    
    /// <summary>
    /// Registry mapping descriptor ordinals to serialization providers.
    /// </summary>
    public interface ISerializationRegistry
    {
        void Register(long descriptorOrdinal, ISerializationProvider provider);
        ISerializationProvider Get(long descriptorOrdinal);
        bool TryGet(long descriptorOrdinal, out ISerializationProvider provider);
    }
}
```

**Dependencies**: FDP-IF-001

**Success Criteria**:
- ✅ Both interfaces defined correctly
- ✅ Uses `Span<byte>` for zero-copy operations
- ✅ XML documentation complete
- ✅ Compiles without errors

**Estimated Effort**: 0.5 day

**Tests**: N/A (interface definition)

---

## Phase 0: TKB Enhancement

### FDP-TKB-001: Create FDP.Toolkit.Tkb Project

**Description**:
Create the TKB toolkit project and migrate existing TKB code from Fdp.Kernel.

**Steps**:
1. Create project at `ModuleHost/FDP.Toolkit.Tkb/FDP.Toolkit.Tkb.csproj`
2. Add references: `Fdp.Kernel`, `FDP.Interfaces`
3. Create test project at `ModuleHost/FDP.Toolkit.Tkb.Tests/`
4. Copy existing TKB code from `Fdp.Kernel/Tkb/` as starting point

**Dependencies**: FDP-IF-002

**Success Criteria**:
- ✅ Project compiles successfully
- ✅ References correct projects
- ✅ Test project setup and can reference main project
- ✅ Existing TKB code copied and compiles

**Estimated Effort**: 0.5 day

**Tests**: Copy existing `Fdp.Tests/TkbTests.cs` to new test project and verify they pass

---

### FDP-TKB-002: Implement PackedKey Utilities

**Description**:
Create utilities for packing/unpacking descriptor keys (ordinal + instance ID).

**File**: `ModuleHost/FDP.Toolkit.Tkb/PackedKey.cs`

**Implementation**:
```csharp
namespace Fdp.Toolkit.Tkb
{
    /// <summary>
    /// Utilities for packing descriptor type and instance ID into a single long.
    /// Layout: [High 32 bits: Ordinal] [Low 32 bits: InstanceId]
    /// </summary>
    public static class PackedKey
    {
        public static long Create(int ordinal, int instanceId)
        {
            return ((long)ordinal << 32) | (uint)instanceId;
        }
        
        public static int GetOrdinal(long packedKey)
        {
            return (int)(packedKey >> 32);
        }
        
        public static int GetInstanceId(long packedKey)
        {
            return (int)(packedKey & 0xFFFFFFFF);
        }
        
        public static string ToString(long packedKey)
        {
            return $"(Ord:{GetOrdinal(packedKey)}, Inst:{GetInstanceId(packedKey)})";
        }
    }
}
```

**Dependencies**: FDP-TKB-001

**Success Criteria**:
- ✅ All utility methods implemented correctly
- ✅ Handles edge cases (ordinal=0, instanceId=0, max values)
- ✅ Round-trip packing/unpacking preserves values
- ✅ Unit tests pass

**Test Requirements**:
```csharp
[TestClass]
public class PackedKeyTests
{
    [TestMethod]
    public void Create_PacksCorrectly()
    {
        long packed = PackedKey.Create(5, 3);
        Assert.AreEqual(5, PackedKey.GetOrdinal(packed));
        Assert.AreEqual(3, PackedKey.GetInstanceId(packed));
    }
    
    [TestMethod]
    public void PackUnpack_RoundTrip()
    {
        int ordinal = 123;
        int instanceId = 456;
        long packed = PackedKey.Create(ordinal, instanceId);
        Assert.AreEqual(ordinal, PackedKey.GetOrdinal(packed));
        Assert.AreEqual(instanceId, PackedKey.GetInstanceId(packed));
    }
    
    [TestMethod]
    public void Create_HandlesZeroValues()
    {
        long packed = PackedKey.Create(0, 0);
        Assert.AreEqual(0, PackedKey.GetOrdinal(packed));
        Assert.AreEqual(0, PackedKey.GetInstanceId(packed));
    }
    
    [TestMethod]
    public void Create_HandlesMaxValues()
    {
        long packed = PackedKey.Create(int.MaxValue, int.MaxValue);
        Assert.AreEqual(int.MaxValue, PackedKey.GetOrdinal(packed));
        // Note: InstanceId is unsigned, so max is different
        Assert.IsTrue(PackedKey.GetInstanceId(packed) > 0);
    }
}
```

**Estimated Effort**: 0.5 day

---

### FDP-TKB-003: Implement MandatoryDescriptor Type

**Description**:
Define the structure for tracking mandatory descriptor requirements.

**File**: `ModuleHost/FDP.Toolkit.Tkb/MandatoryDescriptor.cs`

**Implementation**:
```csharp
namespace Fdp.Toolkit.Tkb
{
    /// <summary>
    /// Defines a requirement for an entity type's construction.
    /// Hard requirements MUST be met; soft requirements have a timeout.
    /// </summary>
    public struct MandatoryDescriptor
    {
        /// <summary>
        /// Packed key: (DescriptorOrdinal << 32) | InstanceId
        /// </summary>
        public long PackedKey;
        
        /// <summary>
        /// If true, entity cannot be promoted without this descriptor.
        /// If false, promotion proceeds after SoftTimeoutFrames.
        /// </summary>
        public bool IsHard;
        
        /// <summary>
        /// For soft requirements: frames to wait before giving up.
        /// Ignored for hard requirements.
        /// </summary>
        public uint SoftTimeoutFrames;
        
        public override string ToString()
        {
            return $"{PackedKey.ToString(PackedKey)} ({(IsHard ? "Hard" : $"Soft:{SoftTimeoutFrames}f")})";
        }
    }
}
```

**Dependencies**: FDP-TKB-002

**Success Criteria**:
- ✅ Struct defined correctly
- ✅ Uses `PackedKey` for descriptor identification
- ✅ XML documentation complete
- ✅ ToString provides useful debug output
- ✅ Compiles without errors

**Test Requirements**:
```csharp
[TestClass]
public class MandatoryDescriptorTests
{
    [TestMethod]
    public void HardRequirement_IsConfiguredCorrectly()
    {
        var req = new MandatoryDescriptor
        {
            PackedKey = PackedKey.Create(5, 0),
            IsHard = true
        };
        
        Assert.IsTrue(req.IsHard);
        Assert.AreEqual(5, PackedKey.GetOrdinal(req.PackedKey));
    }
    
    [TestMethod]
    public void SoftRequirement_HasTimeout()
    {
        var req = new MandatoryDescriptor
        {
            PackedKey = PackedKey.Create(10, 2),
            IsHard = false,
            SoftTimeoutFrames = 600
        };
        
        Assert.IsFalse(req.IsHard);
        Assert.AreEqual(600u, req.SoftTimeoutFrames);
    }
}
```

**Estimated Effort**: 0.5 day

---

### FDP-TKB-004: Enhance TkbTemplate with TkbType Support

**Description**:
Update `TkbTemplate` to use `long TkbType` as primary identifier and support mandatory descriptors.

**File**: `ModuleHost/FDP.Toolkit.Tkb/TkbTemplate.cs`

**Enhancements**:
```csharp
public class TkbTemplate
{
    /// <summary>
    /// Unique type identifier (primary key).
    /// </summary>
    public long TkbType { get; }
    
    /// <summary>
    /// Human-readable name (secondary key).
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// List of descriptors that must be present before ghost promotion.
    /// EntityMaster is implicitly always hard-required.
    /// </summary>
    public List<MandatoryDescriptor> MandatoryDescriptors { get; } = new();
    
    public TkbTemplate(string name, long tkbType)
    {
        if (string.IsNullOrWhitespace(name))
            throw new ArgumentNullException(nameof(name));
        if (tkbType == 0)
            throw new ArgumentException("TkbType cannot be zero", nameof(tkbType));
            
        Name = name;
        TkbType = tkbType;
    }
    
    // ... existing AddComponent methods ...
    
    /// <summary>
    /// Checks if all hard mandatory descriptors are present in the given set.
    /// </summary>
    public bool AreHardRequirementsMet(IReadOnlyCollection<long> availableKeys)
    {
        foreach (var req in MandatoryDescriptors)
        {
            if (req.IsHard && !availableKeys.Contains(req.PackedKey))
                return false;
        }
        return true;
    }
    
    /// <summary>
    /// Checks if all requirements (hard + soft within timeout) are met.
    /// </summary>
    public bool AreAllRequirementsMet(
        IReadOnlyCollection<long> availableKeys, 
        uint currentFrame, 
        uint identifiedFrame)
    {
        foreach (var req in MandatoryDescriptors)
        {
            if (!availableKeys.Contains(req.PackedKey))
            {
                if (req.IsHard)
                    return false;
                    
                // Soft: check timeout
                if (currentFrame - identifiedFrame < req.SoftTimeoutFrames)
                    return false;
            }
        }
        return true;
    }
}
```

**Dependencies**: FDP-TKB-003

**Success Criteria**:
- ✅ Constructor requires both name and TkbType
- ✅ TkbType is validated (non-zero)
- ✅ MandatoryDescriptors list initialized
- ✅ Requirement checking methods work correctly
- ✅ Preserves existing component applicator functionality
- ✅ All tests pass

**Test Requirements**:
```csharp
[TestClass]
public class TkbTemplateTests
{
    [TestMethod]
    public void Constructor_RequiresTkbType()
    {
        Assert.ThrowsException<ArgumentException>(() => 
            new TkbTemplate("Test", 0));
    }
    
    [TestMethod]
    public void AreHardRequirementsMet_ReturnsFalseWhenMissing()
    {
        var template = new TkbTemplate("Tank", 100);
        template.MandatoryDescriptors.Add(new MandatoryDescriptor
        {
            PackedKey = PackedKey.Create(5, 0),
            IsHard = true
        });
        
        var available = new HashSet<long>();
        Assert.IsFalse(template.AreHardRequirementsMet(available));
        
        available.Add(PackedKey.Create(5, 0));
        Assert.IsTrue(template.AreHardRequirementsMet(available));
    }
    
    [TestMethod]
    public void AreAllRequirementsMet_HandlesSoftTimeout()
    {
        var template = new TkbTemplate("Tank", 100);
        template.MandatoryDescriptors.Add(new MandatoryDescriptor
        {
            PackedKey = PackedKey.Create(10, 0),
            IsHard = false,
            SoftTimeoutFrames = 100
        });
        
        var available = new HashSet<long>();
        
        // Within timeout: not ready
        Assert.IsFalse(template.AreAllRequirementsMet(available, 50, 0));
        
        // After timeout: ready even without soft requirement
        Assert.IsTrue(template.AreAllRequirementsMet(available, 101, 0));
    }
}
```

**Estimated Effort**: 1 day

---

### FDP-TKB-005: Enhance TkbDatabase with TkbType Lookup

**Description**:
Update `TkbDatabase` to support lookup by TkbType and implement `ITkbDatabase`.

**File**: `ModuleHost/FDP.Toolkit.Tkb/TkbDatabase.cs`

**Enhancements**:
```csharp
public class TkbDatabase : ITkbDatabase
{
    private readonly Dictionary<string, TkbTemplate> _templatesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<long, TkbTemplate> _templatesByType = new();
    
    public void Register(TkbTemplate template)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));
            
        if (_templatesByType.ContainsKey(template.TkbType))
            throw new InvalidOperationException($"Template with TkbType {template.TkbType} already registered");
            
        if (_templatesByName.ContainsKey(template.Name))
            throw new InvalidOperationException($"Template with name '{template.Name}' already registered");
            
        _templatesByType[template.TkbType] = template;
        _templatesByName[template.Name] = template;
    }
    
    public TkbTemplate GetByType(long tkbType)
    {
        if (!_templatesByType.TryGetValue(tkbType, out var template))
            throw new KeyNotFoundException($"Template with TkbType {tkbType} not found");
        return template;
    }
    
    public bool TryGetByType(long tkbType, out TkbTemplate template)
    {
        return _templatesByType.TryGetValue(tkbType, out template);
    }
    
    public TkbTemplate GetByName(string name)
    {
        if (!_templatesByName.TryGetValue(name, out var template))
            throw new KeyNotFoundException($"Template '{name}' not found");
        return template;
    }
    
    public bool TryGetByName(string name, out TkbTemplate template)
    {
        return _templatesByName.TryGetValue(name, out template);
    }
    
    public IEnumerable<TkbTemplate> GetAll()
    {
        return _templatesByType.Values;
    }
    
    public void Clear()
    {
        _templatesByType.Clear();
        _templatesByName.Clear();
    }
}
```

**Dependencies**: FDP-TKB-004, FDP-IF-002

**Success Criteria**:
- ✅ Implements `ITkbDatabase` interface
- ✅ Dual-key indexing (TkbType + Name)
- ✅ Registration validates uniqueness of both keys
- ✅ Lookup methods work correctly
- ✅ All tests pass

**Test Requirements**:
```csharp
[TestClass]
public class TkbDatabaseTests
{
    [TestMethod]
    public void Register_PreventsDuplicateTkbType()
    {
        var db = new TkbDatabase();
        db.Register(new TkbTemplate("Tank", 100));
        
        Assert.ThrowsException<InvalidOperationException>(() =>
            db.Register(new TkbTemplate("AnotherTank", 100)));
    }
    
    [TestMethod]
    public void GetByType_RetrievesTemplate()
    {
        var db = new TkbDatabase();
        var template = new TkbTemplate("Tank", 100);
        db.Register(template);
        
        var retrieved = db.GetByType(100);
        Assert.AreSame(template, retrieved);
    }
    
    [TestMethod]
    public void TryGetByType_ReturnsFalseWhenMissing()
    {
        var db = new TkbDatabase();
        Assert.IsFalse(db.TryGetByType(999, out var template));
        Assert.IsNull(template);
    }
}
```

**Estimated Effort**: 1 day

---

## Phase 1: Lifecycle Extraction

### FDP-LC-001: Create FDP.Toolkit.Lifecycle Project

**Description**:
Create the lifecycle toolkit project structure.

**Steps**:
1. Create project at `ModuleHost/FDP.Toolkit.Lifecycle/`
2. Add references: `Fdp.Kernel`, `ModuleHost.Core`, `FDP.Interfaces`, `FDP.Toolkit.Tkb`
3. Create test project at `ModuleHost/FDP.Toolkit.Lifecycle.Tests/`
4. Create folder structure: `Events/`, `Systems/`, `Components/`

**Dependencies**: FDP-TKB-005

**Success Criteria**:
- ✅ Project compiles successfully
- ✅ All references correct
- ✅ Test project can run tests
- ✅ Namespace is `Fdp.Toolkit.Lifecycle`

**Estimated Effort**: 0.5 day

**Tests**: N/A (infrastructure task)

---

### FDP-LC-002: Move Lifecycle Events

**Description**:
Move lifecycle event definitions from ModuleHost.Core to toolkit.

**Source**: `ModuleHost\ModuleHost.Core\ELM\LifecycleEvents.cs`  
**Target**: `ModuleHost\FDP.Toolkit.Lifecycle\Events\LifecycleEvents.cs`

**Changes**:
- Update namespace to `Fdp.Toolkit.Lifecycle.Events`
- Change `TypeId` field to `BlueprintId` for clarity
- Keep event IDs the same to maintain compatibility

**Dependencies**: FDP-LC-001

**Success Criteria**:
- ✅ All event types moved correctly
- ✅ Event IDs preserved
- ✅ Namespace updated
- ✅ `BlueprintId` naming used (not `TypeId`)
- ✅ XML documentation updated
- ✅ Compiles without errors

**Estimated Effort**: 0.5 day

**Tests**: Move `ModuleHost.Core.Tests/LifecycleEventsTests.cs` and verify tests pass

---

### FDP-LC-003: Implement BlueprintApplicationSystem

**Description**:
Create system that applies TKB blueprints with preservation logic.

**File**: `ModuleHost/FDP.Toolkit.Lifecycle/Systems/BlueprintApplicationSystem.cs`

**Implementation**:
```csharp
namespace Fdp.Toolkit.Lifecycle.Systems
{
    [UpdateInPhase(SystemPhase.Construction)]
    public class BlueprintApplicationSystem : IModuleSystem
    {
        private readonly ITkbDatabase _tkb;
        
        public BlueprintApplicationSystem(ITkbDatabase tkb)
        {
            _tkb = tkb ?? throw new ArgumentNullException(nameof(tkb));
        }
        
        public void Execute(ISimulationView view, float deltaTime)
        {
            // Process ConstructionOrder events
            foreach (var order in view.ConsumeEvents<ConstructionOrder>())
            {
                if (!_tkb.TryGetByType(order.BlueprintId, out var template))
                {
                    Console.Error.WriteLine($"[BPA] Unknown BlueprintId: {order.BlueprintId}");
                    continue;
                }
                
                var repo = view.GetRepository();
                
                // Apply template with preservation: don't overwrite existing components!
                template.ApplyTo(repo, order.Entity, preserveExisting: true);
            }
        }
    }
}
```

**Dependencies**: FDP-LC-002

**Success Criteria**:
- ✅ System processes `ConstructionOrder` events
- ✅ Looks up blueprint from TKB
- ✅ Calls `ApplyTo` with `preserveExisting: true`
- ✅ Handles missing blueprints gracefully
- ✅ Runs in Construction phase
- ✅ Unit tests pass

**Test Requirements**:
```csharp
[TestClass]
public class BlueprintApplicationSystemTests
{
    [TestMethod]
    public void Execute_AppliesBlueprint()
    {
        var repo = new EntityRepository();
        var tkb = new TkbDatabase();
        var template = new TkbTemplate("Tank", 100);
        template.AddComponent(new TestComponent { Value = 42 });
        tkb.Register(template);
        
        var system = new BlueprintApplicationSystem(tkb);
        var entity = repo.CreateEntity();
        
        // Simulate ConstructionOrder event
        repo.PublishEvent(new ConstructionOrder
        {
            Entity = entity,
            BlueprintId = 100,
            FrameNumber = 1
        });
        
        var view = CreateMockView(repo);
        system.Execute(view, 0.016f);
        
        Assert.IsTrue(repo.HasComponent<TestComponent>(entity));
        Assert.AreEqual(42, repo.GetComponent<TestComponent>(entity).Value);
    }
    
    [TestMethod]
    public void Execute_PreservesExistingComponents()
    {
        var repo = new EntityRepository();
        var tkb = new TkbDatabase();
        var template = new TkbTemplate("Tank", 100);
        template.AddComponent(new TestComponent { Value = 100 });
        tkb.Register(template);
        
        var system = new BlueprintApplicationSystem(tkb);
        var entity = repo.CreateEntity();
        
        // Entity already has component with different value (injected)
        repo.AddComponent(entity, new TestComponent { Value = 200 });
        
        repo.PublishEvent(new ConstructionOrder
        {
            Entity = entity,
            BlueprintId = 100,
            FrameNumber = 1
        });
        
        var view = CreateMockView(repo);
        system.Execute(view, 0.016f);
        
        // Value should remain 200 (preserved), not overwritten to 100
        Assert.AreEqual(200, repo.GetComponent<TestComponent>(entity).Value);
    }
}
```

**Estimated Effort**: 1 day

---

### FDP-LC-004: Move EntityLifecycleModule

**Description**:
Move and refactor the main lifecycle coordination module.

**Source**: `ModuleHost\ModuleHost.Core\ELM\EntityLifecycleModule.cs`  
**Target**: `ModuleHost\FDP.Toolkit.Lifecycle\EntityLifecycleModule.cs`

**Refactorings**:
1. Update namespace to `Fdp.Toolkit.Lifecycle`
2. Change constructor to accept `ITkbDatabase` reference
3. Modify `BeginConstruction` to take `long blueprintId` (not `int typeId`)
4. Add `RegisterRequirement(blueprintId, moduleId)` method for dynamic participation

**Dependencies**: FDP-LC-003

**Success Criteria**:
- ✅ Module moved and compiles
- ✅ Namespace updated
- ✅ Uses `long blueprintId` consistently
- ✅ Dynamic requirement registration works
- ✅ All original functionality preserved
- ✅ Tests pass

**Estimated Effort**: 1.5 days

**Tests**: Move `ModuleHost.Core.Tests/EntityLifecycleModuleTests.cs` and verify all tests pass

---

### FDP-LC-005: Move LifecycleSystem

**Description**:
Move the system that processes ACKs and checks timeouts.

**Source**: `ModuleHost\ModuleHost.Core\ELM\LifecycleSystem.cs`  
**Target**: `ModuleHost\FDP.Toolkit.Lifecycle\Systems\LifecycleSystem.cs`

**Dependencies**: FDP-LC-004

**Success Criteria**:
- ✅ System moved correctly
- ✅ Namespace updated
- ✅ Works with refactored `EntityLifecycleModule`
- ✅ Compiles without errors
- ✅ Tests pass

**Estimated Effort**: 0.5 day

**Tests**: Existing integration tests should cover this

---

### FDP-LC-006: Implement LifecycleCleanupSystem

**Description**:
Create new system that removes transient construction components when entity activates.

**File**: `ModuleHost/FDP.Toolkit.Lifecycle/Systems/LifecycleCleanupSystem.cs`

**Implementation**:
This system watches for entities transitioning to `Active` state and removes any components marked with `[DataPolicy(DataPolicy.Transient)]`.

**Dependencies**: FDP-LC-005

**Success Criteria**:
- ✅ System detects lifecycle transitions
- ✅ Removes transient components on activation
- ✅ Preserves non-transient components
- ✅ Unit tests pass

**Test Requirements**:
```csharp
[TestMethod]
public void Execute_RemovesTransientComponents()
{
    var repo = new EntityRepository();
    var system = new LifecycleCleanupSystem();
    var entity = repo.CreateEntity();
    
    repo.AddComponent(entity, new PermanentComponent { Value = 1 });
    repo.AddManagedComponent(entity, new TransientConstructionParam { Data = "test" });
    
    repo.SetLifecycleState(entity, EntityLifecycle.Constructing);
    repo.Tick();
    
    // Transition to Active
    repo.SetLifecycleState(entity, EntityLifecycle.Active);
    
    var view = CreateMockView(repo);
    system.Execute(view, 0.016f);
    
    // Permanent component stays
    Assert.IsTrue(repo.HasComponent<PermanentComponent>(entity));
    
    // Transient component removed
    Assert.IsFalse(repo.HasManagedComponent<TransientConstructionParam>(entity));
}
```

**Estimated Effort**: 1 day

---

### FDP-LC-007: Clean Up ModuleHost.Core

**Description**:
Remove lifecycle code from ModuleHost.Core now that it's extracted.

**Steps**:
1. Delete folder `ModuleHost\ModuleHost.Core\ELM\`
2. Remove lifecycle-related code from `ModuleHostKernel.cs` if any
3. Update ModuleHost.Core tests to reference toolkit where needed
4. Ensure no broken references remain

**Dependencies**: FDP-LC-006

**Success Criteria**:
- ✅ ELM folder deleted from ModuleHost.Core
- ✅ ModuleHost.Core compiles without ELM code
- ✅ All tests still pass (using toolkit instead)
- ✅ No dead code or broken references

**Estimated Effort**: 0.5 day

**Tests**: Run full ModuleHost.Core test suite

---

### FDP-LC-008: Integration Test - Lifecycle Toolkit

**Description**:
Create comprehensive integration test demonstrating full lifecycle flow.

**File**: `ModuleHost/FDP.Toolkit.Lifecycle.Tests/Integration/LifecycleIntegrationTests.cs`

**Test Scenario**:
```csharp
[TestMethod]
public void FullLifecycle_WithDirectInjection()
{
    // Setup
    var repo = new EntityRepository();
    var tkb = new TkbDatabase();
    var template = new TkbTemplate("Tank", 100);
    template.AddComponent(new Health { Value = 100 });  // Default
    template.MandatoryDescriptors.Add(new MandatoryDescriptor
    {
        PackedKey = PackedKey.Create(1, 0),
        IsHard = true
    });
    tkb.Register(template);
    
    var lifecycle = new EntityLifecycleModule(tkb);
    lifecycle.RegisterRequirement(100, moduleId: 5);  // Physics module
    
    // Create entity with injected override
    var entity = repo.CreateEntity();
    repo.SetLifecycleState(entity, EntityLifecycle.Constructing);
    repo.AddComponent(entity, new Health { Value = 50 });  // INJECTED override
    
    var cmd = new EntityCommandBuffer(repo);
    lifecycle.BeginConstruction(entity, 100, currentFrame: 1, cmd);
    cmd.Playback();
    
    // Simulate BlueprintApplicationSystem
    var blueprintSys = new BlueprintApplicationSystem(tkb);
    blueprintSys.Execute(CreateView(repo), 0.016f);
    
    // Health should still be 50 (preserved injection), not 100 (blueprint default)
    Assert.AreEqual(50, repo.GetComponent<Health>(entity).Value);
    
    // Simulate module ACK
    cmd.PublishEvent(new ConstructionAck
    {
        Entity = entity,
        ModuleId = 5,
        Success = true
    });
    cmd.Playback();
    
    // Entity should now be Active
    Assert.AreEqual(EntityLifecycle.Active, repo.GetLifecycleState(entity));
}
```

**Dependencies**: FDP-LC-007

**Success Criteria**:
- ✅ Integration test passes
- ✅ Direct injection preserves overrides
- ✅ Blueprint fills gaps
- ✅ ACK coordination works correctly

**Estimated Effort**: 1 day

---

## Phase 2: Time Extraction

### FDP-TM-001: Create FDP.Toolkit.Time Project

**Description**:
Create the time synchronization toolkit project.

**Steps**:
1. Create project at `ModuleHost/FDP.Toolkit.Time/`
2. Add references: `Fdp.Kernel`, `ModuleHost.Core`, `FDP.Interfaces`
3. Create test project
4. Create folder structure: `Controllers/`, `Messages/`, `Systems/`

**Dependencies**: None (independent of lifecycle)

**Success Criteria**:
- ✅ Project compiles
- ✅ References correct
- ✅ Test project setup

**Estimated Effort**: 0.5 day

**Tests**: N/A (infrastructure)

---

### FDP-TM-002: Move Time Controllers

**Description**:
Move time controller implementations from ModuleHost.Core to toolkit.

**Files to Move**:
- `MasterTimeController.cs`
- `SlaveTimeController.cs`
- `SteppedMasterController.cs`
- `SteppedSlaveController.cs`
- `DistributedTimeCoordinator.cs`
- `SlaveTimeModeListener.cs`
- `TimeConfig.cs`
- `TimeControllerConfig.cs`

**Target**: `ModuleHost/FDP.Toolkit.Time/Controllers/`

**Keep in ModuleHost.Core**:
- `ITimeController.cs` (interface stays with consumer)

**Dependencies**: FDP-TM-001

**Success Criteria**:
- ✅ All controller files moved
- ✅ Namespaces updated to `Fdp.Toolkit.Time.Controllers`
- ✅ All references to `ITimeController` still resolve correctly
- ✅ Compiles without errors

**Estimated Effort**: 1 day

**Tests**: Move all tests from `ModuleHost.Core.Tests/Time/` folder

---

### FDP-TM-003: Move Time Messages/Descriptors

**Description**:
Move time synchronization message definitions.

**Source**: `ModuleHost\ModuleHost.Core\Time\TimeDescriptors.cs`  
**Target**: `ModuleHost\FDP.Toolkit.Time\Messages\TimeMessages.cs`

**Descriptors to Move**:
- `TimePulse`
- `FrameOrder`
- `FrameAck`
- `SwitchTimeModeEvent`

**Dependencies**: FDP-TM-002

**Success Criteria**:
- ✅ All message types moved
- ✅ Event IDs preserved
- ✅ Namespace updated
- ✅ Compiles without errors

**Estimated Effort**: 0.5 day

**Tests**: Existing tests should verify message structure

---

### FDP-TM-004: Clean Up ModuleHost.Core Time Code

**Description**:
Remove extracted time code from ModuleHost.Core.

**Steps**:
1. Delete all files in `ModuleHost\ModuleHost.Core\Time\` EXCEPT `ITimeController.cs`
2. Update any references in ModuleHostKernel to use toolkit
3. Ensure backwards compatibility

**Dependencies**: FDP-TM-003

**Success Criteria**:
- ✅ Only `ITimeController.cs` remains in ModuleHost.Core
- ✅ ModuleHost.Core compiles
- ✅ All tests pass

**Estimated Effort**: 0.5 day

**Tests**: Run full ModuleHost.Core test suite

---

### FDP-TM-005: Integration Test - Time Synchronization

**Description**:
Create integration test demonstrating PLL synchronization.

**File**: `ModuleHost/FDP.Toolkit.Time.Tests/Integration/PLLSynchronizationTests.cs`

**Test Scenario**: Master/Slave sync with network delay simulation

**Dependencies**: FDP-TM-004

**Success Criteria**:
- ✅ Master and slave synchronize within tolerance
- ✅ PLL smoothing prevents rubber-banding
- ✅ Jitter filter handles network spikes
- ✅ Integration test passes

**Estimated Effort**: 1 day

---

## Phase 3: Replication - Core Infrastructure

### FDP-REP-001: Create FDP.Toolkit.Replication Project

**Description**:
Create the replication toolkit project structure.

**Steps**:
1. Create project at `ModuleHost/FDP.Toolkit.Replication/`
2. Add references: `Fdp.Kernel`, `ModuleHost.Core`, `FDP.Interfaces`, `FDP.Toolkit.Tkb`, `FDP.Toolkit.Lifecycle`
3. Create test project
4. Create folder structure: `Components/`, `Systems/`, `Services/`, `Messages/`

**Dependencies**: FDP-LC-008, FDP-TM-005

**Success Criteria**:
- ✅ Project compiles
- ✅ All necessary references
- ✅ Test project setup

**Estimated Effort**: 0.5 day

**Tests**: N/A (infrastructure)

---

### FDP-REP-002: Implement Core Network Components

**Description**:
Define core ECS components for network replication.

**Files**:
1. `ModuleHost/FDP.Toolkit.Replication/Components/NetworkIdentity.cs`
2. `ModuleHost/FDP.Toolkit.Replication/Components/NetworkAuthority.cs`
3. `ModuleHost/FDP.Toolkit.Replication/Components/DescriptorOwnership.cs`

**Implementations**:
```csharp
// NetworkIdentity.cs
namespace Fdp.Toolkit.Replication.Components
{
    /// <summary>
    /// Globally unique network identifier for an entity.
    /// </summary>
    public struct NetworkIdentity
    {
        public long Value;
    }
}

// NetworkAuthority.cs
namespace Fdp.Toolkit.Replication.Components
{
    /// <summary>
    /// Tracks ownership authority for network entities.
    /// </summary>
    public struct NetworkAuthority
    {
        /// <summary>
        /// Node ID that owns the EntityMaster (primary authority).
        /// </summary>
        public int PrimaryOwnerId;
        
        /// <summary>
        /// This node's ID (for quick local checks).
        /// </summary>
        public int LocalNodeId;
    }
}

// DescriptorOwnership.cs
namespace Fdp.Toolkit.Replication.Components
{
    /// <summary>
    /// Tracks partial ownership of specific descriptors.
    /// If a descriptor is not in the map, ownership falls back to PrimaryOwnerId.
    /// </summary>
    public class DescriptorOwnership
    {
        /// <summary>
        /// Map of PackedKey (DescriptorOrdinal + InstanceId) to OwnerNodeId.
        /// </summary>
        public Dictionary<long, int> Map { get; } = new();
    }
}
```

**Dependencies**: FDP-REP-001

**Success Criteria**:
- ✅ All components defined correctly
- ✅ XML documentation complete
- ✅ Compiles without errors
- ✅ Unit tests validate structure

**Test Requirements**:
```csharp
[TestClass]
public class NetworkComponentsTests
{
    [TestMethod]
    public void NetworkIdentity_StoresValue()
    {
        var id = new NetworkIdentity { Value = 12345 };
        Assert.AreEqual(12345, id.Value);
    }
    
    [TestMethod]
    public void NetworkAuthority_TracksOwnership()
    {
        var auth = new NetworkAuthority
        {
            PrimaryOwnerId = 1,
            LocalNodeId = 2
        };
        Assert.AreEqual(1, auth.PrimaryOwnerId);
        Assert.AreEqual(2, auth.LocalNodeId);
    }
    
    [TestMethod]
    public void DescriptorOwnership_SupportsPartialOwnership()
    {
        var ownership = new DescriptorOwnership();
        long key = PackedKey.Create(5, 0);
        ownership.Map[key] = 3;
        
        Assert.AreEqual(3, ownership.Map[key]);
    }
}
```

**Estimated Effort**: 1 day

---

Due to character limits, I'll continue in the next output with the remaining tasks...

**Estimated Total for TASK-DETAILS.md**: This document will be approximately 20,000+ lines when complete. Should I:
1. Continue creating it as one large file?
2. Split it into multiple files by phase?
3. Create a condensed version first?

Please advise how you'd like me to proceed with the remainder of the task details (Phases 4-9 contain many more detailed tasks).

