# FDP Engine Distributed Recording and Playback - Task Details

**Design Reference:** See [DESIGN.md](./DESIGN.md) for complete architectural context.

---

## Phase 1: Kernel Foundation

### FDP-DRP-001: Entity Index ID Reservation

**Priority:** Critical  
**Depends On:** None  
**Design Reference:** DESIGN.md § 3.1, § 4.2

**Description:**
Add the ability to reserve a range of entity IDs to prevent allocation collisions between recorded entities and new live entities during replay.

**Implementation:**

1. **File:** `Fdp.Kernel/FdpConfig.cs`
   - Add constant: `public const int SYSTEM_ID_RANGE = 65536;`

2. **File:** `Fdp.Kernel/EntityIndex.cs`
   - Add method:
     ```csharp
     public void ReserveIdRange(int maxId) {
         lock (_createLock) {
             if (_maxIssuedIndex < maxId) {
                 _maxIssuedIndex = maxId;
             }
         }
     }
     ```

3. **File:** `Fdp.Kernel/EntityRepository.cs`
   - Add wrapper method:
     ```csharp
     public void ReserveIdRange(int maxId) {
         _entityIndex.ReserveIdRange(maxId);
     }
     ```

**Success Conditions:**
- [ ] `SYSTEM_ID_RANGE` constant defined and accessible
- [ ] Calling `ReserveIdRange(1000)` on empty repository causes next `CreateEntity()` to return ID 1001
- [ ] Thread-safe operation (lock verified)
- [ ] Unit test: Reserve range, create entities, verify IDs > reserved range
- [ ] Unit test: Multiple reservations pick highest value

**Testing:**
```csharp
[Test]
public void ReserveIdRange_PreventsCollision() {
    var repo = new EntityRepository();
    repo.ReserveIdRange(1000);
    var e1 = repo.CreateEntity();
    Assert.That(e1.Index, Is.GreaterThan(1000));
}
```

---

### FDP-DRP-002: Entity Hydration for Replay

**Priority:** Critical  
**Depends On:** FDP-DRP-001  
**Design Reference:** DESIGN.md § 4.2

**Description:**
Add the ability to force-create an entity at a specific ID and generation to match recorded entity handles during replay initialization.

**Implementation:**

1. **File:** `Fdp.Kernel/EntityRepository.cs`
   - Add method:
     ```csharp
     public Entity HydrateEntity(int id, int generation) {
         // Ensure ID is reserved
         if (id > _entityIndex.MaxIssuedIndex) {
             _entityIndex.ReserveIdRange(id);
         }
         
         // Force restore via internal API
         _entityIndex.ForceRestoreEntity(
             index: id,
             isActive: true,
             generation: generation,
             componentMask: default,
             disType: default
         );
         
         // Emit lifecycle event
         if (_lifecycleStream != null) {
             _lifecycleStream.Write(new EntityLifecycleEvent {
                 Entity = new Entity(id, (ushort)generation),
                 Type = LifecycleEventType.Restored,
                 Generation = generation
             });
         }
         
         return new Entity(id, (ushort)generation);
     }
     ```

**Success Conditions:**
- [ ] Can create entity at specific ID out-of-order (e.g., ID 5000 when max is 100)
- [ ] Generation matches requested value
- [ ] Entity is immediately alive and queryable
- [ ] Lifecycle event emitted correctly
- [ ] Unit test: Hydrate entity, verify ID and generation
- [ ] Unit test: Hydrate multiple entities with gaps, verify all valid

**Testing:**
```csharp
[Test]
public void HydrateEntity_CreatesAtSpecificId() {
    var repo = new EntityRepository();
    var e = repo.HydrateEntity(id: 5000, generation: 3);
    Assert.That(e.Index, Is.EqualTo(5000));
    Assert.That(e.Generation, Is.EqualTo(3));
    Assert.That(repo.IsAlive(e), Is.True);
}
```

---

### FDP-DRP-003: Recorder Minimum ID Filter

**Priority:** Critical  
**Depends On:** FDP-DRP-001  
**Design Reference:** DESIGN.md § 3.1, § 11.1

**Description:**
Update `RecorderSystem` to skip chunks below a configurable minimum ID threshold, preventing system entities from being recorded.

**Implementation:**

1. **File:** `Fdp.Kernel/FlightRecorder/RecorderSystem.cs`
   - Add property:
     ```csharp
     public int MinRecordableId { get; set; } = FdpConfig.SYSTEM_ID_RANGE;
     ```
   
   - Update `RecordDeltaFrame()`:
     ```csharp
     private void RecordTable(IComponentTable table, int chunkCapacity) {
         // Calculate first recordable chunk
         int startChunkIndex = MinRecordableId / chunkCapacity;
         
         for (int c = startChunkIndex; c < table.ChunkCount; c++) {
             // Record this chunk...
         }
     }
     ```

**Success Conditions:**
- [ ] Default `MinRecordableId` is 65536
- [ ] Chunks with indices entirely below threshold are skipped
- [ ] Recording file does not contain data for IDs below threshold
- [ ] Performance impact negligible (simple integer comparison)
- [ ] Unit test: Record repo with entities at IDs 0-100 and 70000-70100, verify only high range in file
- [ ] Integration test: Record session, verify system entities absent from recording

**Testing:**
```csharp
[Test]
public void RecorderSystem_SkipsSystemRange() {
    var repo = CreateRepoWithSystemEntities(); // IDs 0-100
    repo.ReserveIdRange(65536);
    var gameEntity = repo.CreateEntity(); // ID 65536
    
    var recorder = new RecorderSystem { MinRecordableId = 65536 };
    recorder.RecordDeltaFrame(repo, ...);
    
    // Verify recording contains only gameEntity
    var playback = LoadRecording(...);
    Assert.That(playback.Contains(Entity(0)), Is.False);
    Assert.That(playback.Contains(gameEntity), Is.True);
}
```

---

## Phase 2: Replication Toolkit

### FDP-DRP-004: Data Policy Enforcement

**Priority:** Critical  
**Depends On:** None  
**Design Reference:** DESIGN.md § 3.2

**Description:**
Mark replication toolkit BUFFER components with `[DataPolicy(DataPolicy.NoRecord)]` to prevent them from being included in recordings. Identity and authority components MUST be recorded to support replay authority checking.

**Implementation:**

1. **Files to Update (Buffer Components Only):**
   - `FDP.Toolkit.Replication/Components/NetworkPosition.cs`
   - `FDP.Toolkit.Replication/Components/NetworkVelocity.cs`
   - `FDP.Toolkit.Replication/Components/NetworkOrientation.cs`

2. **Files to KEEP Recordable (Authority Components):**
   - `FDP.Toolkit.Replication/Components/NetworkIdentity.cs` - **MUST RECORD**
   - `FDP.Toolkit.Replication/Components/NetworkAuthority.cs` - **MUST RECORD**
   - `FDP.Toolkit.Replication/Components/DescriptorOwnership.cs` - **MUST RECORD**

**Rationale:**
- Buffer components (`NetworkPosition`, etc.) are derived from application state and should be regenerated during replay
- Authority components are required by `ReplayBridgeSystem` to check `HasAuthority()` in the Shadow World
- Without recorded authority data, replay cannot determine which components to inject

2. **Pattern:**
   ```csharp
   using Fdp.Kernel;
   
   namespace FDP.Toolkit.Replication.Components
   {
       [DataPolicy(DataPolicy.NoRecord)]
       public struct NetworkPosition {
           public Vector3 Value;
       }
   }
   ```

**Success Conditions:**
- [ ] All toolkit components have `[DataPolicy(NoRecord)]` attribute
- [ ] `ComponentTypeRegistry.IsRecordable()` returns false for these types
- [ ] Recording file does not contain these components
- [ ] Unit test: Register toolkit components, verify `IsRecordable() == false`
- [ ] Integration test: Record session with network components, verify they're excluded

**Testing:**
```csharp
[Test]
public void NetworkComponents_NotRecordable() {
    ComponentTypeRegistry.Register<NetworkPosition>();
    var typeId = ComponentTypeRegistry.GetTypeId<NetworkPosition>();
    Assert.That(ComponentTypeRegistry.IsRecordable(typeId), Is.False);
}
```

