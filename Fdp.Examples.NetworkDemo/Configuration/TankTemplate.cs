using Fdp.Examples.NetworkDemo.Components;
using Fdp.Interfaces;
using Fdp.Kernel;
using FDP.Toolkit.Replication;
using FDP.Toolkit.Replication.Components;

namespace Fdp.Examples.NetworkDemo.Configuration
{
    public static class TankTemplate
    {
        public static void Register(ITkbDatabase tkb)
        {
            var tank = new TkbTemplate("CommandTank", 100);
            
            // Core components
            tank.AddComponent(new DemoPosition());
            tank.AddComponent(new TurretState());
            tank.AddComponent(new Health { Value = 100 });
            
            // Network components
            tank.AddComponent(new NetworkIdentity());
            tank.AddComponent(new NetworkPosition());
            tank.AddComponent(new NetworkVelocity());
            tank.AddComponent(new ModuleHost.Core.Network.NetworkOwnership());
            
            // HARD REQUIREMENT: Chassis (Position/Rotation)
            // Entity stays as Ghost until this arrives
            tank.MandatoryDescriptors.Add(new MandatoryDescriptor {
                PackedKey = PackedKey.Create(5, 0), // Chassis descriptor
                IsHard = true
            });
            
            // SOFT REQUIREMENT: Turret (Aim angles)
            // Entity spawns after timeout even if this hasn't arrived
            tank.MandatoryDescriptors.Add(new MandatoryDescriptor {
                PackedKey = PackedKey.Create(10, 0), // Turret descriptor
                IsHard = false,
                SoftTimeoutFrames = 60 // 1 second at 60Hz
            });
            
            tkb.Register(tank);
        }
    }
}
