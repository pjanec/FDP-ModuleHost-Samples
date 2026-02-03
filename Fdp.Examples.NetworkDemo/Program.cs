using System;
using System.Threading.Tasks;
using System.Numerics;
using Fdp.Kernel;
using Fdp.Examples.NetworkDemo.Components;
using Fdp.Modules.Geographic;
using Fdp.Modules.Geographic.Transforms;
using ModuleHost.Core;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Core.Network.Interfaces;
using ModuleHost.Core.ELM;
using ModuleHost.Network.Cyclone;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone.Modules;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tracking;

namespace Fdp.Examples.NetworkDemo;

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
        RegisterComponents(world);
        
        // Create kernel with event accumulator
        var kernel = new ModuleHostKernel(world, new EventAccumulator());
        
        // Network setup
        var participant = new DdsParticipant(domainId: 0);
        /*
        participant.EnableSenderTracking(new SenderIdentityConfig
        {
            AppDomainId = 1,
            AppInstanceId = instanceId
        });
        */
        
        var nodeMapper = new NodeIdMapper(localDomain: 1, localInstance: instanceId);
        int localInternalId = nodeMapper.LocalNodeId;

        // Using "Node_X" as client ID for Allocator
        var idAllocator = new DdsIdAllocator(participant, $"Node_{instanceId}");
        
        // Simple static topology: 100 and 200 know each other. 
        // We assume 100 and 200 are the only nodes.
        var topology = new StaticNetworkTopology(localNodeId: localInternalId, new int[] { 100, 200 }); // Assuming nodeIds match instanceIds for simplicity
        
        // Entity Lifecycle Module
        var elm = new EntityLifecycleModule(Array.Empty<int>()); 
        kernel.RegisterModule(elm);

        var networkModule = new CycloneNetworkModule(
            participant, nodeMapper, idAllocator, topology, elm
        );
        kernel.RegisterModule(networkModule);
        
        // Geographic module (origin: Berlin)
        var wgs84 = new WGS84Transform();
        wgs84.SetOrigin(52.52, 13.405, 0);
        
        var geoModule = new GeographicModule(wgs84);
        kernel.RegisterModule(geoModule);
        
        // Sync System - Bridging Local components to Network components
        kernel.RegisterGlobalSystem(new DemoNetworkSyncSystem(localInternalId));

        kernel.Initialize();
        
        Console.WriteLine("[INIT] Kernel initialized");
        Console.WriteLine("[INIT] Waiting for peer discovery...");
        await Task.Delay(2000);
        
        // Spawn local entities
        SpawnLocalEntities(world, instanceId, localInternalId);
        
        Console.WriteLine($"[SPAWN] Created 3 local entities");
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
        
        // kernel.Shutdown(); // If Shutdown exists
        participant.Dispose();
    }

    static void RegisterComponents(EntityRepository world)
    {
        world.RegisterComponent<Position>();
        world.RegisterComponent<PositionGeodetic>();
        world.RegisterComponent<Velocity>();
        world.RegisterComponent<EntityType>();
        
        // Register standard network components handled by Cyclone module
        // Manual registration
        world.RegisterComponent<ModuleHost.Network.Cyclone.Components.NetworkPosition>();
        world.RegisterComponent<ModuleHost.Network.Cyclone.Components.NetworkVelocity>();
        world.RegisterComponent<ModuleHost.Network.Cyclone.Components.NetworkOrientation>();
        // Fix: Use Core namespace for Ownership
        world.RegisterComponent<ModuleHost.Core.Network.NetworkOwnership>();
        world.RegisterComponent<ModuleHost.Network.Cyclone.Components.NetworkIdentity>();
        world.RegisterComponent<ModuleHost.Network.Cyclone.Components.NetworkSpawnRequest>();
        // Fix: Use Core namespace for PendingAck
        world.RegisterComponent<ModuleHost.Core.Network.PendingNetworkAck>();
        world.RegisterComponent<ModuleHost.Core.Network.ForceNetworkPublish>();

        world.RegisterComponent<NetworkedEntity>(); // Local component for demo tracking
    }

    static void SpawnLocalEntities(EntityRepository world, int instanceId, int localInternalId)
    {
        var types = new[] { ("Tank", 1), ("Jeep", 2), ("Helicopter", 3) };
        
        foreach (var (name, typeId) in types)
        {
            var entity = world.CreateEntity();
            
            world.AddComponent(entity, new EntityType { Name = name, TypeId = typeId });
            world.AddComponent(entity, new Position 
            { 
                LocalCartesian = new Vector3(
                    Random.Shared.Next(-50, 50),
                    Random.Shared.Next(-50, 50),
                    0
                )
            });
            world.AddComponent(entity, new PositionGeodetic
            {
                Latitude = 52.52 + Random.Shared.NextDouble() * 0.01,
                Longitude = 13.405 + Random.Shared.NextDouble() * 0.01,
                Altitude = 0
            });
            world.AddComponent(entity, new Velocity 
            { 
                Value = new Vector3(10, 5, 0) 
            });

            // Mark for network
            // In a real system, we'd use EntityLifecycleModule to request spawn.
            // Here we assume "Authority" creation pattern (we create locally, then tell network).
            
            // Add Network Components so Egress picks it up
            world.AddComponent(entity, new ModuleHost.Network.Cyclone.Components.NetworkIdentity 
            { 
                Value = (long)instanceId * 1000 + entity.Index // Fix: .Value instead of .NetworkId
            });
            world.AddComponent(entity, new ModuleHost.Core.Network.NetworkOwnership // Fix: Core namespace
            { 
                PrimaryOwnerId = localInternalId, // We own it
                LocalNodeId = localInternalId 
            });
            world.AddComponent(entity, new ModuleHost.Network.Cyclone.Components.NetworkPosition 
            { 
                Value = world.GetComponent<Position>(entity).LocalCartesian 
            });
            
            // Fix: Add NetworkSpawnRequest so EntityMasterTranslator picks it up for publication
            world.AddComponent(entity, new ModuleHost.Network.Cyclone.Components.NetworkSpawnRequest 
            { 
                DisType = (ulong)typeId,
                OwnerId = (ulong)instanceId
            });
            
            // Add Demo tracking component
            world.AddComponent(entity, new NetworkedEntity 
            { 
                NetworkId = (long)instanceId * 1000 + entity.Index,
                OwnerNodeId = localInternalId,
                IsLocallyOwned = true
            });
        }
    }

    static void PrintStatus(EntityRepository world, NodeIdMapper mapper, int localInstanceId)
    {
        // Query entities that have NetworkIdentity (synced entities)
        var query = world.Query()
            .With<ModuleHost.Network.Cyclone.Components.NetworkIdentity>()
            .With<ModuleHost.Core.Network.NetworkOwnership>() // Fix: Core namespace
            .Build();

        Console.WriteLine($"[STATUS] Frame snapshot:");
        
        int localCount = 0;
        int remoteCount = 0;
        
        foreach (var e in query)
        {
             ref readonly var netId = ref world.GetComponentRO<ModuleHost.Network.Cyclone.Components.NetworkIdentity>(e);
             ref readonly var ownership = ref world.GetComponentRO<ModuleHost.Core.Network.NetworkOwnership>(e);
             
             // Try to get EntityType if available (might not be synced automatically for remote)
             string typeName = "Unknown";
             if (world.HasComponent<EntityType>(e))
             {
                 typeName = world.GetComponent<EntityType>(e).Name;
             }
             
             // Try get position
             Vector3 pos = Vector3.Zero;
             if (world.HasComponent<ModuleHost.Network.Cyclone.Components.NetworkPosition>(e))
             {
                 pos = world.GetComponent<ModuleHost.Network.Cyclone.Components.NetworkPosition>(e).Value;
             }

             bool isLocal = ownership.PrimaryOwnerId == localInstanceId;
             string ownerStr = isLocal ? "LOCAL" : $"REMOTE({ownership.PrimaryOwnerId})";
             
             if (isLocal) localCount++; else remoteCount++;

             Console.WriteLine($"  [{ownerStr}] {typeName,-12} " +
                            $"Pos: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1}) " +
                            $"NetID: {netId.Value} "); // Fix: .Value
        }
        
        Console.WriteLine($"[STATUS] Local: {localCount}, Remote: {remoteCount}");
        
        // Testable output marker
        Console.WriteLine($"TEST_OUTPUT: LOCAL={localCount} REMOTE={remoteCount}");
        Console.WriteLine();
    }
}

