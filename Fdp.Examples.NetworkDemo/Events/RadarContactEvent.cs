using System;
using System.Numerics;

namespace Fdp.Examples.NetworkDemo.Events
{
    public struct RadarContactEvent
    {
        public long EntityId;
        public Vector3 Position;
        public DateTime Timestamp;
    }
}
