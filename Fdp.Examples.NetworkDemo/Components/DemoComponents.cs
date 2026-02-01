using System.Numerics;

namespace Fdp.Examples.NetworkDemo.Components
{
    public struct Position
    {
        public Vector3 LocalCartesian;  // Local coordinates
        public override string ToString() => LocalCartesian.ToString();
    }

    public struct PositionGeodetic
    {
        public double Latitude;
        public double Longitude;
        public double Altitude;
        public override string ToString() => $"Lat:{Latitude:F6} Lon:{Longitude:F6} Alt:{Altitude:F1}";
    }

    public struct Velocity
    {
        public Vector3 Value;
        public override string ToString() => Value.ToString();
    }

    public class EntityType
    {
        public string Name; // "Tank", "Jeep", "Helicopter"
        public int TypeId;  // Corresponds to DIS type
        public override string ToString() => Name;
    }

    public struct NetworkedEntity
    {
        public long NetworkId;
        public int OwnerNodeId;
        public bool IsLocallyOwned;
        public override string ToString() => $"NetId:{NetworkId} Owner:{OwnerNodeId} Local:{IsLocallyOwned}";
    }
}
