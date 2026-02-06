using System;
using System.Linq;
using System.Threading.Tasks;
using System.Numerics;
using System.Collections.Generic;
using Fdp.Kernel;
using Fdp.Kernel.FlightRecorder;
using Fdp.Kernel.FlightRecorder.Metadata;
using Fdp.Interfaces;
using Fdp.Examples.NetworkDemo.Components;
using Fdp.Examples.NetworkDemo.Configuration;
using Fdp.Examples.NetworkDemo.Descriptors;
using Fdp.Examples.NetworkDemo.Systems;
using Fdp.Examples.NetworkDemo.Modules;
using Fdp.Modules.Geographic;
using Fdp.Modules.Geographic.Transforms;
using ModuleHost.Core;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Core.Network.Interfaces;
using FDP.Toolkit.Lifecycle;
using FDP.Toolkit.Lifecycle.Events;
using Fdp.Toolkit.Tkb;
using FDP.Toolkit.Replication;
using FDP.Toolkit.Replication.Components;
using FDP.Toolkit.Time.Controllers;
using ModuleHost.Network.Cyclone;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone.Modules;
using ModuleHost.Network.Cyclone.Providers;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tracking;

namespace Fdp.Examples.NetworkDemo;

public class SerializationRegistry : ISerializationRegistry
{
    private readonly Dictionary<long, ISerializationProvider> _providers = new();

    public void Register(long descriptorOrdinal, ISerializationProvider provider)
    {
        _providers[descriptorOrdinal] = provider;
    }

    public ISerializationProvider Get(long descriptorOrdinal)
    {
        return _providers[descriptorOrdinal];
    }

    public bool TryGet(long descriptorOrdinal, out ISerializationProvider provider)
    {
        return _providers.TryGetValue(descriptorOrdinal, out provider);
    }
}

[UpdateInPhase(SystemPhase.Simulation)]
public class ComponentSystemWrapper : IModuleSystem
{
    private readonly ComponentSystem _sys;
    public ComponentSystemWrapper(ComponentSystem sys) => _sys = sys;
    public void Execute(ISimulationView view, float dt) => _sys.Run();
}

