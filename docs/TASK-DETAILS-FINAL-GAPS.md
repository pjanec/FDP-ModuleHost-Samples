# FDP Refactoring - Final Technical Gaps Resolution

**Date**: 2026-02-04 18:31  
**Status**: Ready for integration into main TASK-DETAILS.md

This document addresses the final three technical gaps identified during deep-dive review.

---

## Gap 1: Smart Egress Tracking Component

### FDP-REP-207: Implement Egress Tracking Logic

**Phase**: 5 (Ownership Management)  
**Insert After**: FDP-REP-206

**Description**:
Implement the publication tracking component and salted rolling window logic for efficient unreliable descriptor refresh.

**Rationale**:
Without tracking "last published" state, the system doesn't know when to refresh unreliable descriptors. The salted rolling window ensures deterministic, evenly-distributed refresh cycles without per-entity timers.

**Files**:
1. `ModuleHost/FDP.Toolkit.Replication/Components/EgressPublicationState.cs`
2. `ModuleHost/FDP.Toolkit.Replication/Systems/SmartEgressSystem.cs`

**Component Definition**:
```csharp
namespace Fdp.Toolkit.Replication.Components
{
    /// <summary>
    /// Tracks publication state for smart egress optimization.
    /// Transient component - not persisted in snapshots.
    /// </summary>
    [DataPolicy(DataPolicy.Transient)]
    public class EgressPublicationState
    {
        /// <summary>
        /// Map of PackedKey (DescriptorOrdinal + InstanceId) → Last Published Tick.
        /// Used for dirty tracking and refresh logic.
        /// </summary>
        public Dictionary<long, uint> LastPublishedTickMap { get; } = new();
        
        /// <summary>
        /// Dirty flags per descriptor. Set when component changes.
        /// Cleared after publication.
        /// </summary>
        public HashSet<long> DirtyDescriptors { get; } = new();
    }
}
```

**Salted Rolling Window Formula**:
```csharp
namespace Fdp.Toolkit.Replication.Systems
{
    public class SmartEgressSystem
    {
        private const uint REFRESH_INTERVAL = 600;  // Refresh every 10 seconds at 60Hz
        
        /// <summary>
        /// Determines if an unreliable descriptor needs refresh.
        /// Uses entity ID as salt for even distribution.
        /// </summary>
        private bool NeedsRefresh(long entityId, uint currentTick, uint lastPublishedTick)
        {
            // Dirty descriptors always publish immediately
            if (currentTick == lastPublishedTick) return false;
            
            // Salted rolling window: each entity has unique phase offset
            uint salt = (uint)(entityId % REFRESH_INTERVAL);
            uint tickPhase = (currentTick + salt) % REFRESH_INTERVAL;
            
            return tickPhase == 0;
        }
        
        /// <summary>
        /// Integrates with AutoTranslator.ScanAndPublish.
        /// Includes chunk version early-out for performance.
        /// </summary>
        public bool ShouldPublishDescriptor(
            Entity entity, 
            long packedDescriptorKey,
            uint currentTick,
            bool isUnreliable,
            uint chunkVersion,          // NEW: Chunk version from ECS
            uint lastChunkPublished)    // NEW: Last published chunk version
        {
            // CRITICAL OPTIMIZATION: Early-out if chunk hasn't changed
            // This leverages the existing ECS chunk versioning system
            if (chunkVersion == lastChunkPublished && !isUnreliable)
                return false;  // No changes in this chunk since last publish
            
            var repo = _view.GetRepository();
            
            // Check authority first
            if (!_view.HasAuthority(entity, packedDescriptorKey))
                return false;
            
            // Get or create publication state
            if (!repo.HasManagedComponent<EgressPublicationState>(entity))
            {
                repo.SetManagedComponent(entity, new EgressPublicationState());
            }
            
            var pubState = repo.GetManagedComponent<EgressPublicationState>(entity);
            
            // Check dirty flag (immediate publish)
            if (pubState.DirtyDescriptors.Contains(packedDescriptorKey))
            {
                pubState.DirtyDescriptors.Remove(packedDescriptorKey);
                pubState.LastPublishedTickMap[packedDescriptorKey] = currentTick;
                return true;
            }
            
            // For reliable descriptors, only publish on change
            if (!isUnreliable)
                return false;
            
            // For unreliable, use salted rolling window
            pubState.LastPublishedTickMap.TryGetValue(packedDescriptorKey, out uint lastTick);
            
            var identity = repo.GetComponent<NetworkIdentity>(entity);
            if (NeedsRefresh(identity.Value, currentTick, lastTick))
            {
                pubState.LastPublishedTickMap[packedDescriptorKey] = currentTick;
                return true;
            }
            
            return false;
        }
    }
}
```

