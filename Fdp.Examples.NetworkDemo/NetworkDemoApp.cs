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
using NLog;
using FDP.Kernel.Logging;

namespace Fdp.Examples.NetworkDemo
{
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

    public class NetworkDemoApp : IDisposable
    {
        public EntityRepository World { get; private set; }
        public ModuleHostKernel Kernel { get; private set; }
        
        private DdsParticipant participant;
        private AsyncRecorder recorder;
        private ReplayBridgeSystem replaySystem;
        private bool isReplay;
        private int instanceId;
        private string recordingPath;
        private int localInternalId;
        private NodeIdMapper nodeMapper;
        private TkbDatabase tkb;

        public async Task InitializeAsync(int nodeId, bool replayMode, string recPath = null)
        {
            using (ScopeContext.PushProperty("NodeId", nodeId))
            {
            FdpLog<NetworkDemoApp>.Info($"Starting Node {nodeId}...");
            instanceId = nodeId;
            isReplay = replayMode;
            recordingPath = recPath ?? $"node_{instanceId}.fdp";
            
            string nodeName = instanceId == 100 ? "Alpha" : "Bravo";
            
            Console.WriteLine("==========================================");
            Console.WriteLine($"  Network Demo - {nodeName} (ID: {instanceId}) [{ (isReplay ? "REPLAY" : "LIVE") }]");
            Console.WriteLine("==========================================");
            Console.WriteLine();
            
            // Common Setup
            World = new EntityRepository();
            DemoComponentRegistry.Register(World);

            var accumulator = new EventAccumulator();
            Kernel = new ModuleHostKernel(World, accumulator);
            var eventBus = new FdpEventBus(); // Shared event bus
            
            // --- 1. Network & Topology ---
            participant = new DdsParticipant(domainId: 0);
            int GetInternalId(int instance) => new NodeIdMapper(localDomain: 0, localInstance: instance).LocalNodeId;
            nodeMapper = new NodeIdMapper(localDomain: 0, localInstance: instanceId);
            localInternalId = nodeMapper.LocalNodeId;
            var idAllocator = new DdsIdAllocator(participant, $"Node_{instanceId}");
            
            var peerInstances = new int[] { 100, 200 }.Where(x => x != instanceId).ToArray();
            var peerInternalIds = peerInstances.Select(GetInternalId).ToArray();
            var topology = new StaticNetworkTopology(localNodeId: localInternalId, peerInternalIds);

            // --- 2. TKB & Serialization ---
            tkb = new TkbDatabase();
            World.SetSingletonManaged<Fdp.Interfaces.ITkbDatabase>(tkb);
            
            // Configuration
            TankTemplate.Register(tkb);
            
            var serializationRegistry = new SerializationRegistry();
            World.SetSingletonManaged<ISerializationRegistry>(serializationRegistry);
            
            // var setup = new DemoTkbSetup();
            // setup.Load(tkb); // CONFLICT: Type 100 already registered by TankTemplate

            serializationRegistry.Register(DemoDescriptors.Physics, new CycloneSerializationProvider<NetworkPosition>());
            serializationRegistry.Register(DemoDescriptors.Master, new CycloneSerializationProvider<NetworkVelocity>());

            // --- 3. Modules Registration ---
            var elm = new EntityLifecycleModule(tkb, Array.Empty<int>()); 
            Kernel.RegisterModule(elm);

            // Hoist WGS84 for Shared Use
            var wgs84 = new WGS84Transform();
            wgs84.SetOrigin(52.52, 13.405, 0);

            // Create Shared NetworkEntityMap and Translators
            var entityMap = new FDP.Toolkit.Replication.Services.NetworkEntityMap();
            var allTranslators = new List<Fdp.Interfaces.IDescriptorTranslator>();
            
            // 1. Geodetic (Manual)
            allTranslators.Add(new Fdp.Examples.NetworkDemo.Translators.GeodeticTranslator(wgs84, entityMap));
            
            // 2. Auto-generated (Reflection)
            allTranslators.AddRange(ReplicationBootstrap.CreateAutoTranslators(
                typeof(NetworkDemoApp).Assembly,
                entityMap,
                typeof(CycloneSerializationProvider<>)
            ));

            var networkModule = new CycloneNetworkModule(
                participant, nodeMapper, idAllocator, topology, elm, serializationRegistry,
                allTranslators,
                entityMap
            );
            Kernel.RegisterModule(networkModule);
            
            var geoModule = new GeographicModule(wgs84);
            Kernel.RegisterModule(geoModule);

            // --- 4. Mode Specific Setup ---
            
            recorder = null;

            if (!isReplay)
            {
                // === LIVE MODE ===
                
                // Reserve System IDs
                World.ReserveIdRange(FdpConfig.SYSTEM_ID_RANGE);
                Console.WriteLine($"[Init] Reserved ID range 0-{FdpConfig.SYSTEM_ID_RANGE}");
                
                // Register Systems
                IInputSource inputSource;
                try {
                    inputSource = Console.IsInputRedirected ? new NullInputSource() : new ConsoleInputSource();
                } catch {
                    inputSource = new NullInputSource();
                }
                Kernel.RegisterGlobalSystem(new TimeInputSystem(inputSource)); 
                Kernel.RegisterGlobalSystem(new TransformSyncSystem());
                
                // Advanced Modules (Part B)
                Kernel.RegisterGlobalSystem(new RadarModule(eventBus));
                Kernel.RegisterGlobalSystem(new DamageControlModule());
                Kernel.RegisterGlobalSystem(new OwnershipInputSystem(localInternalId, eventBus));

                // Setup Recorder
                recorder = new AsyncRecorder(recordingPath);
                var recorderSys = new RecorderTickSystem(recorder, World);
                recorderSys.SetMinRecordableId(FdpConfig.SYSTEM_ID_RANGE);
                Kernel.RegisterGlobalSystem(recorderSys);
                
                // Live Demo Topology Systems
                foreach(var sys in DemoTopology.GetSystems(tkb, elm))
                {
                    if (sys is IModuleSystem ms) Kernel.RegisterGlobalSystem(ms);
                    else if (sys is ComponentSystem cs) { cs.Create(World); Kernel.RegisterGlobalSystem(new ComponentSystemWrapper(cs)); }
                }

                // Time Controller
                var timeController = new MasterTimeController(eventBus, null);
                Kernel.SetTimeController(timeController);
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
                World.ReserveIdRange((int)meta.MaxEntityId);
                
                // Replay Bridge
                replaySystem = new ReplayBridgeSystem(recordingPath, localInternalId);
                Kernel.RegisterGlobalSystem(replaySystem);
                
                // Keep Network Receive Active
                Kernel.RegisterGlobalSystem(new TransformSyncSystem());
                
                // Stepping Controller (TimeScale = 0)
                var dummyTime = new GlobalTime { TimeScale = 0 };
                Kernel.SetTimeController(new SteppingTimeController(dummyTime));
                
                Console.WriteLine("[Mode] REPLAY - Playback active (Physics Disabled)");
            }

            // --- 5. Initialization ---

            Kernel.Initialize();
            
            Console.WriteLine("[INIT] Kernel initialized");
            Console.WriteLine("[INIT] Waiting for peer discovery...");
            
            // Simple delay for discovery
            await Task.Delay(2000); // Allow DDS to settle
            
            if (!isReplay)
            {
                 // Spawn entities only in Live mode
                 SpawnLocalEntities(World, tkb, instanceId, localInternalId);
                 Console.WriteLine($"[SPAWN] Created local entities");
            }
            } // End ScopeContext
        }