---

### FDP-DRP-005: FdpDescriptor Attribute

**Priority:** High  
**Depends On:** None  
**Design Reference:** DESIGN.md § 7.1

**Description:**
Create the `[FdpDescriptor]` attribute to mark structs for automatic translator generation (zero boilerplate networking).

**Implementation:**

1. **File:** `FDP.Interfaces/Attributes/FdpDescriptorAttribute.cs`
   ```csharp
   using System;
   
   namespace Fdp.Interfaces
   {
       [AttributeUsage(AttributeTargets.Struct)]
       public class FdpDescriptorAttribute : Attribute
       {
           public int Ordinal { get; }
           public string TopicName { get; }
           public bool IsMandatory { get; set; } = false;
           
           public FdpDescriptorAttribute(int ordinal, string topicName)
           {
               Ordinal = ordinal;
               TopicName = topicName;
           }
       }
   }
   ```

**Success Conditions:**
- [ ] Attribute can be applied to struct types
- [ ] Required properties: Ordinal (int), TopicName (string)
- [ ] Optional property: IsMandatory (bool)
- [ ] Reflection can retrieve attribute data
- [ ] Unit test: Apply attribute to test struct, verify properties via reflection

**Testing:**
```csharp
[Test]
public void FdpDescriptorAttribute_ReflectionAccessible() {
    [FdpDescriptor(10, "TestTopic", IsMandatory = true)]
    struct TestStruct { }
    
    var attr = typeof(TestStruct).GetCustomAttribute<FdpDescriptorAttribute>();
    Assert.That(attr.Ordinal, Is.EqualTo(10));
    Assert.That(attr.TopicName, Is.EqualTo("TestTopic"));
    Assert.That(attr.IsMandatory, Is.True);
}
```

---

### FDP-DRP-006: Generic Descriptor Translator

**Priority:** Critical  
**Depends On:** FDP-DRP-004, FDP-DRP-005  
**Design Reference:** DESIGN.md § 7.2

**Description:**
Implement the generic translator that provides 1:1 mapping between ECS components and network descriptors for attributed types. Includes critical Ghost Stash logic for orphaned descriptors.

**Implementation:**

1. **File:** `FDP.Toolkit.Replication/Translators/GenericDescriptorTranslator.cs`
   ```csharp
   using Fdp.Kernel;
   using Fdp.Interfaces;
   using ModuleHost.Core.Abstractions;
   using FDP.Toolkit.Replication.Components;
   using FDP.Toolkit.Replication.Services;
   using FDP.Toolkit.Replication.Extensions;
   
   namespace FDP.Toolkit.Replication.Translators
   {
       public class GenericDescriptorTranslator<T> : IDescriptorTranslator 
           where T : unmanaged
       {
           private readonly NetworkEntityMap _entityMap;
           private readonly ISerializationProvider _serializer;
           
           public string TopicName { get; }
           public long DescriptorOrdinal { get; }
           
           public GenericDescriptorTranslator(int ordinal, string topicName, 
               NetworkEntityMap entityMap, ISerializationProvider serializer)
           {
               DescriptorOrdinal = ordinal;
               TopicName = topicName;
               _entityMap = entityMap;
               _serializer = serializer;
           }
           
           public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, 
               ISimulationView view)
           {
               foreach (var sample in reader.TakeSamples()) {
                   if (!_entityMap.TryGetEntity(sample.EntityId, out Entity entity))
                       continue;
                   
                   var data = (T)sample.Data;
                   
                   // CRITICAL: Check if entity is a Ghost (accumulating descriptors)
                   if (view.HasManagedComponent<BinaryGhostStore>(entity)) {
                       var store = view.GetManagedComponent<BinaryGhostStore>(entity);
                       long key = PackedKey.Create(DescriptorOrdinal, sample.InstanceId);
                       
                        int size = _serializer.GetSize(data);
                        byte[] buffer = new byte[size]; // Allocation is unavoidable for storage
                        _serializer.Encode(data, buffer);

                       // STASH data - don't apply yet (waiting for mandatory descriptors)
                        store.StashedData[key] = buffer;

                   }
                   else {
                       // Entity is active - apply data directly
                       // Handle sub-entities if InstanceId > 0
                       Entity target = ResolveSubEntity(view, entity, sample.InstanceId);
                       cmd.SetComponent(target, data);
                   }
               }
           }
           
           public void ScanAndPublish(ISimulationView view, IDataWriter writer)
           {
               var query = view.Query()
                   .With<T>()
                   .With<NetworkIdentity>()
                   .With<NetworkAuthority>()
                   .Build();
               
               foreach (var entity in query) {
                   if (view.HasAuthority(entity, DescriptorOrdinal)) {
                       var component = view.GetComponentRO<T>(entity);
                       writer.Write(component);
                   }
               }
           }
           
           public void ApplyToEntity(Entity entity, object data, 
               EntityRepository repo)
           {
               if (data is T val) {
                   repo.AddComponent(entity, val);
               }
           }
       }
   }
   ```

