using System;
using Fdp.Kernel;
using Fdp.Interfaces;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Network.Cyclone.Components;
using FDP.Toolkit.Replication.Components;
using ModuleHost.Network.Cyclone.Services;
using FDP.Toolkit.Replication.Services;
using ModuleHost.Network.Cyclone.Topics;

using NetworkEntityMap = FDP.Toolkit.Replication.Services.NetworkEntityMap;
using IDescriptorTranslator = Fdp.Interfaces.IDescriptorTranslator;
using IDataReader = Fdp.Interfaces.IDataReader;
using IDataWriter = Fdp.Interfaces.IDataWriter;

namespace ModuleHost.Network.Cyclone.Translators
{
    public class EntityStateTranslator : IDescriptorTranslator
    {
        private readonly NetworkEntityMap _entityMap;
        
        public string TopicName => "SST_EntityState";
        public long DescriptorOrdinal => -1;

        public EntityStateTranslator(NetworkEntityMap entityMap)
        {
            _entityMap = entityMap;
        }

        public void ApplyToEntity(Entity entity, object data, EntityRepository repo) { }

        public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, ISimulationView view)
        {
            foreach (var sample in reader.TakeSamples())
            {
                if (sample.InstanceState != Fdp.Interfaces.NetworkInstanceState.Alive) continue;
                if (sample.Data is not EntityStateTopic topic) continue;

                if (_entityMap.TryGetEntity(topic.EntityId, out var entity))
                {
                     // Update NetworkPosition
                     cmd.SetComponent(entity, new NetworkPosition { Value = new System.Numerics.Vector3((float)topic.PositionX, (float)topic.PositionY, (float)topic.PositionZ) });
                     cmd.SetComponent(entity, new NetworkVelocity { Value = new System.Numerics.Vector3(topic.VelocityX, topic.VelocityY, topic.VelocityZ) });
                     cmd.SetComponent(entity, new NetworkOrientation { Value = new System.Numerics.Quaternion(topic.OrientationX, topic.OrientationY, topic.OrientationZ, topic.OrientationW) });
                }
            }
        }

        public void ScanAndPublish(ISimulationView view, IDataWriter writer)
        {
            // Publish local owned entities
            var query = view.Query()
                .With<NetworkIdentity>()
                .With<NetworkOwnership>()
                .With<NetworkPosition>() // Must have position to publish state
                .Build();

            foreach(var entity in query)
            {
                ref readonly var ownership = ref view.GetComponentRO<NetworkOwnership>(entity);
                if (ownership.PrimaryOwnerId != ownership.LocalNodeId) continue;

                ref readonly var identity = ref view.GetComponentRO<NetworkIdentity>(entity);
                ref readonly var pos = ref view.GetComponentRO<NetworkPosition>(entity);
                
                // Optional components
                var vel = view.HasComponent<NetworkVelocity>(entity) ? view.GetComponentRO<NetworkVelocity>(entity).Value : System.Numerics.Vector3.Zero;
                var rot = view.HasComponent<NetworkOrientation>(entity) ? view.GetComponentRO<NetworkOrientation>(entity).Value : System.Numerics.Quaternion.Identity;

                var topic = new EntityStateTopic
                {
                    EntityId = identity.Value,
                    PositionX = pos.Value.X,
                    PositionY = pos.Value.Y,
                    PositionZ = pos.Value.Z,
                    VelocityX = vel.X,
                    VelocityY = vel.Y,
                    VelocityZ = vel.Z,
                    OrientationX = rot.X,
                    OrientationY = rot.Y,
                    OrientationZ = rot.Z,
                    OrientationW = rot.W,
                    Timestamp = (long)view.Tick
                };

                writer.Write(topic);
            }
        }
    }
}
