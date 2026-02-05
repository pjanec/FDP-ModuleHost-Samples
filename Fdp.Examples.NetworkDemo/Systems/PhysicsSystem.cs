using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using FDP.Toolkit.Replication.Components;

namespace Fdp.Examples.NetworkDemo.Systems
{
    [UpdateInPhase(SystemPhase.Simulation)]
    public class PhysicsSystem : IModuleSystem
    {
        public void Execute(ISimulationView view, float deltaTime)
        {
            var cmd = view.GetCommandBuffer();
            var query = view.Query()
                .With<NetworkPosition>()
                .With<NetworkVelocity>()
                 // Only move local entities (remote positions come from network)
                .With<ModuleHost.Core.Network.NetworkOwnership>() 
                .Build();

            foreach (var e in query)
            {
                ref readonly var ownership = ref view.GetComponentRO<ModuleHost.Core.Network.NetworkOwnership>(e);
                if (ownership.PrimaryOwnerId != ownership.LocalNodeId)
                    continue;

                ref readonly var pos = ref view.GetComponentRO<NetworkPosition>(e);
                ref readonly var vel = ref view.GetComponentRO<NetworkVelocity>(e);
                
                var newPos = pos.Value + vel.Value * deltaTime;
                
                cmd.SetComponent(e, new NetworkPosition { Value = newPos });
            }
        }
    }
}
