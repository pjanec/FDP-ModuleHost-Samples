using System.Collections.Generic;
using ModuleHost.Core.Abstractions;
using FDP.Toolkit.Lifecycle.Systems;
using FDP.Toolkit.Replication.Systems;
using Fdp.Examples.NetworkDemo.Systems;
using Fdp.Interfaces;
using FDP.Toolkit.Lifecycle;

namespace Fdp.Examples.NetworkDemo.Configuration
{
    public static class DemoTopology
    {
        public static IEnumerable<object> GetSystems(ITkbDatabase tkb, EntityLifecycleModule elm)
        {
            var systems = new List<object>();

            // Lifecycle
            systems.Add(new LifecycleSystem(elm));
            systems.Add(new BlueprintApplicationSystem(tkb));
            
            // Replication
            systems.Add(new GhostCreationSystem()); 
            systems.Add(new GhostPromotionSystem());
            systems.Add(new SmartEgressSystem());
            
            // Demo Specific
            systems.Add(new RefactoredPlayerInputSystem());
            systems.Add(new PhysicsSystem());
            
            return systems;
        }
    }
}
