using System;
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

            foreach (var entity in query)
            {
                // If we own the chassis, copy to network buffer
                if (view.HasAuthority(entity, CHASSIS_KEY))
                {
                    var appPos = view.GetComponentRO<DemoPosition>(entity);
                    cmd.SetComponent(entity, new NetworkPosition
                    {
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

            foreach (var entity in query)
            {
                // If we DON'T own it, smooth toward network position
                if (!view.HasAuthority(entity, CHASSIS_KEY))
                {
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
