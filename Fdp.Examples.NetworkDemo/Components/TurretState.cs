using System.ComponentModel.DataAnnotations;
using FDP.Interfaces.Abstractions;
using CycloneDDS.Schema;

namespace Fdp.Examples.NetworkDemo.Components
{
    [FdpDescriptor(20, "TurretState")]
    [DdsTopic("TurretState")]
    public partial struct TurretState
    {
        [DdsKey]
        public long EntityId;
        public float Yaw;
        public float Pitch;
        public byte AmmoCount;
    }
}