**Success Conditions:**
- [ ] Compiles with unmanaged constraint
- [ ] Constructor accepts ISerializationProvider
- [ ] Correctly stashes data for Ghost entities (doesn't apply immediately)
- [ ] Correctly applies data for active entities
- [ ] Respects granular ownership (checks `HasAuthority`)
- [ ] Handles missing entities gracefully
- [ ] Unit test: Ghost entity stashes data instead of applying
- [ ] Unit test: Active entity applies data immediately
- [ ] Unit test: Mock reader with sample, verify component updated
- [ ] Unit test: Mock view with owned entity, verify write called
- [ ] Unit test: Mock view with unowned entity, verify write NOT called

**Testing:**
```csharp
[Test]
public void GenericTranslator_IngressUpdatesComponent() {
    struct TestData { public int Value; }
    
    var repo = new EntityRepository();
    repo.RegisterComponent<TestData>();
    var entity = repo.CreateEntity();
    
    var entityMap = new NetworkEntityMap();
    entityMap.Register(100L, entity);
    
    var translator = new GenericDescriptorTranslator<TestData>(
        1, "TestTopic", entityMap
    );
    
    var mockReader = CreateMockReader(entityId: 100L, data: new TestData { Value = 42 });
    translator.PollIngress(mockReader, repo.GetCommandBuffer(), repo.AsView());
    
    var data = repo.GetComponent<TestData>(entity);
    Assert.That(data.Value, Is.EqualTo(42));
}
```

---

### FDP-DRP-007: Assembly Scanning for Auto-Registration

**Priority:** High  
**Depends On:** FDP-DRP-005, FDP-DRP-006  
**Design Reference:** DESIGN.md § 7.3

**Description:**
Implement reflection-based assembly scanner that discovers types with `[FdpDescriptor]` and creates translators automatically.

**Implementation:**

1. **File:** `FDP.Toolkit.Replication/ReplicationBootstrap.cs`
   ```csharp
   using System;
   using System.Collections.Generic;
   using System.Reflection;
   using Fdp.Interfaces;
   using FDP.Toolkit.Replication.Services;
   using FDP.Toolkit.Replication.Translators;
   
   namespace FDP.Toolkit.Replication
   {
       public static class ReplicationBootstrap
       {
           public static List<IDescriptorTranslator> CreateAutoTranslators(
               Assembly assembly, NetworkEntityMap entityMap)
           {
               var translators = new List<IDescriptorTranslator>();
               
               foreach (var type in assembly.GetTypes()) {
                   if (!type.IsValueType) continue;
                   
                   var attr = type.GetCustomAttribute<FdpDescriptorAttribute>();
                   if (attr == null) continue;
                   
                   var translatorType = typeof(GenericDescriptorTranslator<>)
                       .MakeGenericType(type);
                   
                   // Create serialization provider for this type
                   var serializerType = typeof(CycloneSerializationProvider<>)
                       .MakeGenericType(type);
                   var serializer = (ISerializationProvider)Activator.CreateInstance(serializerType);
                   
                   var translator = (IDescriptorTranslator)Activator.CreateInstance(
                       translatorType,
                       attr.Ordinal,
                       attr.TopicName,
                       entityMap,
                       serializer  // Pass serializer for Ghost Stash
                   );
                   
                   translators.Add(translator);
                   
                   Console.WriteLine(
                       $"[Replication] Auto-registered: {type.Name} " +
                       $"(Topic: {attr.TopicName}, ID: {attr.Ordinal})"
                   );
               }
               
               return translators;
           }
       }
   }
   ```

**Success Conditions:**
- [ ] Scans all types in assembly
- [ ] Filters to value types only
- [ ] Creates translator for each attributed type
- [ ] Handles generic type instantiation correctly
- [ ] Logs registration for debugging
- [ ] Unit test: Assembly with 3 attributed types, verify 3 translators created
- [ ] Unit test: Assembly with 0 attributed types, verify empty list
- [ ] Unit test: Verify translator properties match attribute values

**Testing:**
```csharp
[Test]
public void Bootstrap_CreatesTranslatorsForAttributedTypes() {
    // Create test assembly with attributed types
    [FdpDescriptor(1, "Topic1")]
    struct Type1 { }
    
    [FdpDescriptor(2, "Topic2")]
    struct Type2 { }
    
    var translators = ReplicationBootstrap.CreateAutoTranslators(
        typeof(Type1).Assembly,
        new NetworkEntityMap()
    );
    
    Assert.That(translators.Count, Is.EqualTo(2));
    Assert.That(translators[0].DescriptorOrdinal, Is.EqualTo(1));
    Assert.That(translators[1].TopicName, Is.EqualTo("Topic2"));
}
```

---

## Phase 3: Network Demo - Infrastructure

### FDP-DRP-008: Recording Metadata Structure

**Priority:** High  
**Depends On:** None  
**Design Reference:** DESIGN.md § 8

**Description:**
Define the sidecar file structure for recording metadata containing the high water mark and session info.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo/Configuration/RecordingMetadata.cs`
   ```csharp
   using System;
   
   namespace Fdp.Examples.NetworkDemo.Configuration
   {
       [Serializable]
       public class RecordingMetadata
       {
           public int MaxEntityId { get; set; }
           public DateTime Timestamp { get; set; }
           public int NodeId { get; set; }
       }
   }
   ```

2. **File:** `Fdp.Examples.NetworkDemo/Configuration/MetadataManager.cs`
   ```csharp
   using System.IO;
   using System.Text.Json;
   
   public static class MetadataManager
   {
       public static void Save(string path, RecordingMetadata metadata)
       {
           var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions {
               WriteIndented = true
           });
           File.WriteAllText(path, json);
       }
       
       public static RecordingMetadata Load(string path)
       {
           if (!File.Exists(path)) {
               throw new FileNotFoundException($"Metadata not found: {path}");
           }
           
           var json = File.ReadAllText(path);
           return JsonSerializer.Deserialize<RecordingMetadata>(json);
       }
   }
   ```

**Success Conditions:**
- [ ] Serializable structure with all required fields
- [ ] JSON format for human readability
- [ ] Save/Load utilities work correctly
- [ ] File naming convention: `{recordingName}.fdp.meta`
- [ ] Unit test: Save then load, verify data integrity
- [ ] Unit test: Load missing file throws expected exception

**Testing:**
```csharp
[Test]
public void Metadata_SaveLoad_PreservesData() {
    var meta = new RecordingMetadata {
        MaxEntityId = 5000,
        Timestamp = DateTime.UtcNow,
        NodeId = 1
    };
    
    MetadataManager.Save("test.meta", meta);
    var loaded = MetadataManager.Load("test.meta");
    
    Assert.That(loaded.MaxEntityId, Is.EqualTo(5000));
    Assert.That(loaded.NodeId, Is.EqualTo(1));
}
```

---

### FDP-DRP-009: Demo Component Definitions

**Priority:** Critical  
**Depends On:** FDP-DRP-005  
**Design Reference:** DESIGN.md § 3.2, § 5.2

**Description:**
Define the internal and network component structures for the tank demo.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo/Components/DemoPosition.cs`
   ```csharp
   using System.Numerics;
   
   namespace Fdp.Examples.NetworkDemo.Components
   {
       /// <summary>
       /// Internal physics/logic position. This is RECORDED.
       /// </summary>
       public struct DemoPosition
       {
           public Vector3 Value;
       }
   }
   ```

2. **File:** `Fdp.Examples.NetworkDemo/Descriptors/GeoStateDescriptor.cs`
   ```csharp
   using CycloneDDS.Schema;
   
   namespace Fdp.Examples.NetworkDemo.Descriptors
   {
       /// <summary>
       /// Network representation using WGS84 geodetic coordinates.
       /// </summary>
       [DdsTopic("Tank_GeoState")]
       public struct GeoStateDescriptor
       {
           [DdsKey] public long EntityId;
           public double Latitude;
           public double Longitude;
           public double Altitude;
           public float Heading;
       }
   }
   ```

3. **File:** `Fdp.Examples.NetworkDemo/Components/TurretState.cs`
   ```csharp
   using Fdp.Interfaces;
   using CycloneDDS.Schema;
   
   namespace Fdp.Examples.NetworkDemo.Components
   {
       /// <summary>
       /// Auto-translated descriptor (both component and network format).
       /// </summary>
       [FdpDescriptor(10, "Tank_Turret")]
       [DdsTopic("Tank_Turret")]
       public struct TurretState
       {
           [DdsKey] public long EntityId;
           public float YawAngle;
           public float PitchAngle;
           public bool IsTargeting;
       }
   }
   ```

**Success Conditions:**
- [ ] `DemoPosition` is unmanaged struct
- [ ] `GeoStateDescriptor` has correct DDS attributes
- [ ] `TurretState` has both `[FdpDescriptor]` and `[DdsTopic]`
- [ ] All structs are blittable for ECS compatibility
- [ ] Unit test: Verify struct sizes and layouts
- [ ] Unit test: Verify attributes present via reflection

---

### FDP-DRP-010: Geographic Translator Implementation

**Priority:** High  
**Depends On:** FDP-DRP-009  
**Design Reference:** DESIGN.md § 5.2

