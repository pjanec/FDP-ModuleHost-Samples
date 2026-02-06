using System;
using System.Linq;
using System.Threading.Tasks;
using System.Numerics;
using System.Collections.Generic;
using Fdp.Kernel;
using Fdp.Interfaces;
using Fdp.Examples.NetworkDemo.Components;
using Fdp.Examples.NetworkDemo.Configuration;
using Fdp.Examples.NetworkDemo.Descriptors;
using Fdp.Modules.Geographic;
using Fdp.Modules.Geographic.Transforms;
using ModuleHost.Core;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Core.Network.Interfaces;
using FDP.Toolkit.Lifecycle;
using FDP.Toolkit.Lifecycle.Events;
using Fdp.Toolkit.Tkb;
using FDP.Toolkit.Replication.Components;
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
        // Parse instance ID from args
        int instanceId = args.Length > 0 ? int.Parse(args[0]) : 100;
        string nodeName = instanceId == 100 ? "Alpha" : "Bravo";
        
        Console.WriteLine("==========================================");
        Console.WriteLine($"  Network Demo - {nodeName} Node (ID: {instanceId})  ");
        Console.WriteLine("==========================================");
        Console.WriteLine();
        
        // Setup
        var world = new EntityRepository();
        // RegisterComponents(world); // No longer needed, TKB handles it? 
        // We still need to register types for queries if TKB doesn't do it globally on Register?
        // Use manual registration for safety
        RegisterComponents(world);

        // Create kernel with event accumulator
        var accumulator = new EventAccumulator();
        var kernel = new ModuleHostKernel(world, accumulator);
        
        // Network setup
        var participant = new DdsParticipant(domainId: 0);
        
        // Mapper helpers
        int GetInternalId(int instance) => new NodeIdMapper(localDomain: 1, localInstance: instance).LocalNodeId;

        // Current Node
        var nodeMapper = new NodeIdMapper(localDomain: 1, localInstance: instanceId);
        int localInternalId = nodeMapper.LocalNodeId;

        // Using "Node_X" as client ID for Allocator
        var idAllocator = new DdsIdAllocator(participant, $"Node_{instanceId}");
        
        // Topology - Map all expected peers
        var peerInstances = new int[] { 100, 200 };
        var peerInternalIds = peerInstances.Select(GetInternalId).ToArray();
        
        var topology = new StaticNetworkTopology(localNodeId: localInternalId, peerInternalIds);
        
        // --- BATCH-11 Integration ---
        
        // 1. Instantiate TkbDatabase
        var tkb = new TkbDatabase();
        world.SetSingletonManaged<Fdp.Interfaces.ITkbDatabase>(tkb);
        
        // 2. Instantiate SerializationRegistry
        var serializationRegistry = new SerializationRegistry();
        world.SetSingletonManaged<ISerializationRegistry>(serializationRegistry);
        
        // 3. Load DemoTkbSetup
        var setup = new DemoTkbSetup();
        setup.Load(tkb);
        
        // 4. Register Serialization Providers
        // Physics Descriptor -> NetworkPosition
        serializationRegistry.Register(DemoDescriptors.Physics, new CycloneSerializationProvider<NetworkPosition>());
        // Master Descriptor -> NetworkVelocity (as an example of secondary channel)
        serializationRegistry.Register(DemoDescriptors.Master, new CycloneSerializationProvider<NetworkVelocity>());
        
        // Entity Lifecycle Module
        var elm = new EntityLifecycleModule(tkb, Array.Empty<int>()); 
        kernel.RegisterModule(elm);

        // Pass SerializationRegistry to CycloneNetworkModule
        var networkModule = new CycloneNetworkModule(
            participant, nodeMapper, idAllocator, topology, elm, serializationRegistry
        );
        kernel.RegisterModule(networkModule);
        
        // Geographic module (origin: Berlin) - Keeping from legacy
        var wgs84 = new WGS84Transform();
        wgs84.SetOrigin(52.52, 13.405, 0);
        
        var geoModule = new GeographicModule(wgs84);
        kernel.RegisterModule(geoModule);
        
        // Register Demo Topology Systems
        foreach(var sys in DemoTopology.GetSystems(tkb, elm))
        {
            if (sys is IModuleSystem ms)
            {
               kernel.RegisterGlobalSystem(ms);
            }
            else if (sys is ComponentSystem cs)
            {
               cs.Create(world);
               kernel.RegisterGlobalSystem(new ComponentSystemWrapper(cs));
            }
        }
        
        // Time Controller
        var bus = new FdpEventBus();
        var timeController = new FDP.Toolkit.Time.Controllers.MasterTimeController(bus, null);
        kernel.SetTimeController(timeController);

        kernel.Initialize();
        
        Console.WriteLine("[INIT] Kernel initialized");
        Console.WriteLine("[INIT] Waiting for peer discovery...");
        await Task.Delay(2000);
        
        // Spawn local entities using TKB
        SpawnLocalEntities(world, tkb, localInternalId, localInternalId);
        
        Console.WriteLine($"[SPAWN] Created local entities");
        Console.WriteLine();
        Console.WriteLine("==========================================");
        Console.WriteLine("           Monitoring Network             ");
        Console.WriteLine("==========================================");
        Console.WriteLine();
        
        // Main loop
        int frameCount = 0;
        // Run until cancelled or count reached.
        var cts = new System.Threading.CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

        while (!cts.Token.IsCancellationRequested && frameCount < 200) // 20 seconds @ 10Hz
        {
            kernel.Update(0.1f);
            
            if (frameCount % 10 == 0)
            {
                PrintStatus(world, nodeMapper, localInternalId);
            }
            
            await Task.Delay(100);
            frameCount++;
        }
        
        Console.WriteLine();
        Console.WriteLine("[SHUTDOWN] Demo complete");
        
        // kernel.Shutdown(); 
        participant.Dispose();
    }

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

        // Demo tracking
        world.RegisterComponent<NetworkedEntity>(); 
    }

    static void SpawnLocalEntities(EntityRepository world, TkbDatabase tkb, int instanceId, int localInternalId)
    {
        // Use TKB to spawn
        if (tkb.TryGetByName("Tank", out var template))
        {
            for(int i=0; i<3; i++)
            {
                var entity = world.CreateEntity();
                template.ApplyTo(world, entity);
                
                // Override/Set specific properties
                
                // 1. Identity
                var netId = (long)instanceId * 1000 + entity.Index;
                world.SetComponent(entity, new NetworkIdentity { Value = netId });
                
                // 2. Ownership
                world.AddComponent(entity, new ModuleHost.Core.Network.NetworkOwnership 
                { 
                    PrimaryOwnerId = localInternalId, 
                    LocalNodeId = localInternalId 
                });

                // 2b. Spawn Request (Required for Egress)
                world.AddComponent(entity, new NetworkSpawnRequest 
                { 
                    DisType = 100, // Tank Type
                    OwnerId = (ulong)localInternalId 
                });
                
                // 3. Initial Position/Vel
                world.SetComponent(entity, new NetworkPosition 
                { 
                    Value = new Vector3(
                        Random.Shared.Next(-50, 50),
                        Random.Shared.Next(-50, 50),
                        0
                    )
                });
                
                world.SetComponent(entity, new NetworkVelocity 
                { 
                    Value = new Vector3(10, 5, 0) // Moving
                });
                
                // Add demo visual components
                world.AddComponent(entity, new EntityType { Name = "Tank", TypeId = 1 });
            }
        }
    }

    static void PrintStatus(EntityRepository world, NodeIdMapper mapper, int localInstanceId)
    {
        // Query entities that have NetworkIdentity (synced entities)
        var query = world.Query()
            .With<NetworkIdentity>()
            .With<ModuleHost.Core.Network.NetworkOwnership>()
            .Build();

        Console.WriteLine($"[STATUS] Frame snapshot:");
        
        int localCount = 0;
        int remoteCount = 0;
        
        foreach (var e in query)
        {
             ref readonly var netId = ref world.GetComponentRO<NetworkIdentity>(e);
             ref readonly var ownership = ref world.GetComponentRO<ModuleHost.Core.Network.NetworkOwnership>(e);
             
             string typeName = "Unknown";
             if (world.HasComponent<EntityType>(e))
             {
                 typeName = world.GetComponent<EntityType>(e).Name;
             }
             
             // Get position from NetworkPosition (Single Source of Truth)
             Vector3 pos = Vector3.Zero;
             if (world.HasComponent<NetworkPosition>(e))
             {
                 pos = world.GetComponent<NetworkPosition>(e).Value;
             }

             bool isLocal = ownership.PrimaryOwnerId == localInstanceId;
             string ownerStr = isLocal ? "LOCAL" : $"REMOTE({ownership.PrimaryOwnerId})";
             
             if (isLocal) localCount++; else remoteCount++;

             Console.WriteLine($"  [{ownerStr}] {typeName,-12} " +
                            $"Pos: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1}) " +
                            $"NetID: {netId.Value} ");
        }
        
        Console.WriteLine($"[STATUS] Local: {localCount}, Remote: {remoteCount}");
    }
}
