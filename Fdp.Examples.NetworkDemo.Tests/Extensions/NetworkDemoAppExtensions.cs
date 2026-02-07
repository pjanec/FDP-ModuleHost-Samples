using System;
using System.Numerics;
using Fdp.Kernel;
using Fdp.Interfaces;
using Fdp.Examples.NetworkDemo;
using Fdp.Examples.NetworkDemo.Components;
using FDP.Toolkit.Replication.Components;
using ModuleHost.Core.Network;

namespace Fdp.Examples.NetworkDemo.Tests.Extensions
{
    public static class NetworkDemoAppExtensions
    {
        public static Entity SpawnTank(this NetworkDemoApp app)
        {
            if (app.Tkb.TryGetByName("CommandTank", out var template))
            {
                var entity = app.World.CreateEntity();
                template.ApplyTo(app.World, entity);

                // Identity
                var netId = (long)app.InstanceId * 1000 + entity.Index; 
                app.World.SetComponent(entity, new NetworkIdentity { Value = netId });

                // Ownership
                app.World.AddComponent(entity, new NetworkOwnership 
                { 
                    PrimaryOwnerId = app.LocalNodeId, 
                    LocalNodeId = app.LocalNodeId 
                });
                
                app.World.AddComponent(entity, new NetworkAuthority(app.LocalNodeId, app.LocalNodeId));

                // Spawn Request
                app.World.AddComponent(entity, new NetworkSpawnRequest 
                { 
                    DisType = 100,
                    OwnerId = (ulong)app.LocalNodeId 
                });
                
                // Initial Position
                app.World.SetComponent(entity, new DemoPosition 
                { 
                    Value = new Vector3(
                        Random.Shared.Next(-50, 50),
                        Random.Shared.Next(-50, 50),
                        0
                    )
                });
                
                app.World.SetComponent(entity, new NetworkPosition 
                { 
                    Value = new Vector3(0,0,0)
                });
                
                app.World.AddComponent(entity, new EntityType { Name = "Tank", TypeId = 1 });
                
                app.EntityMap.Register(netId, entity);

                return entity;
            }
            throw new Exception("Tank template not found");
        }

        public static long GetNetworkId(this NetworkDemoApp app, Entity entity)
        {
            if (app.World.HasComponent<NetworkIdentity>(entity))
            {
                return app.World.GetComponent<NetworkIdentity>(entity).Value;
            }
            throw new Exception($"Entity {entity} has no NetworkIdentity");
        }

        public static Entity GetEntityByNetId(this NetworkDemoApp app, long netId)
        {
            if (app.TryGetEntityByNetId(netId, out var entity))
                return entity;
            return Entity.Null;
        }

        public static bool TryGetEntityByNetId(this NetworkDemoApp app, long netId, out Entity entity)
        {
             var query = app.World.Query().With<NetworkIdentity>().Build();
             foreach(var e in query)
             {
                 if (app.World.GetComponent<NetworkIdentity>(e).Value == netId)
                 {
                     entity = e;
                     return true;
                 }
             }
             entity = Entity.Null;
             return false;
        }
    }
}