**Description:**
Implement the manual translator that converts between internal flat coordinates and network geodetic coordinates.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo/Translators/GeodeticTranslator.cs`
   ```csharp
   using System.Numerics;
   using Fdp.Kernel;
   using Fdp.Interfaces;
   using Fdp.Modules.Geographic;
   using ModuleHost.Core.Network;
   using ModuleHost.Core.Abstractions;
   using FDP.Toolkit.Replication.Components;
   using FDP.Toolkit.Replication.Extensions;
   using Fdp.Examples.NetworkDemo.Descriptors;
   
   namespace Fdp.Examples.NetworkDemo.Translators
   {
       public class GeodeticTranslator : IDescriptorTranslator
       {
           private readonly IGeographicTransform _geoTransform;
           private readonly NetworkEntityMap _entityMap;
           
           public string TopicName => "Tank_GeoState";
           public long DescriptorOrdinal => 5;  // Chassis descriptor
           
           public GeodeticTranslator(IGeographicTransform transform, 
               NetworkEntityMap entityMap)
           {
               _geoTransform = transform;
               _entityMap = entityMap;
           }
           
           public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, 
               ISimulationView view)
           {
               foreach (var sample in reader.TakeSamples()) {
                   if (!_entityMap.TryGetEntity(sample.EntityId, out Entity entity))
                       continue;
                   
                   var geoData = (GeoStateDescriptor)sample.Data;
                   
                   // Convert Geodetic → Flat
                   var flatPos = _geoTransform.ToCartesian(
                       geoData.Latitude, 
                       geoData.Longitude, 
                       geoData.Altitude
                   );
                   
                   cmd.SetComponent(entity, new NetworkPosition { Value = flatPos });
               }
           }
           
           public void ScanAndPublish(ISimulationView view, IDataWriter writer)
           {
               var query = view.Query()
                   .With<NetworkPosition>()
                   .With<NetworkIdentity>()
                   .With<NetworkAuthority>()
                   .Build();
               
               foreach (var entity in query) {
                   if (!view.HasAuthority(entity, DescriptorOrdinal))
                       continue;
                   
                   var netPos = view.GetComponentRO<NetworkPosition>(entity);
                   var netId = view.GetComponentRO<NetworkIdentity>(entity);
                   
                   // Convert Flat → Geodetic
                   var (lat, lon, alt) = _geoTransform.ToGeodetic(netPos.Value);
                   
                   writer.Write(new GeoStateDescriptor {
                       EntityId = netId.Value,
                       Latitude = lat,
                       Longitude = lon,
                       Altitude = alt,
                       Heading = 0 // TODO: Extract from orientation component
                   });
               }
           }
           
           public void ApplyToEntity(Entity entity, object data, 
               EntityRepository repo)
           {
               var desc = (GeoStateDescriptor)data;
               var flatPos = _geoTransform.ToCartesian(
                   desc.Latitude, desc.Longitude, desc.Altitude
               );
               repo.AddComponent(entity, new NetworkPosition { Value = flatPos });
           }
       }
   }
   ```

**Success Conditions:**
- [ ] Correctly converts WGS84 ↔ Cartesian using transform
- [ ] Respects ownership (checks `HasAuthority`)
- [ ] Handles missing entities gracefully
- [ ] Integration test: Round-trip conversion preserves position within tolerance
- [ ] Integration test: Only owned entities published
- [ ] Integration test: Geographic transform configured with correct origin

**Testing:**
```csharp
[Test]
public void GeodeticTranslator_RoundTrip_PreservesPosition() {
    var transform = new WGS84Transform();
    transform.SetOrigin(52.5200, 13.4050, 0);
    
    var original = new Vector3(100, 0, 200); // 100m east, 200m north
    var (lat, lon, alt) = transform.ToGeodetic(original);
    var restored = transform.ToCartesian(lat, lon, alt);
    
    Assert.That(Vector3.Distance(original, restored), Is.LessThan(0.01f));
}
```

---

## Phase 4: Network Demo - Systems

### FDP-DRP-011: Transform Sync System

**Priority:** Critical  
**Depends On:** FDP-DRP-009  
**Design Reference:** DESIGN.md § 9.1

**Description:**
Implement the system that bridges application position ↔ network buffer, providing clean separation and enabling replay.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo/Systems/TransformSyncSystem.cs`
   ```csharp
   using System.Numerics;
   using Fdp.Kernel;
   using ModuleHost.Core.Abstractions;
   using FDP.Toolkit.Replication.Components;
   using FDP.Toolkit.Replication.Extensions;
   using Fdp.Examples.NetworkDemo.Components;
   
   namespace Fdp.Examples.NetworkDemo.Systems
   {
       [UpdateInPhase(SystemPhase.PostSimulation)]
       public class TransformSyncSystem : IModuleSystem
       {
           private const long CHASSIS_KEY = 5; // Chassis descriptor ordinal
           private const float SMOOTHING_RATE = 10.0f;
           
           public void Execute(ISimulationView view, float deltaTime)
           {
               SyncOwnedEntities(view);
               SyncRemoteEntities(view, deltaTime);
           }
           
           private void SyncOwnedEntities(ISimulationView view)
           {
               var query = view.Query()
                   .With<DemoPosition>()
                   .With<NetworkPosition>()
                   .With<NetworkAuthority>()
                   .Build();
               
               var cmd = view.GetCommandBuffer();
               
               foreach (var entity in query) {
                   // If we own the chassis, copy to network buffer
                   if (view.HasAuthority(entity, CHASSIS_KEY)) {
                       var appPos = view.GetComponentRO<DemoPosition>(entity);
                       cmd.SetComponent(entity, new NetworkPosition { 
                           Value = appPos.Value 
                       });
                   }
               }
           }
           
           private void SyncRemoteEntities(ISimulationView view, float deltaTime)
           {
               var query = view.Query()
                   .With<DemoPosition>()
                   .With<NetworkPosition>()
                   .With<NetworkAuthority>()
                   .Build();
               
               var cmd = view.GetCommandBuffer();
               
               foreach (var entity in query) {
                   // If we DON'T own it, smooth toward network position
                   if (!view.HasAuthority(entity, CHASSIS_KEY)) {
                       var netPos = view.GetComponentRO<NetworkPosition>(entity);
                       var currentPos = view.GetComponentRO<DemoPosition>(entity);
                       
                       var smoothed = Vector3.Lerp(
                           currentPos.Value,
                           netPos.Value,
                           deltaTime * SMOOTHING_RATE
                       );
                       
                       cmd.SetComponent(entity, new DemoPosition { Value = smoothed });
                   }
               }
           }
       }
   }
   ```

**Success Conditions:**
- [ ] Owned entities: `DemoPosition` → `NetworkPosition`
- [ ] Remote entities: `NetworkPosition` → `DemoPosition` (smoothed)
- [ ] Runs in PostSimulation phase (after physics)
- [ ] Respects granular ownership
- [ ] Unit test: Owned entity position synced to buffer
- [ ] Unit test: Remote entity smoothing works correctly
- [ ] Integration test: Ownership transfer triggers correct sync direction change

**Testing:**
```csharp
[Test]
public void TransformSync_OwnedEntity_CopiesAppToBuffer() {
    var repo = CreateTestRepo();
    var entity = CreateOwnedEntity(repo);
    
    repo.SetComponent(entity, new DemoPosition { Value = new Vector3(10, 0, 0) });
    
    var system = new TransformSyncSystem();
    system.Execute(repo.AsView(), 0.016f);
    
    var netPos = repo.GetComponent<NetworkPosition>(entity);
    Assert.That(netPos.Value, Is.EqualTo(new Vector3(10, 0, 0)));
}
```

---

### FDP-DRP-012: Replay Bridge System

**Priority:** Critical  
**Depends On:** FDP-DRP-001, FDP-DRP-002, FDP-DRP-008  
**Design Reference:** DESIGN.md § 9.2, § 4

