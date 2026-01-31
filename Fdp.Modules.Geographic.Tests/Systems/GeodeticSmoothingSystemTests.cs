using System;
using System.Numerics;
using Xunit;
using Moq;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using Fdp.Modules.Geographic;
using Fdp.Modules.Geographic.Systems;
using Fdp.Modules.Geographic.Components;

using PositionGeodetic = Fdp.Modules.Geographic.Components.PositionGeodetic;

namespace Fdp.Modules.Geographic.Tests.Systems
{
    public class GeodeticSmoothingSystemTests : IDisposable
    {
        private readonly EntityRepository _repo;
        private readonly Mock<IGeographicTransform> _mockGeo;
        private readonly GeodeticSmoothingSystem _system;
        
        public GeodeticSmoothingSystemTests()
        {
            _repo = new EntityRepository();
            _repo.RegisterComponent<Position>();
            _repo.RegisterComponent<NetworkOwnership>();
            _repo.RegisterComponent<PositionGeodetic>();
            
            _mockGeo = new Mock<IGeographicTransform>();
            _system = new GeodeticSmoothingSystem(_mockGeo.Object);
        }
        
        public void Dispose()
        {
            _repo.Dispose();
        }
        
        [Fact]
        public void Execute_RemoteEntity_InterpolatesPosition()
        {
            // Setup entity at (0,0,0)
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new Position { Value = Vector3.Zero });
            _repo.AddComponent(entity, new PositionGeodetic { Latitude = 10, Longitude = 10, Altitude = 100 });
            
            // Remote ownership (Local=1, Primary=2)
            _repo.AddComponent(entity, new NetworkOwnership { LocalNodeId = 1, PrimaryOwnerId = 2 });
            
            // Mock transform returns (10,0,0) as target
            var targetPos = new Vector3(10, 0, 0);
            _mockGeo.Setup(g => g.ToCartesian(10, 10, 100))
                .Returns(targetPos);
                
            // Execute with dt=0.05 -> t=0.5 -> Lerp 50%.
            
            _system.Execute(_repo, 0.05f);
            
            // Verify
            var pos = _repo.GetComponentRO<Position>(entity);
            // Lerp(0, 10, 0.5) = 5
            Assert.Equal(5.0f, pos.Value.X, 0.01f);
        }
        
        [Fact]
        public void Execute_LocalEntity_Ignored()
        {
            // Setup entity at (0,0,0)
            var entity = _repo.CreateEntity();
            _repo.AddComponent(entity, new Position { Value = Vector3.Zero });
            _repo.AddComponent(entity, new PositionGeodetic { Latitude = 10, Longitude = 10, Altitude = 100 });
            
            // Local ownership (Local=1, Primary=1)
            _repo.AddComponent(entity, new NetworkOwnership { LocalNodeId = 1, PrimaryOwnerId = 1 });
            
            _mockGeo.Setup(g => g.ToCartesian(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .Returns(new Vector3(10, 0, 0));
                
            _system.Execute(_repo, 0.05f);
            
            // Verify UNCHANGED
            var pos = _repo.GetComponentRO<Position>(entity);
            Assert.Equal(0.0f, pos.Value.X);
        }
    }
}
