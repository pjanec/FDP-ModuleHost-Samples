# FDP Refactoring - Critical Task Addendum

**Date**: 2026-02-04  
**Purpose**: Address technical gaps identified in code review

This addendum contains critical missing tasks that must be inserted into TASK-DETAILS.md to ensure completeness.

---

## INSERT AFTER FDP-IF-006 (Phase 0)

### FDP-IF-007: Move Transport Interfaces to FDP.Interfaces

**Description**:
Move `IDataReader`, `IDataWriter`, and `IDataSample` from `ModuleHost.Core` to `FDP.Interfaces` to prevent circular dependencies.

**Rationale**:
The `IDescriptorTranslator` interface (in `FDP.Interfaces`) needs to reference `IDataReader` and `IDataWriter`. If these remain in `ModuleHost.Core`, it creates a circular dependency: `FDP.Interfaces` → `ModuleHost.Core` → `FDP.Interfaces`. Moving them to Layer 0 breaks the cycle.

**Source Investigation**:
Check if these interfaces exist in `ModuleHost\ModuleHost.Core\Network\`. If not, extract them from existing concrete implementations (likely in CycloneDDS plugin).

**Target**: `ModuleHost/FDP.Interfaces/Transport/`

**Interface Specifications**:
```csharp
namespace Fdp.Interfaces.Transport
{
    /// <summary>
    /// Represents a single network data sample with metadata.
    /// </summary>
    public interface IDataSample
    {
        /// <summary>
        /// The deserialized descriptor object.
        /// </summary>
        object Data { get; }
        
        /// <summary>
        /// Writer/owner node ID (from DDS metadata).
        /// Used for implicit ownership tracking.
        /// </summary>
        int PublisherId { get; }
        
        /// <summary>
        /// Instance ID extracted from transport metadata.
        /// Enables routing to sub-entity parts without reflection.
        /// For single-instance descriptors, this is 0.
        /// </summary>
        long InstanceId { get; }
        
        /// <summary>
        /// True if this is a disposal notification (entity deleted).
        /// </summary>
        bool IsDisposed { get; }
        
        /// <summary>
        /// Sequence number for ordering.
        /// </summary>
        long SequenceNumber { get; }
    }
    
    /// <summary>
    /// Reads samples from a network topic.
    /// </summary>
    public interface IDataReader
    {
        /// <summary>
        /// Topic name this reader is subscribed to.
        /// </summary>
        string TopicName { get; }
        
        /// <summary>
        /// Takes up to maxSamples from the reader.
        /// Must be followed by ReturnLoan when done.
        /// </summary>
        IEnumerable<IDataSample> Take(int maxSamples);
        
        /// <summary>
        /// Returns borrowed samples back to the reader pool.
        /// Required for zero-copy implementations (CycloneDDS).
        /// </summary>
        void ReturnLoan(IEnumerable<IDataSample> samples);
    }
    
    /// <summary>
    /// Writes samples to a network topic.
    /// </summary>
    public interface IDataWriter
    {
        /// <summary>
        /// Topic name this writer publishes to.
        /// </summary>
        string TopicName { get; }
        
        /// <summary>
        /// Publishes a descriptor to the network.
        /// </summary>
        void Write(object data);
        
        /// <summary>
        /// Sends a disposal notification (entity deleted).
        /// Sets instance state to NotAliveDisposed.
        /// </summary>
        void Dispose(object data);
    }
}
```

**Dependencies**: FDP-IF-001

**Success Criteria**:
- ✅ All transport interfaces defined in `FDP.Interfaces`
- ✅ `IDescriptorTranslator` can reference them without circular dependency
- ✅ Existing implementations in Cyclone plugin still compile
- ✅ Updated references in any code that uses these interfaces
- ✅ XML documentation complete

**Impact**: This is a **critical foundation task**. Without it, `FDP.Interfaces` cannot be truly Layer 0.

**Estimated Effort**: 0.5 day

**Tests**: Existing plugin tests should still pass after refactoring references

---

## INSERT AFTER FDP-TKB-005 (Phase 0)

### FDP-TKB-006: Add Sub-Entity Blueprint Support

**Description**:
Enhance `TkbTemplate` to define child blueprints that should be automatically spawned as sub-entities (parts).

**Rationale**:
When a Tank is ghosted/promoted, the system needs to know it should also create Turret0 and Turret1 as linked sub-entities. Without this metadata in the TKB, the `GhostPromotionSystem` cannot perform automated part spawning.

**File**: `ModuleHost/FDP.Toolkit.Tkb/TkbTemplate.cs`

**Enhancement**:
```csharp
public class TkbTemplate
{
    // ... existing properties ...
    