**Description:**
Implement the core replay system that merges recorded data from shadow world into live world based on partial ownership.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo/Systems/ReplayBridgeSystem.cs`
   ```csharp
   using System;
   using Fdp.Kernel;
   using Fdp.Kernel.FlightRecorder;
   using ModuleHost.Core.Abstractions;
   using FDP.Toolkit.Replication.Components;
   using FDP.Toolkit.Replication.Extensions;
   using Fdp.Examples.NetworkDemo.Components;
   
   namespace Fdp.Examples.NetworkDemo.Systems
   {
       public class ReplayBridgeSystem : IModuleSystem, IDisposable
       {
           private readonly EntityRepository _shadowRepo;
           private readonly PlaybackController _controller;
           
           // Playback control
           private double _accumulator = 0.0;
           private float _playbackSpeed = 1.0f;
           private bool _isPaused = false;
           
           private const float RECORDED_DELTA = 1.0f / 60.0f;
           private const long CHASSIS_KEY = 5;
           private const long TURRET_KEY = 10;
           
           public ReplayBridgeSystem(string recordingPath)
           {
               _shadowRepo = new EntityRepository();
               RegisterShadowComponents();
               
               _controller = new PlaybackController(recordingPath);
           }
           
           private void RegisterShadowComponents()
           {
               _shadowRepo.RegisterComponent<NetworkIdentity>();
               _shadowRepo.RegisterComponent<NetworkAuthority>();
               _shadowRepo.RegisterComponent<DemoPosition>();
               _shadowRepo.RegisterComponent<TurretState>();
               // ... register other recorded components ...
           }
           
           public void Execute(ISimulationView liveView, float deltaTime)
           {
               HandleInput();
               
               if (_isPaused) return;
               
               // Accumulate time
               _accumulator += deltaTime * _playbackSpeed;
               
               // Consume frames
               while (_accumulator >= RECORDED_DELTA) {
                   bool hasMore = _controller.StepForward(_shadowRepo);
                   if (!hasMore) {
                       _isPaused = true;
                       Console.WriteLine("[Replay] End of recording");
                       break;
                   }
                   
                   SyncShadowToLive(liveView);
                   _accumulator -= RECORDED_DELTA;
               }
           }
           
           private void SyncShadowToLive(ISimulationView liveView)
           {
               var shadowQuery = _shadowRepo.Query()
                   .With<NetworkIdentity>()
                   .Build();
               
               var cmd = liveView.GetCommandBuffer();
               
               foreach (var shadowEntity in shadowQuery) {
                   // Direct ID mapping (Shadow ID == Live ID)
                   Entity liveEntity = shadowEntity;
                   
                   if (!liveView.IsAlive(liveEntity))
                       continue;
                   
                   // CRITICAL: Copy identity/authority on first encounter
                   // These are needed for system queries (TransformSync, SmartEgress)
                   if (!liveView.HasComponent<NetworkIdentity>(liveEntity)) {
                       var netId = _shadowRepo.GetComponentRO<NetworkIdentity>(shadowEntity);
                       cmd.AddComponent(liveEntity, netId);
                   }
                   
                   if (!liveView.HasComponent<NetworkAuthority>(liveEntity)) {
                       var auth = _shadowRepo.GetComponentRO<NetworkAuthority>(shadowEntity);
                       cmd.AddComponent(liveEntity, auth);
                   }
                   
                   if (_shadowRepo.HasComponent<DescriptorOwnership>(shadowEntity) &&
                       !liveView.HasComponent<DescriptorOwnership>(liveEntity)) {
                        var shadowOwnership = _shadowRepo.GetComponentRO<DescriptorOwnership>(shadowEntity);
                        
                        // Create NEW instance and copy dictionary contents
                        var liveOwnership = new DescriptorOwnership();
                        foreach(var kvp in shadowOwnership.Map) {
                            liveOwnership.Map[kvp.Key] = kvp.Value;
                        }
                        
                        cmd.AddComponent(liveEntity, liveOwnership);
                   }
                   
                   // Inject Chassis (DemoPosition) if we owned it
                   if (_shadowRepo.HasAuthority(shadowEntity, CHASSIS_KEY) &&
                       _shadowRepo.HasComponent<DemoPosition>(shadowEntity)) {
                       var pos = _shadowRepo.GetComponentRO<DemoPosition>(shadowEntity);
                       cmd.SetComponent(liveEntity, pos);
                   }
                   
                   // Inject Turret if we owned it
                   if (_shadowRepo.HasAuthority(shadowEntity, TURRET_KEY) &&
                       _shadowRepo.HasComponent<TurretState>(shadowEntity)) {
                       var turret = _shadowRepo.GetComponentRO<TurretState>(shadowEntity);
                       cmd.SetComponent(liveEntity, turret);
                   }
               }
           }
           
           private void HandleInput()
           {
               if (Console.KeyAvailable) {
                   var key = Console.ReadKey(true).Key;
                   
                   switch (key) {
                       case ConsoleKey.Spacebar:
                           _isPaused = !_isPaused;
                           Console.WriteLine($"[Replay] {(_isPaused ? "Paused" : "Resumed")}");
                           break;
                       
                       case ConsoleKey.UpArrow:
                           _playbackSpeed += 0.5f;
                           Console.WriteLine($"[Replay] Speed: {_playbackSpeed}x");
                           break;
                       
                       case ConsoleKey.DownArrow:
                           _playbackSpeed = Math.Max(0.5f, _playbackSpeed - 0.5f);
                           Console.WriteLine($"[Replay] Speed: {_playbackSpeed}x");
                           break;
                       
                       case ConsoleKey.RightArrow:
                           if (_isPaused) {
                               if (_controller.StepForward(_shadowRepo)) {
                                   SyncShadowToLive((ISimulationView)_shadowRepo);
                                   Console.WriteLine($"[Replay] Step -> Frame {_controller.CurrentFrame}");
                               }
                           }
                           break;
                   }
               }
           }
           
           public void Dispose()
           {
               _controller?.Dispose();
               _shadowRepo?.Dispose();
           }
       }
   }
   ```

**Success Conditions:**
- [ ] Shadow world isolated from live world
- [ ] Identity/authority components copied from Shadow to Live on first encounter
- [ ] Only owned components injected (authority checked)
- [ ] Variable playback speed works (0.5x - 4x)
- [ ] Pause/resume functionality
- [ ] Single-step when paused
- [ ] No memory leaks (proper disposal)
- [ ] Unit test: Identity/authority components exist in live world after first sync
- [ ] Unit test: Mock recording with owned/unowned mix, verify selective injection
- [ ] Integration test: Full replay session, verify data matches expectations
- [ ] Integration test: Speed changes don't corrupt replay

**Testing:**
```csharp
[Test]
public void ReplayBridge_InjectsOnlyOwnedComponents() {
    var recording = CreateMockRecording(
        ownedComponents: new[] { typeof(DemoPosition) },
        unownedComponents: new[] { typeof(TurretState) }
    );
    
    var bridge = new ReplayBridgeSystem(recording.Path);
    var liveRepo = CreateTestRepo();
    var entity = CreateEntity(liveRepo);
    
    // Advance one frame
    bridge.Execute(liveRepo.AsView(), 0.016f);
    
    // Verify only DemoPosition was injected
    Assert.That(liveRepo.HasComponent<DemoPosition>(entity), Is.True);
    Assert.That(liveRepo.HasComponent<TurretState>(entity), Is.False);
}
```

---

### FDP-DRP-013: Time Mode Input System

**Priority:** Medium  
**Depends On:** None  
**Design Reference:** DESIGN.md § 6.2, § 9.3

**Description:**
Implement user input handler for runtime time mode switching and speed control during live sessions.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo/Systems/TimeInputSystem.cs`
   ```csharp
   using System;
   using Fdp.Kernel;
   using ModuleHost.Core;
   using ModuleHost.Core.Abstractions;
   using FDP.Toolkit.Time.Controllers;
   
   namespace Fdp.Examples.NetworkDemo.Systems
   {
       public class TimeInputSystem : IModuleSystem
       {
           private readonly ModuleHostKernel _kernel;
           private readonly DistributedTimeCoordinator _coordinator;
           private float _targetScale = 1.0f;
           
           public TimeInputSystem(ModuleHostKernel kernel, 
               DistributedTimeCoordinator coordinator)
           {
               _kernel = kernel;
               _coordinator = coordinator;
           }
           
           public void Execute(ISimulationView view, float deltaTime)
           {
               if (!Console.KeyAvailable) return;
               
               var key = Console.ReadKey(true).Key;
               var controller = _kernel.GetTimeController();
               var mode = controller.GetMode();
               
               switch (key) {
                   case ConsoleKey.T:
                       ToggleTimeMode(mode);
                       break;
                   
                   case ConsoleKey.UpArrow:
                       _targetScale += 0.5f;
                       controller.SetTimeScale(_targetScale);
                       Console.WriteLine($"[Time] Scale: {_targetScale}x");
                       break;
                   
                   case ConsoleKey.DownArrow:
                       _targetScale = Math.Max(0.0f, _targetScale - 0.5f);
                       controller.SetTimeScale(_targetScale);
                       Console.WriteLine($"[Time] Scale: {_targetScale}x");
                       break;
                   
                   case ConsoleKey.RightArrow:
                       if (mode == TimeMode.Deterministic && _targetScale == 0.0f) {
                           _kernel.StepFrame(0.0166f);
                           Console.WriteLine("[Time] Manual step");
                       }
                       break;
               }
           }
           
           private void ToggleTimeMode(TimeMode current)
           {
               if (current == TimeMode.Continuous) {
                   Console.WriteLine("[Time] Switching to Deterministic...");
                   _coordinator.SwitchToDeterministic();
               } else {
                   Console.WriteLine("[Time] Switching to Continuous...");
                   _coordinator.SwitchToContinuous();
               }
           }
       }
   }
   ```

**Success Conditions:**
- [ ] 'T' key toggles time mode
- [ ] Arrow keys control time scale
- [ ] Right arrow steps frame when paused in deterministic mode
- [ ] Console feedback for all actions
- [ ] No conflicts with replay input handling
- [ ] Integration test: Mode switch triggers distributed barrier
- [ ] Integration test: Time scale changes affect simulation speed

---

### FDP-DRP-018: Advanced Demo Modules (Radar & Damage)

**Priority:** Medium  
**Depends On:** FDP-DRP-011  
**Design Reference:** DESIGN.md § 8

