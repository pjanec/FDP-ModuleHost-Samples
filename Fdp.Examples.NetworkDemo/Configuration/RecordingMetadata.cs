using System;

namespace Fdp.Examples.NetworkDemo.Configuration
{
    [Serializable]
    public class RecordingMetadata
    {
        public long MaxEntityId { get; set; }
        public DateTime Timestamp { get; set; }
        public int NodeId { get; set; }
    }
}