    /// <summary>
    /// List of child blueprints to spawn as sub-entities.
    /// Each child will be linked via PartMetadata/ChildMap.
    /// </summary>
    public List<ChildBlueprintDefinition> ChildBlueprints { get; } = new();
    
    // ... existing methods ...
}

/// <summary>
/// Defines a child entity that should be spawned with the parent.
/// </summary>
public struct ChildBlueprintDefinition
{
    /// <summary>
    /// TkbType of the child blueprint.
    /// </summary>
    public long ChildTkbType;
    
    /// <summary>
    /// Instance ID for this child (e.g., 0 for Turret0, 1 for Turret1).
    /// </summary>
    public int InstanceId;
    
    /// <summary>
    /// Optional: Custom initialization data for this child.
    /// Applied after blueprint, before ELM.
    /// </summary>
    public object? InitializationData;
}
```

**Usage Example** (in application TKB setup):
```csharp
var tankTemplate = new TkbTemplate("M1_Abrams", TANK_TYPE);
tankTemplate.AddComponent(new Health { Value = 100 });

// Define that a Tank should have two turrets
tankTemplate.ChildBlueprints.Add(new ChildBlueprintDefinition
{
    ChildTkbType = TURRET_TYPE,
    InstanceId = 0  // Main turret
});
tankTemplate.ChildBlueprints.Add(new ChildBlueprintDefinition
{
    ChildTkbType = TURRET_TYPE,
    InstanceId = 1  // Secondary turret
});

tkb.Register(tankTemplate);
```

**Dependencies**: FDP-TKB-005

**Success Criteria**:
- ✅ `ChildBlueprints` list added to `TkbTemplate`
- ✅ `ChildBlueprintDefinition` struct defined correctly
- ✅ XML documentation explains usage
- ✅ Unit tests validate child definitions
- ✅ Compiles without errors

**Test Requirements**:
```csharp
[TestClass]
public class TkbTemplateChildBlueprintsTests
{
    [TestMethod]
    public void ChildBlueprints_CanBeAdded()
    {
        var template = new TkbTemplate("Tank", 100);
        template.ChildBlueprints.Add(new ChildBlueprintDefinition
        {
            ChildTkbType = 200,
            InstanceId = 0
        });
        
        Assert.AreEqual(1, template.ChildBlueprints.Count);
        Assert.AreEqual(200, template.ChildBlueprints[0].ChildTkbType);
        Assert.AreEqual(0, template.ChildBlueprints[0].InstanceId);
    }
    