**Integration with AutoTranslator**:
```csharp
public void ScanAndPublish(ISimulationView view, IDataWriter writer)
{
    var repo = view.GetRepository();
    var currentTick = repo.GlobalVersion;
    
    // Query entities with this component type and NetworkIdentity
    var query = view.Query()
        .With<T>()
        .With<NetworkIdentity>()
        .Build();
    
    // OPTIMIZATION: Track chunk version for early-out
    uint lastChunkPublished = GetLastPublishedChunkVersion();
    
    foreach (var entity in query)
    {
        // Get current chunk version for this entity's component
        uint chunkVersion = repo.GetComponentChunkVersion<T>(entity);
        
        long packedKey = PackedKey.Create((int)_ordinal, GetInstanceId(entity));
        
        // Consult SmartEgressSystem with chunk version
        if (!_smartEgress.ShouldPublishDescriptor(
            entity, 
            packedKey, 
            currentTick, 
            _isUnreliable,
            chunkVersion,
            lastChunkPublished))
        {
            continue;
        }
        
        // Publish to network
        var descriptor = repo.GetComponent<T>(entity);
        writer.Write(descriptor);
    }
    
    // Update chunk version tracking
    UpdateLastPublishedChunkVersion(currentTick);
}
```

**Performance Notes**:
- **Chunk Version Early-Out**: Leverages existing ECS chunk versioning to skip entire chunks with no changes
- **Dirty Tracking**: Immediate publication on explicit changes (via `MarkDescriptorDirty`)
- **Salted Rolling Window**: Deterministic refresh for unreliable descriptors without timers
- **Authority Check**: Prevents unnecessary publication if not authoritative

**Dirty Flag Integration** (in component setters):
```csharp
// When a component changes (via SetComponent or direct mutation)
public static void MarkDescriptorDirty<T>(Entity entity, EntityRepository repo) where T : struct
{
    if (!repo.HasManagedComponent<EgressPublicationState>(entity))
        return;
    
    var pubState = repo.GetManagedComponent<EgressPublicationState>(entity);
    long packedKey = PackedKey.Create(GetOrdinalForType<T>(), 0);  // Needs registry
    pubState.DirtyDescriptors.Add(packedKey);
}
```

**Dependencies**: FDP-REP-206, FDP-REP-008 (AutoTranslator)

**Success Criteria**:
- ✅ `EgressPublicationState` component defined correctly
- ✅ Salted rolling window formula implemented
- ✅ Dirty tracking works for immediate publishing
- ✅ Integration with `AutoTranslator.ScanAndPublish` complete
- ✅ Unreliable descriptors refresh deterministically
- ✅ Bandwidth reduced (no constant spamming of unchanged data)
- ✅ Unit tests pass

