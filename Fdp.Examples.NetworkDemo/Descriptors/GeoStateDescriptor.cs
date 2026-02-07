using CycloneDDS.Schema;

namespace Fdp.Examples.NetworkDemo.Descriptors
{
    [DdsTopic("Tank_GeoState")]
    public partial struct GeoStateDescriptor
    {
        [DdsKey]
        public long EntityId;
        
        public double Lat;
        public double Lon;
        public float Alt;
        public float Heading;
    }
}
