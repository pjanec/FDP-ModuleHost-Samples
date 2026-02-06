using System.ComponentModel.DataAnnotations;

namespace Fdp.Examples.NetworkDemo.Descriptors
{
    public class GeoStateDescriptor
    {
        [Key]
        public uint EntityId { get; set; }
        
        public double Lat { get; set; }
        public double Lon { get; set; }
        public float Alt { get; set; }
        public float Heading { get; set; }
    }
}
