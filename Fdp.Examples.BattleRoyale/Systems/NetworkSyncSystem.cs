using System;
using System.Collections.Generic;
using System.Numerics;
using ModuleHost.Core.Abstractions;
using Fdp.Kernel;
using Fdp.Examples.BattleRoyale.Components;
using FDP.Toolkit.Replication.Components;
using ModuleHost.Core.Network;

namespace Fdp.Examples.BattleRoyale.Systems
{
    [UpdateInPhase(SystemPhase.PostSimulation)]
    public class NetworkSyncSystem : IModuleSystem
    {
        public void Execute(ISimulationView view, float deltaTime)
        {
            var cmd = view.GetCommandBuffer();
            
            // Query entities that have both local and network representation
            var query = view.Query()
                .With<NetworkPosition>()
                .With<Position>()
                .With<NetworkOwnership>()
                .Build();

            foreach (var entity in query)
            {
                var ownership = view.GetComponentRO<NetworkOwnership>(entity);
                
                if (ownership.PrimaryOwnerId == ownership.LocalNodeId)
                {
                    // EGRESS: Local Authority -> Network State
                    // We own this entity, so we push our simulation state to the network component
                    var localPos = view.GetComponentRO<Position>(entity);
                    cmd.SetComponent(entity, new NetworkPosition { Value = localPos.Value });
                }
                else
                {
                    // INGRESS: Network State -> Local Simulation
                    // Someone else owns this, so we pull their state into our simulation
                    var netPos = view.GetComponentRO<NetworkPosition>(entity);
                    cmd.SetComponent(entity, new Position { Value = netPos.Value });
                }
            }
        }
    }
}