**Description:**
Implement `RadarModule` (async/SlowBackground) and `DamageControlModule` (reactive/event-driven) to demonstrate advanced execution policies and event-driven architecture.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo/Modules/RadarModule.cs`
   ```csharp
   using Fdp.Kernel;
   using ModuleHost.Core.Abstractions;
   using FDP.Toolkit.Replication.Components;
   
   [ExecutionPolicy(ExecutionMode.SlowBackground, priority: 1)]
   [SnapshotPolicy(SnapshotMode.OnDemand)]
   public class RadarModule : IModuleSystem
   {
       private readonly IEventBus _eventBus;
       private float _scanInterval = 1.0f; // 1Hz scan rate
       private float _accumulator = 0.0f;
       
       public void Execute(ISimulationView view, float dt)
       {
           _accumulator += dt;
           if (_accumulator < _scanInterval) return;
           
           _accumulator = 0;
           
           // Request snapshot for thread-safe access
           var snapshot = view.CaptureSnapshot();
           
           // Scan for entities within range
           var query = snapshot.Query()
               .With<NetworkIdentity>()
               .With<DemoPosition>()
               .Build();
           
           foreach (var entity in query) {
               var pos = snapshot.GetComponentRO<DemoPosition>(entity);
               // Simulate radar detection
               if (Vector3.Distance(pos.Value, Vector3.Zero) < 1000f) {
                   _eventBus.Publish(new RadarContactEvent {
                       EntityId = snapshot.GetComponentRO<NetworkIdentity>(entity).Value,
                       Position = pos.Value,
                       Timestamp = DateTime.UtcNow
                   });
               }
           }
       }
   }
   ```

2. **File:** `Fdp.Examples.NetworkDemo/Modules/DamageControlModule.cs`
   ```csharp
   using Fdp.Kernel;
   using ModuleHost.Core.Abstractions;
   
   [ExecutionPolicy(ExecutionMode.Synchronous)]
   [WatchEvents(typeof(DetonationEvent))]
   public class DamageControlModule : IModuleSystem
   {
       public void Execute(ISimulationView view, float dt)
       {
           // Only executes when DetonationEvent occurs
           var events = view.GetEvents<DetonationEvent>();
           
           foreach (var evt in events) {
               // Apply damage to nearby entities
               var query = view.Query()
                   .With<DemoPosition>()
                   .With<Health>()
                   .Build();
               
               var cmd = view.GetCommandBuffer();
               
               foreach (var entity in query) {
                   var pos = view.GetComponentRO<DemoPosition>(entity);
                   float distance = Vector3.Distance(pos.Value, evt.Position);
                   
                   if (distance < evt.Radius) {
                       var health = view.GetComponentRO<Health>(entity);
                       float damage = evt.Damage * (1 - distance / evt.Radius);
                       
                       cmd.SetComponent(entity, new Health {
                           Value = Math.Max(0, health.Value - damage)
                       });
                   }
               }
           }
       }
   }
   ```

3. **File:** `Fdp.Examples.NetworkDemo/Events/RadarContactEvent.cs`
   ```csharp
   public struct RadarContactEvent
   {
       public long EntityId;
       public Vector3 Position;
       public DateTime Timestamp;
   }
   ```

4. **File:** `Fdp.Examples.NetworkDemo/Events/DetonationEvent.cs`
   ```csharp
   public struct DetonationEvent
   {
       public Vector3 Position;
       public float Radius;
       public float Damage;
   }
   ```

5. **Update PlayerInputSystem** to trigger detonations:
   ```csharp
   if (Input.GetKeyDown(KeyCode.B)) {
       _eventBus.Publish(new DetonationEvent {
           Position = GetTankPosition(),
           Radius = 50f,
           Damage = 100f
       });
       Console.WriteLine("[Input] Detonation triggered");
   }
   ```

**Success Conditions:**
- [ ] `RadarModule` runs at 1Hz (not 60Hz)
- [ ] `RadarModule` uses snapshot for thread safety
- [ ] `DamageControlModule` only executes when `DetonationEvent` published
- [ ] No execution overhead when no events
- [ ] Unit test: Verify RadarModule scan interval
- [ ] Unit test: Verify DamageControlModule reactive behavior
- [ ] Integration test: Press 'B', verify damage applied
- [ ] Performance test: Idle simulation shows zero DamageControl executions

---

### FDP-DRP-019: Dynamic Ownership Transfer System

**Priority:** Medium  
**Depends On:** FDP-DRP-013  
**Design Reference:** DESIGN.md § 8.3

**Description:**
Implement runtime ownership transfer to demonstrate partial authority handoff between nodes.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo/Systems/OwnershipInputSystem.cs`
   ```csharp
   using Fdp.Kernel;
   using ModuleHost.Core.Abstractions;
   using FDP.Toolkit.Replication.Components;
   using FDP.Toolkit.Replication.Extensions;
   
   public class OwnershipInputSystem : IModuleSystem
   {
       private readonly int _localNodeId;
       private readonly IEventBus _eventBus;
       private const long TURRET_DESCRIPTOR = 10;
       
       public OwnershipInputSystem(int localNodeId, IEventBus eventBus)
       {
           _localNodeId = localNodeId;
           _eventBus = eventBus;
       }
       
       public void Execute(ISimulationView view, float dt)
       {
           if (!Console.KeyAvailable) return;
           
           var key = Console.ReadKey(true).Key;
           
           if (key == ConsoleKey.O) {
               // Find the tank entity
               var query = view.Query()
                   .With<NetworkIdentity>()
                   .With<TurretState>()
                   .Build();
               
               foreach (var tank in query) {
                   // Check if we already have authority
                   if (view.HasAuthority(tank, TURRET_DESCRIPTOR)) {
                       Console.WriteLine("[Ownership] Already own turret");
                       continue;
                   }
                   
                   // Request ownership transfer
                   var netId = view.GetComponentRO<NetworkIdentity>(tank);
                   
                   var request = new OwnershipUpdateRequest {
                       EntityId = netId.Value,
                       DescriptorOrdinal = TURRET_DESCRIPTOR,
                       InstanceId = 0,
                       NewOwner = _localNodeId,
                       Timestamp = DateTime.UtcNow
                   };
                   
                   _eventBus.Publish(request);
                   Console.WriteLine($"[Ownership] Requesting turret control for entity {netId.Value}");
                   
                   break; // Only one tank for now
               }
           }
       }
   }
   ```

2. **File:** `Fdp.Examples.NetworkDemo/Events/OwnershipUpdateRequest.cs`
   ```csharp
   public struct OwnershipUpdateRequest
   {
       public long EntityId;
       public long DescriptorOrdinal;
       public long InstanceId;
       public int NewOwner;
       public DateTime Timestamp;
   }
   ```

3. **Integration with Replication Toolkit:**
   - The `OwnershipEgressSystem` (existing in toolkit) should listen for these events
   - Broadcasts `OwnershipUpdate` message to all nodes
   - `OwnershipIngressSystem` updates `DescriptorOwnership` component
   - Both nodes' authority checks now reflect the new owner

**Success Conditions:**
- [ ] 'O' key triggers ownership request
- [ ] Request published to event bus
- [ ] `OwnershipEgressSystem` broadcasts to network
- [ ] Remote nodes receive and apply ownership change
- [ ] Authority checks reflect new owner
- [ ] Original owner stops publishing turret data
- [ ] New owner starts publishing turret data
- [ ] Unit test: Request generation correct
- [ ] Integration test: Full ownership transfer flow with 2 nodes
- [ ] Integration test: Verify egress/ingress behavior changes

---

### FDP-DRP-020: TKB Mandatory Requirements Configuration

**Priority:** High  
**Depends On:** FDP-DRP-014  
**Design Reference:** DESIGN.md § 3.3