    [TestMethod]
    public void ChildBlueprints_SupportsMultipleChildren()
    {
        var template = new TkbTemplate("Tank", 100);
        template.ChildBlueprints.Add(new ChildBlueprintDefinition { ChildTkbType = 200, InstanceId = 0 });
        template.ChildBlueprints.Add(new ChildBlueprintDefinition { ChildTkbType = 200, InstanceId = 1 });
        
        Assert.AreEqual(2, template.ChildBlueprints.Count);
    }
}
```

**Estimated Effort**: 0.5 day

---

## UPDATE FDP-IF-006 (Phase 0)

**Change Description**: Fix `ISerializationProvider.Apply` signature for thread-safety

**Current (Incorrect)**:
```csharp
void Apply(Entity entity, ReadOnlySpan<byte> buffer, EntityRepository repo);
```

**Corrected**:
```csharp
/// <summary>
/// Applies serialized descriptor data to an entity.
/// CRITICAL: Uses IEntityCommandBuffer for thread-safe mutations.
/// </summary>
void Apply(Entity entity, ReadOnlySpan<byte> buffer, IEntityCommandBuffer cmd);
```

**Rationale**:
The `GhostPromotionSystem` (and most toolkit systems) operates on `ISimulationView` and uses `IEntityCommandBuffer` for deferred mutations. Passing raw `EntityRepository` breaks the command pattern and is not thread-safe in multi-threaded scenarios.

**Impact**: Update all references and implementations to use `IEntityCommandBuffer` instead of `EntityRepository`.

---

## INSERT IN PHASE 3 (After FDP-REP-002)

### FDP-REP-008: Implement Reflection-Based Auto-Discovery

**Description**:
Create the automatic translator discovery and registration system for "zero boilerplate" descriptor mapping.

**Files**:
1. `ModuleHost/FDP.Toolkit.Replication/Services/ReplicationBootstrap.cs`
2. `ModuleHost/FDP.Toolkit.Replication/Translators/AutoTranslator.cs`
3. `ModuleHost/FDP.Toolkit.Replication/Attributes/FdpDescriptorAttribute.cs`

**Components**:

**1. FdpDescriptorAttribute**:
```csharp
namespace Fdp.Toolkit.Replication.Attributes
{
    /// <summary>
    /// Marks a DDS topic struct as an FDP network descriptor.
    /// Enables automatic translator generation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class FdpDescriptorAttribute : Attribute
    {
        /// <summary>
        /// Descriptor ordinal (high 32 bits of PackedKey).
        /// </summary>
        public int Ordinal { get; }
        
        /// <summary>
        /// Is this descriptor part of the hard mandatory set for ghost promotion?
        /// </summary>
        public bool IsMandatory { get; set; }
        
        /// <summary>
        /// If mandatory but soft, how many frames to wait before giving up?
        /// </summary>
        public uint SoftTimeoutFrames { get; set; } = 600;
        
        public FdpDescriptorAttribute(int ordinal)
        {
            Ordinal = ordinal;
        }
    }
    
    /// <summary>
    /// Marks a descriptor as unreliable (uses rolling-window refresh).
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class FdpUnreliableAttribute : Attribute
    {
    }
}
```

**2. AutoTranslator<T>**:
```csharp
namespace Fdp.Toolkit.Replication.Translators
{
    /// <summary>
    /// Automatically generated translator for descriptors that follow
    /// the "Unified Component" pattern (descriptor IS the ECS component).
    /// </summary>
    public class AutoTranslator<T> : IDescriptorTranslator
        where T : struct
    {
        private readonly long _ordinal;
        private readonly string _topicName;
        private readonly NetworkEntityMap _entityMap;
        private readonly ISerializationRegistry _serialization;
        
        public long DescriptorOrdinal => _ordinal;
        public string TopicName => _topicName;
        
        public AutoTranslator(
            long ordinal,
            string topicName,
            NetworkEntityMap entityMap,
            ISerializationRegistry serialization)
        {
            _ordinal = ordinal;
            _topicName = topicName;
            _entityMap = entityMap;
            _serialization = serialization;
        }
        
        public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, ISimulationView view)
        {
            var samples = reader.Take(100);
            try
            {
                foreach (var sample in samples)
                {
                    var descriptor = (T)sample.Data;
                    long entityId = GetEntityId(descriptor);  // Via reflection
                    
                    if (!_entityMap.TryGet(entityId, out var entity))
                    {
                        // Ghost creation logic
                        entity = CreateGhost(entityId, cmd);
                        _entityMap.Register(entityId, entity);
                    }
                    
                    var lifecycle = view.GetRepository().GetLifecycleState(entity);
                    
                    if (lifecycle == EntityLifecycle.Ghost)
                    {
                        // Stash in BinaryGhostStore
                        StashDescriptor(entity, descriptor, cmd);
                    }
                    else if (lifecycle == EntityLifecycle.Active || lifecycle == EntityLifecycle.Constructing)
                    {
                        // Direct apply
                        cmd.SetComponent(entity, descriptor);
                    }
                    
                    // Update ownership
                    UpdateAuthority(entity, sample.PublisherId, cmd);
                }
            }
            finally
            {
                reader.ReturnLoan(samples);
           }
        }
        
        public void ScanAndPublish(ISimulationView view, IDataWriter writer)
        {
            // Query entities with component T and NetworkIdentity
            // If HasAuthority, write to network
            // Implement dirty tracking for efficiency
        }
        
