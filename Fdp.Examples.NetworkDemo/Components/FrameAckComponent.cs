using FDP.Interfaces.Abstractions;
using CycloneDDS.Schema;

namespace Fdp.Examples.NetworkDemo.Components
{
    [FdpDescriptor(201, "FrameAckComponent")]
    [DdsTopic("FrameAckComponent")]
    public partial struct FrameAckComponent
    {
        [DdsKey]
        public long EntityId;

        public int SenderNodeId;
        public long CompletedFrameId;
    }
}