**Description:**
Configure TKB templates with mandatory and soft descriptor requirements to demonstrate Ghost Protocol behavior.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo/Configuration/TankTemplate.cs`
   ```csharp
   using Fdp.Interfaces;
   using Fdp.Kernel;
   using FDP.Toolkit.Replication;
   
   public static class TankTemplate
   {
       public static void Register(ITkbDatabase tkb)
       {
           var tank = new TkbTemplate("CommandTank", 100);
           
           // Core components
           tank.AddComponent(new DemoPosition());
           tank.AddComponent(new TurretState());
           tank.AddComponent(new Health { Value = 100 });
           
           // HARD REQUIREMENT: Chassis (Position/Rotation)
           // Entity stays as Ghost until this arrives
           tank.MandatoryDescriptors.Add(new MandatoryDescriptor {
               PackedKey = PackedKey.Create(5, 0), // Chassis descriptor
               IsHard = true
           });
           
           // SOFT REQUIREMENT: Turret (Aim angles)
           // Entity spawns after timeout even if this hasn't arrived
           tank.MandatoryDescriptors.Add(new MandatoryDescriptor {
               PackedKey = PackedKey.Create(10, 0), // Turret descriptor
               IsHard = false,
               SoftTimeoutFrames = 60 // 1 second at 60Hz
           });
           
           tkb.Register(tank);
       }
   }
   ```

2. **Update Program.cs** to call template registration:
   ```csharp
   var tkb = new TkbDatabase();
   TankTemplate.Register(tkb);
   ```

**Success Conditions:**
- [ ] Tank template registered with correct requirements
- [ ] Hard requirement (Chassis) prevents Ghost promotion until satisfied
- [ ] Soft requirement (Turret) allows promotion after timeout
- [ ] Unit test: Verify template configuration
- [ ] Integration test: Ghost receives only Turret → stays Ghost
- [ ] Integration test: Ghost receives Chassis → promotes immediately
- [ ] Integration test: Ghost receives Chassis, waits for Turret timeout → promotes with default turret

**Testing:**
```csharp
[Test]
public void TankGhost_OnlySoftDescriptor_StaysGhost() {
    var ghost = CreateGhostEntity();
    var store = repo.GetManagedComponent<BinaryGhostStore>(ghost);
    
    // Receive only soft requirement (Turret)
    store.StashedData[PackedKey.Create(10, 0)] = SerializeTurret(...);
    
    // Run promotion system
    ghostPromotion.Execute(repo.AsView(), 0.016f);
    
    // Should still be Ghost (hard requirement missing)
    Assert.That(repo.HasManagedComponent<BinaryGhostStore>(ghost), Is.True);
}
```

---

## Phase 5: Integration & Configuration

### FDP-DRP-014: Program.cs Live Mode Setup

**Priority:** Critical  
**Depends On:** FDP-DRP-003, FDP-DRP-008, FDP-DRP-011  
**Design Reference:** DESIGN.md § 10.1, § 11

**Description:**
Configure the demo application for live recording mode with proper ID reservation and metadata generation.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo/Program.cs` (Live Mode Section)
   ```csharp
   static async Task Main(string[] args)
   {
       int nodeId = int.Parse(args[0]);
       bool isReplay = args.Length > 1 && args[1] == "replay";
       
       // === 1. KERNEL INIT ===
       var world = new EntityRepository();
       world.RegisterComponent<DemoPosition>();
       world.RegisterComponent<TurretState>();
       world.RegisterComponent<NetworkPosition>();
       world.RegisterComponent<NetworkIdentity>();
       world.RegisterComponent<NetworkAuthority>();
       
       var kernel = new ModuleHostKernel(world);
       
       // === 2. SYSTEM ENTITIES ===
       // Create system singletons (IDs 0-100)
       CreateSystemEntities(world);
       
       // === 3. SAFETY GAP ===
       // Reserve up to 65536 to separate system from simulation
       world.ReserveIdRange(FdpConfig.SYSTEM_ID_RANGE);
       Console.WriteLine($"[Init] Reserved ID range 0-{FdpConfig.SYSTEM_ID_RANGE}");
       
       // === 4. MODULES ===
       var topology = new StaticNetworkTopology(nodeId, new[] { 1, 2 });
       var geoTransform = new WGS84Transform();
       geoTransform.SetOrigin(52.5200, 13.4050, 0); // Berlin
       
       var replication = new ReplicationToolkit(tkbDatabase, topology);
       var cyclone = new CycloneNetworkModule(replication);
       kernel.RegisterModule(cyclone);
       
       // === 5. TRANSLATORS ===
       var entityMap = new NetworkEntityMap();
       
       // Manual translator for geodetic conversion
       replication.RegisterTranslator(
           new GeodeticTranslator(geoTransform, entityMap)
       );
       
       // Auto translators from assembly scan
       var autoTranslators = ReplicationBootstrap.CreateAutoTranslators(
           typeof(Program).Assembly,
           entityMap
       );
       foreach (var t in autoTranslators) {
           replication.RegisterTranslator(t);
       }
       
       // === 6. MODE-SPECIFIC SETUP ===
       if (!isReplay) {
           // LIVE MODE
           kernel.RegisterGlobalSystem(new PhysicsSystem());
           kernel.RegisterGlobalSystem(new PlayerInputSystem(nodeId));
           kernel.RegisterGlobalSystem(new TransformSyncSystem());
           kernel.RegisterGlobalSystem(new TimeInputSystem(kernel, coordinator));
           
           var recorder = new AsyncRecorder($"node_{nodeId}.fdp");
           var recorderSys = new RecorderTickSystem(recorder, world);
           recorderSys.SetMinRecordableId(FdpConfig.SYSTEM_ID_RANGE);
           kernel.RegisterGlobalSystem(recorderSys);
           
           Console.WriteLine("[Mode] LIVE - Recording enabled");
       }
       
       // === 7. RUN LOOP ===
       kernel.Initialize();
       cyclone.Connect();
       
       bool running = true;
       while (running) {
           kernel.Update();
           // ... render, input ...
       }
       
       // === 8. CLEANUP ===
       if (!isReplay) {
           recorder.Dispose();
           
           var meta = new RecordingMetadata {
               MaxEntityId = world.MaxEntityIndex,
               Timestamp = DateTime.UtcNow,
               NodeId = nodeId
           };
           MetadataManager.Save($"node_{nodeId}.fdp.meta", meta);
           
           Console.WriteLine($"[Recorder] Saved metadata (MaxID: {meta.MaxEntityId})");
       }
   }
   ```

**Success Conditions:**
- [ ] ID reservation happens before any game entity creation
- [ ] Recorder configured with correct `MinRecordableId`
- [ ] Metadata saved on clean exit
- [ ] System entities not recorded
- [ ] All systems registered in correct phase
- [ ] Integration test: Run live session, verify metadata file created
- [ ] Integration test: Verify recorded file contains only IDs >= 65536

---

### FDP-DRP-015: Program.cs Replay Mode Setup

**Priority:** Critical  
**Depends On:** FDP-DRP-012, FDP-DRP-014  
**Design Reference:** DESIGN.md § 10.2, § 11

