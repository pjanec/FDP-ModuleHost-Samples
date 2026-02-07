using Fdp.Kernel;
using Fdp.Examples.NetworkDemo.Components;
using FDP.Toolkit.Replication.Components;
using ModuleHost.Core.Network;
using ModuleHost.Network.Cyclone.Components;

namespace Fdp.Examples.NetworkDemo.Configuration
{
    public static class DemoComponentRegistry
    {
        public static void Register(EntityRepository world)
        {
            // Legacy components
            world.RegisterComponent<Position>();
            world.RegisterComponent<PositionGeodetic>();
            world.RegisterComponent<Velocity>();
            world.RegisterComponent<EntityType>();
            
            // Toolkit components
            world.RegisterComponent<NetworkPosition>();
            world.RegisterComponent<NetworkVelocity>();
            world.RegisterComponent<NetworkOrientation>();
            world.RegisterComponent<NetworkOwnership>();
            world.RegisterComponent<NetworkIdentity>();
            world.RegisterComponent<NetworkSpawnRequest>();
            world.RegisterComponent<PendingNetworkAck>();
            world.RegisterComponent<ForceNetworkPublish>();

            // Batch-03 Components
            world.RegisterComponent<DemoPosition>();
            world.RegisterComponent<TurretState>();
            world.RegisterComponent<TimeConfiguration>();
            world.RegisterComponent<ReplayTime>();
            world.RegisterComponent<NetworkAuthority>();
            world.RegisterManagedComponent<DescriptorOwnership>();
            world.RegisterComponent<Health>();
            world.RegisterComponent<TimeModeComponent>();
            world.RegisterComponent<FrameAckComponent>();

            // Demo tracking
            world.RegisterComponent<NetworkedEntity>(); 
        }
    }
}