// System to sync Local -> Net logic for demo
[UpdateInPhase(SystemPhase.Simulation)]
public class DemoNetworkSyncSystem : IModuleSystem
{
    private int _localNodeId;
    public DemoNetworkSyncSystem(int localNodeId) { _localNodeId = localNodeId; }

    public void Execute(ISimulationView view, float deltaTime)
    {
        var cmd = view.GetCommandBuffer();
        var query = view.Query()
            .With<Position>()
            .With<ModuleHost.Network.Cyclone.Components.NetworkPosition>()
            .With<ModuleHost.Core.Network.NetworkOwnership>() // Fix: Core namespace
            .Build();
            
        foreach (var entity in query)
        {
            ref readonly var ownership = ref view.GetComponentRO<ModuleHost.Core.Network.NetworkOwnership>(entity); // Fix: Core namespace
            if (ownership.PrimaryOwnerId == _localNodeId)
            {
                // Local -> Network
                ref readonly var pos = ref view.GetComponentRO<Position>(entity);
                cmd.SetComponent(entity, new ModuleHost.Network.Cyclone.Components.NetworkPosition { Value = pos.LocalCartesian });
            }
            else
            {
                // Network -> Local (For visualization)
                ref readonly var netPos = ref view.GetComponentRO<ModuleHost.Network.Cyclone.Components.NetworkPosition>(entity);
                cmd.SetComponent(entity, new Position { LocalCartesian = netPos.Value });
            }
        }
    }
}