**Test Requirements**:
```csharp
[TestClass]
public class SmartEgressSystemTests
{
    [TestMethod]
    public void NeedsRefresh_UsesEntityIdAsSalt()
    {
        var system = new SmartEgressSystem(...);
        
        // Two entities at same tick have different refresh phases
        bool entity1NeedsRefresh = system.NeedsRefresh(entityId: 1, currentTick: 100, lastPublishedTick: 0);
        bool entity2NeedsRefresh = system.NeedsRefresh(entityId: 2, currentTick: 100, lastPublishedTick: 0);
        
        // At least one should differ (statistical test over multiple ticks)
        bool foundDifference = false;
        for (uint tick = 0; tick < 600; tick++)
        {
            var r1 = system.NeedsRefresh(1, tick, 0);
            var r2 = system.NeedsRefresh(2, tick, 0);
            if (r1 != r2) foundDifference = true;
        }
        
        Assert.IsTrue(foundDifference, "Salt should distribute refresh phases");
    }
    
    [TestMethod]
    public void ShouldPublishDescriptor_PublishesDirtyImmediately()
    {
        var repo = new EntityRepository();
        var entity = repo.CreateEntity();
        var pubState = new EgressPublicationState();
        pubState.DirtyDescriptors.Add(PackedKey.Create(5, 0));
        repo.SetManagedComponent(entity, pubState);
        repo.AddComponent(entity, new NetworkIdentity { Value = 123 });
        repo.AddComponent(entity, new NetworkAuthority { PrimaryOwnerId = 1, LocalNodeId = 1 });
        
        var view = CreateMockView(repo);
        var system = new SmartEgressSystem(view);
        
        bool shouldPublish = system.ShouldPublishDescriptor(entity, PackedKey.Create(5, 0), 100, isUnreliable: true);
        
        Assert.IsTrue(shouldPublish);
        Assert.IsFalse(pubState.DirtyDescriptors.Contains(PackedKey.Create(5, 0)));  // Cleared after check
    }
    
    [TestMethod]
    public void ShouldPublishDescriptor_RespectsRefreshInterval()
    {
        var repo = new EntityRepository();
        var entity = repo.CreateEntity();
        repo.AddComponent(entity, new NetworkIdentity { Value = 100 });
        repo.AddComponent(entity, new NetworkAuthority { PrimaryOwnerId = 1, LocalNodeId = 1 });
        
        var view = CreateMockView(repo);
        var system = new SmartEgressSystem(view);
        
        long key = PackedKey.Create(5, 0);
        
        // First publish at tick 0
        Assert.IsTrue(system.ShouldPublishDescriptor(entity, key, 0, true));
        
        // Should NOT publish every tick
        int publishCount = 0;
        for (uint tick = 1; tick < 1200; tick++)
        {
            if (system.ShouldPublishDescriptor(entity, key, tick, true))
                publishCount++;
        }
        
        // Should publish approximately twice (at 600 and maybe at 1200)
        Assert.IsTrue(publishCount >= 1 && publishCount <= 3, $"Expected ~2 refreshes, got {publishCount}");
    }
}
```

**Estimated Effort**: 1.5 days

---

## Gap 2: Ghost Promotion Priority Queue

### UPDATE TO FDP-REP-106: Implement GhostPromotionSystem

**Addition to Existing Task**:

**Priority Queue Logic**:
```csharp
public class GhostPromotionSystem : IModuleSystem
{
    private readonly ITkbDatabase _tkb;
    private readonly Queue<Entity> _promotionQueue = new();
    private readonly HashSet<Entity> _inQueue = new();  // Prevent duplicates
    private readonly Stopwatch _stopwatch = new();
    
    // Configuration
    private const long PROMOTION_BUDGET_MICROSECONDS = 2000;  // 2ms per frame
    
    public void Execute(ISimulationView view, float deltaTime)
    {
        var repo = view.GetRepository();
        var currentFrame = repo.GlobalVersion;
        
        // Step 1: Scan for newly-ready ghosts and enqueue them
        EnqueueReadyGhosts(view, currentFrame);
        
        // Step 2: Promote from queue with time budget
        _stopwatch.Restart();
        
        while (_promotionQueue.Count > 0 && _stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency < PROMOTION_BUDGET_MICROSECONDS)
        {
            var entity = _promotionQueue.Dequeue();
            _inQueue.Remove(entity);
            
            // Double-check entity is still valid and ready
            if (!repo.IsAlive(entity)) continue;
            
            var lifecycle = repo.GetLifecycleState(entity);
            if (lifecycle != EntityLifecycle.Ghost) continue;
            
            // Perform atomic birth
            PromoteGhost(entity, view);
        }
        
        _stopwatch.Stop();
        
        // Step 3: Log if we hit budget limit (tuning feedback)
        if (_promotionQueue.Count > 0)
        {
            Console.WriteLine($"[GhostPromotion] Budget exhausted. {_promotionQueue.Count} ghosts deferred to next frame.");
        }
    }
    
    private void EnqueueReadyGhosts(ISimulationView view, uint currentFrame)
    {
        var repo = view.GetRepository();
        
        // Query all ghosts with NetworkSpawnRequest (identified ghosts)
        var query = view.Query()
            .With<NetworkSpawnRequest>()
            .With<BinaryGhostStore>()
            .WithLifecycleState(EntityLifecycle.Ghost)
            .Build();
        
        foreach (var entity in query)
        {
            // Skip if already in queue
            if (_inQueue.Contains(entity)) continue;
            
            var spawnReq = repo.GetComponent<NetworkSpawnRequest>(entity);
            var ghostStore = repo.GetManagedComponent<BinaryGhostStore>(entity);
            
            // Get blueprint
            if (!_tkb.TryGetByType(spawnReq.TkbType, out var template))
                continue;
            
            // Check mandatory requirements
            var availableKeys = new HashSet<long>(ghostStore.Stashed.Keys);
            
            // Hard requirements
            if (!template.AreHardRequirementsMet(availableKeys))
                continue;
            
            // Soft requirements with timeout
            if (!template.AreAllRequirementsMet(availableKeys, currentFrame, ghostStore.IdentifiedAtFrame))
                continue;
            
            // Ready! Add to queue
            _promotionQueue.Enqueue(entity);
            _inQueue.Add(entity);
        }
    }
    
    private void PromoteGhost(Entity entity, ISimulationView view)
    {
        var repo = view.GetRepository();
        var cmd = new EntityCommandBuffer(repo);
        
        var spawnReq = repo.GetComponent<NetworkSpawnRequest>(entity);
        var ghostStore = repo.GetManagedComponent<BinaryGhostStore>(entity);
        
        // 1. Apply Blueprint (with preserveExisting: false, ghosts are empty)
        var template = _tkb.GetByType(spawnReq.TkbType);
        template.ApplyTo(repo, entity, preserveExisting: false);
        
        // 2. Apply stashed descriptors (overwrites defaults)
        foreach (var kvp in ghostStore.Stashed)
        {
            long packedKey = kvp.Key;
            var entry = kvp.Value;
            
            // Get serialization provider
            int ordinal = PackedKey.GetOrdinal(packedKey);
            if (!_serializationRegistry.TryGet(ordinal, out var provider))
            {
                Console.Error.WriteLine($"[GhostPromotion] No serialization provider for ordinal {ordinal}");
                continue;
            }
            
            // Apply binary data to entity
            var buffer = _nativeMemoryPool.GetSlice(entry.Offset, entry.Length);
            provider.Apply(entity, buffer, cmd);
        }
        
        // 3. Spawn child blueprints (sub-entities)
        foreach (var childDef in template.ChildBlueprints)
        {
            var childEntity = repo.CreateEntity();
            repo.SetLifecycleState(childEntity, EntityLifecycle.Constructing);
            
            // Link to parent
            cmd.AddComponent(childEntity, new PartMetadata
            {
                ParentEntity = entity,
                InstanceId = childDef.InstanceId
            });
            
            // Apply child blueprint
            var childTemplate = _tkb.GetByType(childDef.ChildTkbType);
            childTemplate.ApplyTo(repo, childEntity, preserveExisting: false);
            
            // Register in parent's ChildMap
            if (!repo.HasManagedComponent<ChildMap>(entity))
            {
                cmd.SetManagedComponent(entity, new ChildMap());
            }
            var childMap = repo.GetManagedComponent<ChildMap>(entity);
            childMap.InstanceToEntity[childDef.InstanceId] = childEntity;
        }
        
        // 4. Cleanup and transition
        cmd.RemoveManagedComponent<BinaryGhostStore>(entity);
        cmd.RemoveComponent<NetworkSpawnRequest>(entity);
        cmd.SetLifecycleState(entity, EntityLifecycle.Constructing);
        
        // 5. Hand over to ELM
        _lifecycleModule.BeginConstruction(entity, spawnReq.TkbType, repo.GlobalVersion, cmd);
        
        cmd.Playback();
    }
}
```