**Description:**
Configure the demo application for replay mode with metadata loading and replay bridge setup.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo/Program.cs` (Replay Mode Section)
   ```csharp
   // ... (continuing from FDP-DRP-014) ...
   
   if (isReplay) {
       // REPLAY MODE
       string recPath = $"node_{nodeId}.fdp";
       string metaPath = $"{recPath}.meta";
       
       // 1. Load Metadata
       var meta = MetadataManager.Load(metaPath);
       Console.WriteLine($"[Replay] Loaded metadata (MaxID: {meta.MaxEntityId})");
       
       // 2. Reserve ID Range
       // This prevents new ghosts from colliding with recorded entities
       world.ReserveIdRange(meta.MaxEntityId);
       
       // 3. Setup Replay Bridge
       // This system injects recorded data into live world
       var replayBridge = new ReplayBridgeSystem(recPath);
       kernel.RegisterGlobalSystem(replayBridge);
       
       // 4. Keep Network Active
       // Even though we're replaying, we still need network for receiving
       // remote node's replay stream
       kernel.RegisterGlobalSystem(new TransformSyncSystem());
       
       // 5. Freeze Physics (time scale = 0, but Tick still advances)
       // Replay data drives position, physics is disabled
       var dummyTime = new GlobalTime { TimeScale = 0 };
       kernel.SwapTimeController(new SteppingTimeController(dummyTime));
       
       Console.WriteLine("[Mode] REPLAY - Playback active");
   }
   
   // ... RUN LOOP ...
   kernel.Initialize();
   cyclone.Connect();
   
   bool running = true;
   while (running) {
       if (isReplay) {
           // CRITICAL: Advance global version for change detection
           // Without this, SmartEgressSystem won't detect injected changes
           world.Tick();
       }
       
       kernel.Update();
       // ... render, input ...
   }
   ```

**Success Conditions:**
- [ ] Metadata loaded before any entity operations
- [ ] ID range reserved correctly
- [ ] Physics disabled (time scale = 0)
- [ ] Network systems still active for receiving
- [ ] Replay bridge registered and functional
- [ ] Integration test: Full replay session with 2 nodes
- [ ] Integration test: Verify distributed reconstruction accuracy

---

## Phase 6: Testing & Validation

### FDP-DRP-016: End-to-End Integration Test

**Priority:** Critical  
**Depends On:** All previous tasks  
**Design Reference:** DESIGN.md § 13

**Description:**
Create comprehensive integration test that validates the complete distributed recording and playback workflow.

**Implementation:**

1. **File:** `Fdp.Examples.NetworkDemo.Tests/Integration/DistributedReplayTests.cs`
   ```csharp
   using System.Threading.Tasks;
   using NUnit.Framework;
   
   namespace Fdp.Examples.NetworkDemo.Tests.Integration
   {
       [TestFixture]
       public class DistributedReplayTests
       {
           [Test]
           public async Task FullScenario_TwoNodes_RecordAndReplay()
           {
               // PHASE 1: LIVE SESSION
               var nodeA = StartNode(1, isReplay: false);
               var nodeB = StartNode(2, isReplay: false);
               
               // Simulate 10 seconds of interaction
               for (int i = 0; i < 600; i++) {
                   if (i < 300) {
                       // Node A drives
                       nodeA.Input.SetAxis("Forward", 1.0f);
                   }
                   if (i >= 200) {
                       // Node B aims turret
                       nodeB.Input.SetAxis("TurretYaw", 45.0f);
                   }
                   
                   nodeA.Update(0.016f);
                   nodeB.Update(0.016f);
                   await Task.Delay(16);
               }
               
               // Stop and save
               var metaA = nodeA.Stop();
               var metaB = nodeB.Stop();
               
               Assert.That(metaA.MaxEntityId, Is.GreaterThan(65536));
               Assert.That(File.Exists("node_1.fdp"), Is.True);
               Assert.That(File.Exists("node_2.fdp"), Is.True);
               
               // PHASE 2: REPLAY SESSION
               var replayA = StartNode(1, isReplay: true);
               var replayB = StartNode(2, isReplay: true);
               
               // Run replay for same duration
               for (int i = 0; i < 600; i++) {
                   replayA.Update(0.016f);
                   replayB.Update(0.016f);
                   await Task.Delay(16);
               }
               
               // VALIDATION
               // Node A should see tank at expected position
               // (from its own replay) and turret at expected angle
               // (from Node B's replay via network)
               var tankA = replayA.GetEntity("Tank");
               var posA = replayA.GetComponent<DemoPosition>(tankA);
               var turretA = replayA.GetComponent<TurretState>(tankA);
               
               Assert.That(posA.Value.X, Is.GreaterThan(0)); // Moved forward
               Assert.That(turretA.YawAngle, Is.EqualTo(45).Within(1)); // From Node B
               
               replayA.Stop();
               replayB.Stop();
           }
           
           [Test]
           public void TimeMode_SwitchDuringLive_PreservesSync()
           {
               var nodeA = StartNode(1, isReplay: false);
               var nodeB = StartNode(2, isReplay: false);
               
               // Run 100 frames in continuous
               for (int i = 0; i < 100; i++) {
                   nodeA.Update(0.016f);
                   nodeB.Update(0.016f);
               }
               
               // Switch to deterministic
               nodeA.TimeCoordinator.SwitchToDeterministic();
               
               // Wait for barrier
               WaitForBarrier(nodeA, nodeB);
               
               // Verify both in deterministic mode
               Assert.That(nodeA.Kernel.GetTimeController().GetMode(), 
                   Is.EqualTo(TimeMode.Deterministic));
               Assert.That(nodeB.Kernel.GetTimeController().GetMode(), 
                   Is.EqualTo(TimeMode.Deterministic));
               
               // Step manually
               nodeA.Kernel.StepFrame(0.016f);
               nodeB.Kernel.StepFrame(0.016f);
               
               // Verify frame counters match
               Assert.That(nodeA.FrameCount, Is.EqualTo(nodeB.FrameCount));
           }
           
           [Test]
           public void Replay_VariableSpeed_MaintainsCoherence()
           {
               // Create recording
               var recorder = CreateMockRecording(600); // 10 seconds @ 60Hz
               
               var replay = StartReplayNode(recorder.Path);
               
               // Play at 2x speed
               replay.Bridge.SetPlaybackSpeed(2.0f);
               
               // Should complete in 5 seconds
               var start = DateTime.UtcNow;
               while (!replay.Bridge.IsComplete) {
                   replay.Update(0.016f);
               }
               var elapsed = (DateTime.UtcNow - start).TotalSeconds;
               
               Assert.That(elapsed, Is.LessThan(6.0)); // Some tolerance
               Assert.That(replay.Bridge.FramesProcessed, Is.EqualTo(600));
           }
       }
   }
   ```

**Success Conditions:**
- [ ] Live session records successfully
- [ ] Metadata files created
- [ ] Replay loads recordings
- [ ] Distributed data reconstructed correctly
- [ ] Time mode switching works
- [ ] Variable playback speed works
- [ ] All assertions pass
- [ ] No memory leaks or crashes
- [ ] Performance acceptable (< 5% overhead)

---

### FDP-DRP-017: Performance Validation

**Priority:** Medium  
**Depends On:** FDP-DRP-016  
**Design Reference:** DESIGN.md § 13

**Description:**
Validate that the distributed replay system maintains acceptable performance characteristics.

**Benchmarks:**

1. **Recording Overhead:**
   - Target: < 2% CPU overhead during live session
   - Measure: CPU usage with/without recorder active

2. **Replay Throughput:**
   - Target: 1x replay speed uses < 10% CPU
   - Measure: Frame processing time during replay

3. **Memory Usage:**
   - Target: Shadow World < 50MB for 10-minute recording
   - Measure: Memory delta when loading recording

4. **Network Bandwidth:**
   - Target: Geographic translation adds < 10% overhead vs raw binary
   - Measure: Packet sizes for GeoStateDescriptor vs raw Vector3

**Testing:**
```csharp
[Test]
[Category("Performance")]
public void Recording_Overhead_AcceptableBenchmark() {
    var repo = CreatePopulatedRepo(entityCount: 1000);
    
    // Baseline: No recording
    var baseline = MeasureCpuUsage(() => {
        for (int i = 0; i < 600; i++) {
            kernel.Update(0.016f);
        }
    });
    
    // With recording
    var recorder = new AsyncRecorder("perf_test.fdp");
    var recorderSys = new RecorderTickSystem(recorder, repo);
    kernel.RegisterGlobalSystem(recorderSys);
    
    var withRecording = MeasureCpuUsage(() => {
        for (int i = 0; i < 600; i++) {
            kernel.Update(0.016f);
        }
    });
    
    recorder.Dispose();
    
    var overhead = (withRecording - baseline) / baseline;
    Assert.That(overhead, Is.LessThan(0.02)); // < 2%
}
```

---

## Appendix: Quick Reference

### Task Dependencies

```
Phase 1 (Kernel):
  001 → 002, 003

Phase 2 (Replication):
  004 (parallel)
  005 → 006 → 007

Phase 3 (Demo Infrastructure):
  008 (parallel)
  005 → 009 → 010

Phase 4 (Systems):
  009 → 011
  001, 002, 008 → 012
  013 (parallel)

Phase 5 (Integration):
  003, 008, 011 → 014
  012, 014 → 015

Phase 6 (Testing):
  ALL → 016 → 017
```

### Critical Path

`001 → 002 → 005 → 006 → 009 → 011 → 012 → 014 → 015 → 016`

Total estimated duration: 18-22 developer-days