        public void ApplyToEntity(Entity entity, object data, IEntityCommandBuffer cmd)
        {
            cmd.SetComponent(entity, (T)data);
        }
        
        // ... helper methods ...
    }
}
```

**3. ReplicationBootstrap**:
```csharp
namespace Fdp.Toolkit.Replication.Services
{
    /// <summary>
    /// Scans assemblies for [FdpDescriptor] attributes and automatically
    /// registers translators.
    /// </summary>
    public class ReplicationBootstrap
    {
        private readonly NetworkEntityMap _entityMap;
        private readonly ISerializationRegistry _serialization;
        private readonly List<IDescriptorTranslator> _translators = new();
        
        public ReplicationBootstrap(
            NetworkEntityMap entityMap,
            ISerializationRegistry serialization)
        {
            _entityMap = entityMap;
            _serialization = serialization;
        }
        
        /// <summary>
        /// Scans the given assembly for descriptor types and creates translators.
        /// </summary>
        public void ScanAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<FdpDescriptorAttribute>();
                if (attr == null) continue;
                
                if (!type.IsValueType)
                {
                    throw new InvalidOperationException(
                        $"[FdpDescriptor] can only be applied to value types (structs): {type.Name}");
                }
                
                // Get topic name from [DdsTopic] attribute (CycloneDDS)
                var topicAttr = type.GetCustomAttribute<DdsTopicAttribute>();
                if (topicAttr == null)
                {
                    throw new InvalidOperationException(
                        $"Descriptor {type.Name} missing [DdsTopic] attribute");
                }
                
                // Create AutoTranslator<T> via reflection
                var translatorType = typeof(AutoTranslator<>).MakeGenericType(type);
                var translator = (IDescriptorTranslator)Activator.CreateInstance(
                    translatorType,
                    (long)attr.Ordinal,
                    topicAttr.TopicName,
                    _entityMap,
                    _serialization);
                
                _translators.Add(translator);
                
                Console.WriteLine($"[Bootstrap] Registered {type.Name} → Ordinal {attr.Ordinal}");
            }
        }
        
        public IReadOnlyList<IDescriptorTranslator> GetTranslators() => _translators;
    }
}
```

**Dependencies**: FDP-REP-002, FDP-IF-007

**Success Criteria**:
- ✅ `FdpDescriptorAttribute` and `FdpUnreliableAttribute` defined
- ✅ `AutoTranslator<T>` implements full translator pattern
- ✅ `ReplicationBootstrap` scans assemblies correctly
- ✅ Zero-boilerplate demo works (Phase 8 NetworkDemo)
- ✅ All tests pass

**Test Requirements**:
```csharp
[TestClass]
public class ReplicationBootstrapTests
{
    [FdpDescriptor(ordinal: 5, IsMandatory = true)]
    [DdsTopic("TestDescriptor")]
    public struct TestDescriptor
    {
        public long EntityId;
        public int Value;
    }
    
    [TestMethod]
    public void ScanAssembly_DiscoversDescriptors()
    {
        var bootstrap = new ReplicationBootstrap(new NetworkEntityMap(), new SerializationRegistry());
        bootstrap.ScanAssembly(typeof(TestDescriptor).Assembly);
        
        var translators = bootstrap.GetTranslators();
        Assert.IsTrue(translators.Any(t => t.DescriptorOrdinal == 5));
    }
    