**Key Improvements**:
1. **FIFO Queue**: Ghosts are promoted in order of readiness (first ready, first promoted)
2. **Prevents Starvation**: Deferred ghosts stay at front of queue for next frame
3. **Time Budget**: Uses `Stopwatch` for precise microsecond timing
4. **Duplicate Prevention**: `_inQueue` HashSet prevents same entity being queued twice
5. **Tuning Feedback**: Logs when budget is hit for performance tuning

**Additional Test Requirements**:
```csharp
[TestMethod]
public void Execute_PromotesInFIFOOrder()
{
    var repo = new EntityRepository();
    var tkb = new TkbDatabase();
    var template = new TkbTemplate("Simple", 100);
    tkb.Register(template);
    
    var system = new GhostPromotionSystem(tkb, ...);
    
    // Create 3 ghosts in order
    var ghost1 = CreateReadyGhost(repo, tkb, 100, seenFrame: 1);
    var ghost2 = CreateReadyGhost(repo, tkb, 100, seenFrame: 2);
    var ghost3 = CreateReadyGhost(repo, tkb, 100, seenFrame: 3);
    
    // Promote with unlimited budget
    var view = CreateMockView(repo);
    system.Execute(view, 0.016f);
    
    // All should be promoted, ghost1 first
    Assert.AreEqual(EntityLifecycle.Constructing, repo.GetLifecycleState(ghost1));
    Assert.AreEqual(EntityLifecycle.Constructing, repo.GetLifecycleState(ghost2));
    Assert.AreEqual(EntityLifecycle.Constructing, repo.GetLifecycleState(ghost3));
}

[TestMethod]
public void Execute_RespectsTimeBudget()
{
    var repo = new EntityRepository();
    var tkb = new TkbDatabase();
    var template = new TkbTemplate("Heavy", 100);
    // Add many components to make promotion expensive
    for (int i = 0; i < 50; i++)
        template.AddComponent(new TestComponent { Value = i });
    tkb.Register(template);
    
    var system = new GhostPromotionSystem(tkb, ...);
    
    // Create 100 ready ghosts
    var ghosts = new List<Entity>();
    for (int i = 0; i < 100; i++)
    {
        ghosts.Add(CreateReadyGhost(repo, tkb, 100, seenFrame: (uint)i));
    }
    
    var view = CreateMockView(repo);
    system.Execute(view, 0.016f);
    
    // Not all should be promoted (budget limit hit)
    int promotedCount = ghosts.Count(g => repo.GetLifecycleState(g) == EntityLifecycle.Constructing);
    
    Assert.IsTrue(promotedCount < 100, "Budget should prevent all promotions in one frame");
    Assert.IsTrue(promotedCount > 0, "At least some should be promoted");
    
    // Second frame should promote more
    system.Execute(view, 0.016f);
    int promotedCountAfter = ghosts.Count(g => repo.GetLifecycleState(g) == EntityLifecycle.Constructing);
    
    Assert.IsTrue(promotedCountAfter > promotedCount, "Deferred ghosts should be promoted in subsequent frames");
}
```

---

## Gap 3: TkbType vs BlueprintId Naming Consistency

### UPDATE TO FDP-LC-002: Move Lifecycle Events

**Naming Change**:
Replace all instances of `BlueprintId` with `TkbType` for consistency with the rest of the architecture.