class Program
{
    static async Task Main(string[] args)
    {
        // Parse arguments
        int instanceId = args.Length > 0 ? int.Parse(args[0]) : 100;
        string modeArg = args.Length > 1 ? args[1].ToLower() : "live";
        string recordingPath = args.Length > 2 ? args[2] : $"node_{instanceId}.fdp";
        
        bool isReplay = modeArg == "replay";
        string nodeName = instanceId == 100 ? "Alpha" : "Bravo";
        
        Console.WriteLine("==========================================");
        Console.WriteLine($"  Network Demo - {nodeName} (ID: {instanceId}) [{modeArg.ToUpper()}]");
        Console.WriteLine("==========================================");
        Console.WriteLine();
        
        // Common Setup
        var world = new EntityRepository();
        RegisterComponents(world);

        var accumulator = new EventAccumulator();
        var kernel = new ModuleHostKernel(world, accumulator);
        var eventBus = new FdpEventBus(); // Shared event bus
        
        // --- 1. Network & Topology ---
        var participant = new DdsParticipant(domainId: 0);
        int GetInternalId(int instance) => new NodeIdMapper(localDomain: 1, localInstance: instance).LocalNodeId;
        var nodeMapper = new NodeIdMapper(localDomain: 1, localInstance: instanceId);
        int localInternalId = nodeMapper.LocalNodeId;
        var idAllocator = new DdsIdAllocator(participant, $"Node_{instanceId}");
        
        var peerInstances = new int[] { 100, 200 }.Where(x => x != instanceId).ToArray();
        var peerInternalIds = peerInstances.Select(GetInternalId).ToArray();
        var topology = new StaticNetworkTopology(localNodeId: localInternalId, peerInternalIds);

        // --- 2. TKB & Serialization ---
        var tkb = new TkbDatabase();
        world.SetSingletonManaged<Fdp.Interfaces.ITkbDatabase>(tkb);
        
        // Configuration
        TankTemplate.Register(tkb);
        
        var serializationRegistry = new SerializationRegistry();
        world.SetSingletonManaged<ISerializationRegistry>(serializationRegistry);
        
        var setup = new DemoTkbSetup();
        setup.Load(tkb);

        serializationRegistry.Register(DemoDescriptors.Physics, new CycloneSerializationProvider<NetworkPosition>());
        serializationRegistry.Register(DemoDescriptors.Master, new CycloneSerializationProvider<NetworkVelocity>());

        // --- 3. Modules Registration ---
        var elm = new EntityLifecycleModule(tkb, Array.Empty<int>()); 
        kernel.RegisterModule(elm);

        var networkModule = new CycloneNetworkModule(
            participant, nodeMapper, idAllocator, topology, elm, serializationRegistry
        );
        kernel.RegisterModule(networkModule);
        
        var wgs84 = new WGS84Transform();
        wgs84.SetOrigin(52.52, 13.405, 0);
        var geoModule = new GeographicModule(wgs84);
        kernel.RegisterModule(geoModule);

        // --- 4. Mode Specific Setup ---
        
        AsyncRecorder recorder = null;

        if (!isReplay)
        {
            // === LIVE MODE ===
            
            // Reserve System IDs
            world.ReserveIdRange(FdpConfig.SYSTEM_ID_RANGE);
            Console.WriteLine($"[Init] Reserved ID range 0-{FdpConfig.SYSTEM_ID_RANGE}");
            
            // Register Systems
            kernel.RegisterGlobalSystem(new TimeInputSystem()); 
            kernel.RegisterGlobalSystem(new TransformSyncSystem());
            
            // Advanced Modules (Part B)
            // Note: RadarModule and DamageControlModule are IModuleSystem, not IModule (stateful)
            // But kernel.RegisterModule expects IModule.
            // We should use kernel.RegisterGlobalSystem for IModuleSystem.
            kernel.RegisterGlobalSystem(new RadarModule(eventBus));
            kernel.RegisterGlobalSystem(new DamageControlModule());
            kernel.RegisterGlobalSystem(new OwnershipInputSystem(localInternalId, eventBus));

            // Setup Recorder
            recorder = new AsyncRecorder(recordingPath);
            var recorderSys = new RecorderTickSystem(recorder, world);
            recorderSys.SetMinRecordableId(FdpConfig.SYSTEM_ID_RANGE);
            kernel.RegisterGlobalSystem(recorderSys);
            
            // Live Demo Topology Systems
            foreach(var sys in DemoTopology.GetSystems(tkb, elm))
            {
                if (sys is IModuleSystem ms) kernel.RegisterGlobalSystem(ms);
                else if (sys is ComponentSystem cs) { cs.Create(world); kernel.RegisterGlobalSystem(new ComponentSystemWrapper(cs)); }
            }

            // Time Controller
            var timeController = new MasterTimeController(eventBus, null);
            kernel.SetTimeController(timeController);
        }
        else
        {
            // === REPLAY MODE ===
            
            string metaPath = recordingPath + ".meta";
            Fdp.Examples.NetworkDemo.Configuration.RecordingMetadata meta;
            try 
            {
               meta = MetadataManager.Load(metaPath);
               Console.WriteLine($"[Replay] Loaded metadata (MaxID: {meta.MaxEntityId})");
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"[Replay] Metadata load failed ({ex.Message}). Using default range.");
                meta = new Fdp.Examples.NetworkDemo.Configuration.RecordingMetadata { MaxEntityId = 1_000_000 };
            }

            // Reserve recorded ID range to prevent conflicts
            world.ReserveIdRange((int)meta.MaxEntityId);
            
            // Replay Bridge
            var replayBridge = new ReplayBridgeSystem(recordingPath, localInternalId);
            kernel.RegisterGlobalSystem(replayBridge);
            
            // Keep Network Receive Active
            kernel.RegisterGlobalSystem(new TransformSyncSystem());
            
            // Stepping Controller (TimeScale = 0)
            var dummyTime = new GlobalTime { TimeScale = 0 };
            kernel.SetTimeController(new SteppingTimeController(dummyTime));
            
            Console.WriteLine("[Mode] REPLAY - Playback active (Physics Disabled)");
        }

        // --- 5. Initialization & Loop ---

        kernel.Initialize();
        
        Console.WriteLine("[INIT] Kernel initialized");
        Console.WriteLine("[INIT] Waiting for peer discovery...");
        
        // Simple delay for discovery
        await Task.Delay(2000); // Allow DDS to settle
        
        if (!isReplay)
        {
             // Spawn entities only in Live mode
             SpawnLocalEntities(world, tkb, instanceId, localInternalId);
             Console.WriteLine($"[SPAWN] Created local entities");
        }
        
        Console.WriteLine();
        Console.WriteLine("==========================================");
        Console.WriteLine("           Values Running...              ");
        Console.WriteLine("==========================================");
        
        var cts = new System.Threading.CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
        
        int frameCount = 0;
        
        try 
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (isReplay)
                {
                    // CRITICAL: Advance global version in Replay
                    // Assuming EntityRepository supports Tick or update logic sufficient
                    // If not, ReplayBridge needs to drive it.
                    // For now, relying on Kernel.Update
                }
                
                kernel.Update(0.1f);
                
                if (frameCount % 60 == 0) // Less frequent printing
                {
                    PrintStatus(world, nodeMapper, localInternalId);
                }
                