        public async Task RunLoopAsync(System.Threading.CancellationToken token)
        {
             using (ScopeContext.PushProperty("NodeId", instanceId))
             {
                int frameCount = 0;
                while (!token.IsCancellationRequested)
                {
                    Update(0.1f);
                    if (frameCount % 60 == 0) PrintStatus();
                    try { await Task.Delay(33, token); } catch (TaskCanceledException) { break; }
                    frameCount++;
                }
             }
        }

        public void Update(float dt)
        {
            if (isReplay)
            {
                World.Tick(); // CRITICAL: Advance global version in Replay
            }
            Kernel.Update(dt);
        }

        public void Stop()
        {
             Dispose();
        }

        public void Dispose()
        {
            if (!isReplay && recorder != null)
            {
                recorder.Dispose();
                var meta = new Fdp.Examples.NetworkDemo.Configuration.RecordingMetadata {
                    MaxEntityId = World.MaxEntityIndex,
                    Timestamp = DateTime.UtcNow,
                    NodeId = instanceId
                };
                try
                {
                    MetadataManager.Save(recordingPath + ".meta", meta);
                    Console.WriteLine($"[Recorder] Saved metadata to {recordingPath}.meta");
                } 
                catch (Exception ex)
                {
                    Console.WriteLine($"[Recorder] Failed to save metadata: {ex.Message}");
                }
            }
            
            participant?.Dispose();
            replaySystem?.Dispose();
            Console.WriteLine("[SHUTDOWN] Done.");
        }
        
        public void PrintStatus()
        {
            PrintStatus(World, nodeMapper, localInternalId);
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
                 
                 string ownershipInfo = "No Ownership";
                 if (world.HasComponent<ModuleHost.Core.Network.NetworkOwnership>(e))
                 {
                     ref readonly var own = ref world.GetComponentRO<ModuleHost.Core.Network.NetworkOwnership>(e);
                     ownershipInfo = $"Own(P:{own.PrimaryOwnerId} L:{own.LocalNodeId})";
                 }

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
                                $"NetID: {netId.Value} " +
                                $"{ownershipInfo}");
            }
            
            Console.WriteLine($"[STATUS] Local: {localCount}, Remote: {remoteCount}");
        }
    }
}