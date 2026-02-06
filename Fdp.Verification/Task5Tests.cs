using System;
using System.Collections.Generic;
using System.Numerics;
using Fdp.Interfaces;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using Fdp.Modules.Geographic;
using Fdp.Examples.NetworkDemo.Components;
using Fdp.Examples.NetworkDemo.Descriptors;
using Fdp.Examples.NetworkDemo.Translators;
using FDP.Toolkit.Replication.Services;
using Moq;
using Xunit;

namespace Fdp.Verification
{
    public class Task5Tests
    {
        [Fact]
        public void PollIngress_Updates_Component()
        {
            // Arrange
            var mockGeo = new Mock<IGeographicTransform>();
            // Relax argument matching to ensure return value is provided
            mockGeo.Setup(x => x.ToCartesian(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                   .Returns(new Vector3(10, 20, 30));
            
            var entityMap = new NetworkEntityMap();
            var entity = new Entity(1, 1);
            entityMap.Register(1001, entity);
            
            var translator = new GeodeticTranslator(mockGeo.Object, entityMap);
            
            var descriptor = new GeoStateDescriptor { EntityId = 1001, Lat = 45.0, Lon = 90.0, Alt = 100.0f };
            // Manually creating SampleData since it's simple class in Fdp.Interfaces
            var sample = new SampleData { EntityId = 1001, Data = descriptor };
            
            var mockReader = new Mock<IDataReader>();
            mockReader.Setup(r => r.TakeSamples()).Returns(new List<IDataSample> { sample });
            
            var mockCmd = new Mock<IEntityCommandBuffer>();
            var mockView = new Mock<ISimulationView>();
            
            // Act
            translator.PollIngress(mockReader.Object, mockCmd.Object, mockView.Object);
            
            // Assert
            Assert.Single(mockCmd.Invocations);
            var invocation = mockCmd.Invocations[0];
            Assert.Equal("AddComponent", invocation.Method.Name);
            
            // Arguments might be (Entity, DemoPosition)
            var p = (DemoPosition)invocation.Arguments[1];
            Assert.Equal(10, p.Value.X);
            Assert.Equal(20, p.Value.Y);
            Assert.Equal(30, p.Value.Z);
        }
    }
}