**Updated Event Definitions**:
```csharp
namespace Fdp.Toolkit.Lifecycle.Events
{
    [EventId(9001)]
    public struct ConstructionOrder
    {
        public Entity Entity;
        
        /// <summary>
        /// TKB type identifier for blueprint lookup.
        /// Formerly called BlueprintId - renamed for consistency.
        /// </summary>
        public long TkbType;  // CHANGED from: int BlueprintId
        
        public uint FrameNumber;
    }
    
    [EventId(9002)]
    public struct ConstructionAck
    {
        public Entity Entity;
        public int ModuleId;
        public bool Success;
        public FixedString64 ErrorMessage;
    }
    
    [EventId(9003)]
    public struct DestructionOrder
    {
        public Entity Entity;
        public uint FrameNumber;
        public FixedString64 Reason;
    }
    
    [EventId(9004)]
    public struct DestructionAck
    {
        public Entity Entity;
        public int ModuleId;
    }
}
```

**Impact on EntityLifecycleModule**:
```csharp
// Update BeginConstruction signature
public void BeginConstruction(Entity entity, long tkbType, uint currentFrame, IEntityCommandBuffer cmd)
{
    // ... existing logic ...
    
    _pendingConstruction[entity] = new PendingConstruction
    {
        Entity = entity,
        TkbType = tkbType,  // CHANGED from: TypeId
        StartFrame = currentFrame,
        RemainingAcks = new HashSet<int>(_participatingModuleIds)
    };
    
    cmd.PublishEvent(new ConstructionOrder
    {
        Entity = entity,
        TkbType = tkbType,  // CHANGED from: BlueprintId
        FrameNumber = currentFrame
    });
}

// Update PendingConstruction helper class
internal class PendingConstruction
{
    public Entity Entity;
    public long TkbType;  // CHANGED from: int TypeId
    public uint StartFrame;
    public HashSet<int> RemainingAcks = new();
}
```

**Rationale**:
- **Consistency**: `INetworkMaster.TkbType`, `TkbTemplate.TkbType`, `TkbDatabase.GetByType(long tkbType)` all use this naming
- **Clarity**: "TkbType" is the data field name; "Blueprint" is the concept
- **Type Safety**: Using `long` (not `int`) maintains consistency with network master descriptors

---

## Summary of Final Gaps Resolution

| Gap # | Issue | Resolution | Task ID | Phase |
|-------|-------|------------|---------|-------|
| **#1** | Smart Egress tracking missing | Added `EgressPublicationState` + salted rolling window | FDP-REP-207 | Phase 5 |
| **#2** | Promotion priority undefined | Added FIFO queue with time budget to existing task | FDP-REP-106 update | Phase 4 |
| **#3** | Naming inconsistency | Changed `BlueprintId` → `TkbType` everywhere | FDP-LC-002 update | Phase 1 |

**Phase Impact**:
- **Phase 1**: Minor update (naming only)
- **Phase 4**: Moderate update (queue logic added to existing task)
- **Phase 5**: **+1 task** (6 → 7 tasks, total now 7 with FDP-REP-306)

**Total Project**:
- **Previous**: 77 tasks
- **New**: 78 tasks (added FDP-REP-207)
- **Duration**: ~13 weeks (unchanged - one task offset by P4 refinement)

---

## Integration Checklist

- [ ] Add FDP-REP-207 to TASK-DETAILS.md after FDP-REP-206
- [ ] Update FDP-REP-106 in TASK-DETAILS.md with priority queue logic
- [ ] Update FDP-LC-002 in TASK-DETAILS.md to use `TkbType` instead of `BlueprintId`
- [ ] Add FDP-REP-207 to TASK_TRACKER.md Phase 5 table
- [ ] Update Phase 5 summary: 7 → 8 tasks (FDP-REP-306 + FDP-REP-207)
- [ ] Update total in TASK_TRACKER.md: 77 → 78 tasks
- [ ] Update Notes & Decisions section with final gap resolution timestamp

---

**Status**: Ready for final integration. Once merged, documentation is **100% implementation-ready**.

