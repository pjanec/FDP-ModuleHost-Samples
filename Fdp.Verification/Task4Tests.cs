using System;
using System.Linq;
using System.Reflection;
using Xunit;
using Fdp.Examples.NetworkDemo.Components;
using FDP.Interfaces.Abstractions;
using Fdp.Examples.NetworkDemo.Descriptors;
using System.ComponentModel.DataAnnotations;
using CycloneDDS.Core;

namespace Fdp.Verification
{
    public class Task4Tests
    {
        [Fact]
        public void TurretState_Has_FdpDescriptor_With_Id_20()
        {
            var type = typeof(TurretState);
            var attr = type.GetCustomAttribute<FdpDescriptorAttribute>();

            Assert.NotNull(attr);
            Assert.Equal(20, attr.Ordinal);
            Assert.Equal("TurretState", attr.TopicName);
        }

        [Fact]
        public void TurretState_Has_Key_Attribute_On_EntityId()
        {
            var type = typeof(TurretState);
            var prop = type.GetProperty("EntityId");
            Assert.NotNull(prop);
            
            var keyAttr = prop.GetCustomAttribute<KeyAttribute>();
            Assert.NotNull(keyAttr);
        }

        [Fact]
        public void GeoStateDescriptor_Is_Defined_Correctly()
        {
            var desc = new GeoStateDescriptor();
            desc.EntityId = 123;
            desc.Lat = 45.0;
            desc.Lon = 90.0;
            
            Assert.Equal(123u, desc.EntityId);
            Assert.Equal(45.0, desc.Lat);
        }
    }
}