    [TestMethod]
    public void AutoTranslator_ImplementsInterface()
    {
        var translator = new AutoTranslator<TestDescriptor>(5, "TestDescriptor", 
            new NetworkEntityMap(), new SerializationRegistry());
            
        Assert.IsNotNull(translator as IDescriptorTranslator);
        Assert.AreEqual(5, translator.DescriptorOrdinal);
        Assert.AreEqual("TestDescriptor", translator.TopicName);
    }
}
```

**Estimated Effort**: 2 days

---

## INSERT IN PHASE 5 (After FDP-REP-206)

### FDP-REP-306: Implement Hierarchical Authority Extensions

**Description**:
Create extension method for authority checking with parent-link traversal fallback.

**File**: `ModuleHost/FDP.Toolkit.Replication/Extensions/AuthorityExtensions.cs`

**Implementation**:
```csharp
namespace Fdp.Toolkit.Replication.Extensions
{
    public static class AuthorityExtensions
    {
        /// <summary>
        /// Checks if the local node has authority over the given entity.
        /// For sub-entities (parts), falls back to parent authority if no override exists.
        /// </summary>
        public static bool HasAuthority(this ISimulationView view, Entity entity)
        {
            var repo = view.GetRepository();
            
            if (!repo.HasComponent<NetworkAuthority>(entity))
                return false;  // No network authority = no control
            
            var auth = repo.GetComponent<NetworkAuthority>(entity);
            
            // Check if this is a sub-entity (part)
            if (repo.HasComponent<PartMetadata>(entity))
            {
                var partMeta = repo.GetComponent<PartMetadata>(entity);
                
                // Check if part has explicit ownership override
                if (repo.HasManagedComponent<DescriptorOwnership>(partMeta.ParentEntity))
                {
                    var ownership = repo.GetManagedComponent<DescriptorOwnership>(partMeta.ParentEntity);
                    
                    // Use stored DescriptorOrdinal instead of reflection lookup
                    long partKey = PackedKey.Create(partMeta.DescriptorOrdinal, partMeta.InstanceId);
                    
                    if (ownership.Map.TryGetValue(partKey, out int partOwner))
                    {
                        // Explicit ownership for this part
                        return partOwner == auth.LocalNodeId;
                    }
                }
                
                // No explicit override: fall back to parent's primary authority
                var parentAuth = repo.GetComponent<NetworkAuthority>(partMeta.ParentEntity);
                return parentAuth.PrimaryOwnerId == auth.LocalNodeId;
            }
            
            // Main entity: check if we are the primary owner
            return auth.PrimaryOwnerId == auth.LocalNodeId;
        }
        
        /// <summary>
        /// Checks if the local node has authority over a specific descriptor of an entity.
        /// </summary>
        public static bool HasAuthority(this ISimulationView view, Entity entity, long packedDescriptorKey)
        {
            var repo = view.GetRepository();
            
            if (!repo.HasComponent<NetworkAuthority>(entity))
                return false;
            
            var auth = repo.GetComponent<NetworkAuthority>(entity);
            
            // Check for explicit descriptor ownership
            if (repo.HasManagedComponent<DescriptorOwnership>(entity))
            {
                var ownership = repo.GetManagedComponent<DescriptorOwnership>(entity);
                if (ownership.Map.TryGetValue(packedDescriptorKey, out int owner))
                {
                    return owner == auth.LocalNodeId;
                }
            }
            
            // Fall back to primary owner
            return auth.PrimaryOwnerId == auth.LocalNodeId;
        }
    }
}
```

**Note on PartMetadata Component** (updated in FDP-REP-301):
```csharp
public struct PartMetadata
{
    public Entity ParentEntity;
    public int InstanceId;
    
    /// <summary>
    /// Descriptor ordinal for this part type.
    /// Stored directly to avoid reflection lookups in HasAuthority().
    /// </summary>
    public int DescriptorOrdinal;
}
```

**Dependencies**: FDP-REP-301, FDP-REP-302

**Success Criteria**:
- ✅ Extension methods defined correctly
- ✅ Parent-link traversal works for sub-entities
- ✅ Explicit ownership overrides respected
- ✅ Fallback to primary owner works correctly
- ✅ Unit tests pass

**Test Requirements**:
```csharp
[TestClass]
public class AuthorityExtensionsTests
{
    [TestMethod]
    public void HasAuthority_ReturnsTrueForPrimaryOwner()
    {
        var repo = new EntityRepository();
        var entity = repo.CreateEntity();
        repo.AddComponent(entity, new NetworkAuthority
        {
            PrimaryOwnerId = 1,
            LocalNodeId = 1
        });
        
        var view = CreateMockView(repo);
        Assert.IsTrue(view.HasAuthority(entity));
    }
    
