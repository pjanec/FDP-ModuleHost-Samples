using System;
using Fdp.Examples.NetworkDemo.Components;
using Fdp.Examples.NetworkDemo.Events;
using Fdp.Kernel;
using FDP.Toolkit.Replication.Components;
using FDP.Toolkit.Replication.Extensions;
using ModuleHost.Core.Abstractions;

namespace Fdp.Examples.NetworkDemo.Systems
{
    [UpdateInPhase(SystemPhase.Simulation)]
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
}
