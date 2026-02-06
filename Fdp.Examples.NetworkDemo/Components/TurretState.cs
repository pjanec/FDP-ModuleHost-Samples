using System.ComponentModel.DataAnnotations;
using FDP.Interfaces.Abstractions;

namespace Fdp.Examples.NetworkDemo.Components
{
    [FdpDescriptor(20, "TurretState")]
    public struct TurretState
    {
        [Key]
        public uint EntityId { get; set; }
        public float Yaw { get; set; }
        public float Pitch { get; set; }
        public byte AmmoCount { get; set; }
    }
}