    [TestMethod]
    public void HasAuthority_FallbackToParentForSubEntity()
    {
        var repo = new EntityRepository();
        var parent = repo.CreateEntity();
        repo.AddComponent(parent, new NetworkAuthority
        {
            PrimaryOwnerId = 1,
            LocalNodeId = 1
        });
        
        var child = repo.CreateEntity();
        repo.AddComponent(child, new PartMetadata
        {
            ParentEntity = parent,
            InstanceId = 0
        });
        repo.AddComponent(child, new NetworkAuthority
        {
            PrimaryOwnerId = 2,  // Different, but fallback applies
            LocalNodeId = 1
        });
        
        var view = CreateMockView(repo);
        Assert.IsTrue(view.HasAuthority(child));  // Uses parent's authority
    }
    
    [TestMethod]
    public void HasAuthority_ExplicitOverrideForPart()
    {
        var repo = new EntityRepository();
        var parent = repo.CreateEntity();
        repo.AddComponent(parent, new NetworkAuthority
        {
            PrimaryOwnerId = 1,
            LocalNodeId = 2
        });
        
        var ownership = new DescriptorOwnership();
        long partKey = PackedKey.Create(10, 0);  // Part ordinal 10, instance 0
        ownership.Map[partKey] = 2;  // Explicitly owned by node 2
        repo.SetManagedComponent(parent, ownership);
        
        var child = repo.CreateEntity();
        repo.AddComponent(child, new PartMetadata
        {
            ParentEntity = parent,
            InstanceId = 0
        });
        repo.AddComponent(child, new NetworkAuthority
        {
            PrimaryOwnerId = 1,
            LocalNodeId = 2
        });
        
        var view = CreateMockView(repo);
        Assert.IsTrue(view.HasAuthority(child));  // Uses explicit override
    }
}
```

**Estimated Effort**: 1 day

---

## UPDATE FDP-REP-102 (Phase 4)

**Change Description**: Add `IdentifiedAtFrame` field to `BinaryGhostStore`

**Rationale**:
The `GhostPromotionSystem` needs to track how long an entity has been "Identified" (Master arrived, waiting for mandatory soft requirements). Without this timestamp, soft timeout logic cannot function.

**Updated Component**:
```csharp
public class BinaryGhostStore
{
    /// <summary>
    /// Map of stashed descriptors: PackedKey → offset/length in NativeMemoryPool.
    /// </summary>
    public Dictionary<long, GhostEntry> Stashed { get; } = new();
    
    /// <summary>
    /// Frame when the FIRST descriptor for this entity arrived.
    /// Used for ghost timeout (cleanup stale ghosts).
    /// </summary>
    public uint FirstSeenFrame;
    
    /// <summary>
    /// Frame when the EntityMaster arrived (entity became "Identified").
    /// Used for soft requirement timeout calculation.
    /// Zero if Master has not yet arrived.
    /// </summary>
    public uint IdentifiedAtFrame;  // NEW FIELD
}
```

**Impact**: Update `GhostPromotionSystem` to set this field when `NetworkSpawnRequest` is added, and use it for soft timeout checks.

---

## Priority Updates to TASK_TRACKER.md

1. Update Phase 0 task count: 11 → 12 tasks (added FDP-IF-007, FDP-TKB-006)
2. Update Phase 3 task count to include FDP-REP-008
3. Update Phase 5 task count to include FDP-REP-306
4. Add note: "Smart Egress" moved from Future Work to Phase 5
5. Mark FDP-IF-006 and FDP-REP-102 as "Updated" with signature/field changes

---

## Summary of Critical Fixes

| Gap # | Task ID | Priority | Impact |
|-------|---------|----------|--------|
| 1 | FDP-TKB-006 | High | Without this, sub-entity spawning cannot be automated |
| 2 | FDP-IF-006 (Update) | Critical | Thread-safety issue in ghost promotion |
| 3 | FDP-REP-008 | High | Zero-boilerplate demo requires this |
| 4 | FDP-REP-306 | Medium | Authority checks need parent fallback |
| 5 | FDP-REP-102 (Update) | High | Soft requirement timeout logic broken |
| 6 | Smart Egress Priority | Medium | Should not be deferred to future |
| 7 | FDP-IF-007 | Critical | Prevents circular dependency in Layer 0 |

All seven gaps must be addressed for the refactoring to be production-ready.

