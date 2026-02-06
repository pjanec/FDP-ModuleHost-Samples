using System;
using System.Numerics;
using Fdp.Interfaces;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using Fdp.Modules.Geographic;
using Fdp.Examples.NetworkDemo.Components;
using Fdp.Examples.NetworkDemo.Descriptors;
using FDP.Toolkit.Replication.Services;
using FDP.Toolkit.Replication.Extensions;

namespace Fdp.Examples.NetworkDemo.Translators
{
    public class GeodeticTranslator : IDescriptorTranslator
    {
        private readonly IGeographicTransform _geoTransform;
        private readonly NetworkEntityMap _entityMap;

        public string TopicName => "Tank_GeoState";
        public long DescriptorOrdinal => 5;

        public GeodeticTranslator(IGeographicTransform geoTransform, NetworkEntityMap entityMap)
        {
            _geoTransform = geoTransform;
            _entityMap = entityMap;
        }

        public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, ISimulationView view)
        {
            foreach (var sample in reader.TakeSamples())
            {
                if (sample.Data is not GeoStateDescriptor descriptor)
                    continue;

                // Map NetworkId to Entity
                if (!_entityMap.TryGetEntity(sample.EntityId, out var entity))
                {
                    // Entity not found (maybe not spawned yet or filtered)
                    continue;
                }

                // Ingress: Geodetic (Lat/Lon) -> Cartesian (Flat)
                var flatPos = _geoTransform.ToCartesian(descriptor.Lat, descriptor.Lon, descriptor.Alt);
                
                // Update ECS Component
                cmd.AddComponent(entity, new DemoPosition { 
                    Value = flatPos
                });
            }
        }

        public void ScanAndPublish(ISimulationView view, IDataWriter writer)
        {
            // Iterate all entities with the component we want to publish
            var query = view.Query().With<DemoPosition>().Build();
            
            foreach (var entity in query)
            {
                // 1. Must be a networked entity
                if (!_entityMap.TryGetNetworkId(entity, out var netId))
                    continue;

                // 2. Must have Authority to publish (prevent echo)
                // Using FDP.Toolkit.Replication.Extensions
                if (!view.HasAuthority(entity, DescriptorOrdinal))
                    continue;

                // Egress: Cartesian -> Geodetic
                var localPos = view.GetComponentRO<DemoPosition>(entity);
                var (lat, lon, alt) = _geoTransform.ToGeodetic(localPos.Value);

                var descriptor = new GeoStateDescriptor
                {
                    EntityId = (uint)netId, // descriptor uses uint, map uses long
                    Lat = lat,
                    Lon = lon,
                    Alt = (float)alt,
                    Heading = 0.0f // Heading not in DemoPosition yet
                };

                writer.Write(descriptor);
            }
        }

        public void ApplyToEntity(Entity entity, object data, EntityRepository repo)
        {
            if (data is GeoStateDescriptor descriptor)
            {
                var flatPos = _geoTransform.ToCartesian(descriptor.Lat, descriptor.Lon, descriptor.Alt);
                repo.AddComponent(entity, new DemoPosition { 
                    Value = flatPos
                });
            }
        }
    }
}
