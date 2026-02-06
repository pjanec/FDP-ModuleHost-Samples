using System;
using System.Numerics;
using Fdp.Examples.NetworkDemo.Components;
using Fdp.Examples.NetworkDemo.Events;
using Fdp.Kernel;
using FDP.Toolkit.Replication.Components;
using ModuleHost.Core.Abstractions;

namespace Fdp.Examples.NetworkDemo.Modules
{
    [ExecutionPolicy(ExecutionMode.SlowBackground, priority: 1)]
    [SnapshotPolicy(SnapshotMode.OnDemand)]
    public class RadarModule : IModuleSystem
    {
        private readonly IEventBus _eventBus;
        private float _scanInterval = 1.0f; // 1Hz scan rate
        private float _accumulator = 0.0f;
        
        public RadarModule(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }
        
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
}