                await Task.Delay(33); // ~30Hz loop
                frameCount++;
            }
        }
        catch (Exception ex)
        {
             Console.WriteLine($"[Error] {ex.Message}");
             Console.WriteLine(ex.StackTrace);
        }
        
        // --- 6. Cleanup ---
        
        if (!isReplay && recorder != null)
        {
            recorder.Dispose();
            var meta = new Fdp.Examples.NetworkDemo.Configuration.RecordingMetadata {
                MaxEntityId = world.MaxEntityIndex,
                Timestamp = DateTime.UtcNow,
                NodeId = instanceId
            };
            MetadataManager.Save(recordingPath + ".meta", meta);
            Console.WriteLine($"[Recorder] Saved metadata to {recordingPath}.meta");
        }
        
        participant.Dispose();
        Console.WriteLine("[SHUTDOWN] Done.");
    }

    // Helper: RegisterComponents
    static void RegisterComponents(EntityRepository world)
    {
        // Legacy components
        world.RegisterComponent<Position>();
        world.RegisterComponent<PositionGeodetic>();
        world.RegisterComponent<Velocity>();
        world.RegisterComponent<EntityType>();
        
        // Toolkit components
        world.RegisterComponent<NetworkPosition>();
        world.RegisterComponent<NetworkVelocity>();
        world.RegisterComponent<ModuleHost.Network.Cyclone.Components.NetworkOrientation>();
        world.RegisterComponent<ModuleHost.Core.Network.NetworkOwnership>();
        world.RegisterComponent<NetworkIdentity>();
        world.RegisterComponent<NetworkSpawnRequest>();
        world.RegisterComponent<ModuleHost.Core.Network.PendingNetworkAck>();
        world.RegisterComponent<ModuleHost.Core.Network.ForceNetworkPublish>();

        // Batch-03 Components
        world.RegisterComponent<DemoPosition>();
        world.RegisterComponent<TurretState>();
        world.RegisterComponent<TimeConfiguration>();
        world.RegisterComponent<ReplayTime>();
        world.RegisterComponent<FDP.Toolkit.Replication.Components.NetworkAuthority>();
        world.RegisterManagedComponent<FDP.Toolkit.Replication.Components.DescriptorOwnership>();
        world.RegisterComponent<Health>(); // Added for DamageControlModule

        // Demo tracking
        world.RegisterComponent<NetworkedEntity>(); 
    }

    // Helper: SpawnLocalEntities
    static void SpawnLocalEntities(EntityRepository world, TkbDatabase tkb, int instanceId, int localInternalId)
    {
        if (tkb.TryGetByName("CommandTank", out var template)) // Updated name B.3
        {
            for(int i=0; i<1; i++) // Just 1 tank per node for now
            {
                var entity = world.CreateEntity();
                template.ApplyTo(world, entity);
                
                // Override Properties
                
                // 1. Identity
                var netId = (long)instanceId * 1000 + entity.Index;
                world.SetComponent(entity, new NetworkIdentity { Value = netId });
                
                // 2. Ownership
                // Adding NetworkOwnership for NetworkModule compatibility
                world.AddComponent(entity, new ModuleHost.Core.Network.NetworkOwnership 
                { 
                    PrimaryOwnerId = localInternalId, 
                    LocalNodeId = localInternalId 
                });
                
                // Adding NetworkAuthority for ReplayBridge compatibility
                world.AddComponent(entity, new FDP.Toolkit.Replication.Components.NetworkAuthority(localInternalId, localInternalId));

                // 2b. Spawn Request
                world.AddComponent(entity, new NetworkSpawnRequest 
                { 
                    DisType = 100,
                    OwnerId = (ulong)localInternalId 
                });
                
                // 3. Initial Position
                world.SetComponent(entity, new DemoPosition 
                { 
                    Value = new Vector3(
                        Random.Shared.Next(-50, 50),
                        Random.Shared.Next(-50, 50),
                        0
                    )
                });
                
                 world.SetComponent(entity, new NetworkPosition 
                { 
                    Value = new Vector3(0,0,0) // synced position
                });
                
                world.AddComponent(entity, new EntityType { Name = "Tank", TypeId = 1 });
            }
        }
    }

    // Helper: PrintStatus
    static void PrintStatus(EntityRepository world, NodeIdMapper mapper, int localInstanceId)
    {
        var query = world.Query()
            .With<NetworkIdentity>()
            .With<FDP.Toolkit.Replication.Components.NetworkAuthority>() // Use Authority
            .Build();

        Console.WriteLine($"[STATUS] Frame snapshot:");
        
        int localCount = 0;
        int remoteCount = 0;
        
        foreach (var e in query)
        {
             ref readonly var netId = ref world.GetComponentRO<NetworkIdentity>(e);
             ref readonly var auth = ref world.GetComponentRO<FDP.Toolkit.Replication.Components.NetworkAuthority>(e);
             
             string typeName = "Unknown";
             if (world.HasComponent<EntityType>(e)) typeName = world.GetComponent<EntityType>(e).Name;
             
             Vector3 pos = Vector3.Zero;
             if (world.HasComponent<DemoPosition>(e)) pos = world.GetComponent<DemoPosition>(e).Value;
             else if (world.HasComponent<NetworkPosition>(e)) pos = world.GetComponent<NetworkPosition>(e).Value;

             bool isLocal = auth.PrimaryOwnerId == localInstanceId;
             string ownerStr = isLocal ? "LOCAL" : $"REMOTE({auth.PrimaryOwnerId})";
             
             if (isLocal) localCount++; else remoteCount++;

             Console.WriteLine($"  [{ownerStr}] {typeName,-12} " +
                            $"Pos: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1}) " +
                            $"NetID: {netId.Value} ");
        }
        
        Console.WriteLine($"[STATUS] Local: {localCount}, Remote: {remoteCount}");
    }
}
