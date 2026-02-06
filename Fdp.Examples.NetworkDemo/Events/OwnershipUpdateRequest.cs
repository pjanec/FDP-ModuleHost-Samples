using System;

namespace Fdp.Examples.NetworkDemo.Events
{
    public struct OwnershipUpdateRequest
    {
        public long EntityId;
        public long DescriptorOrdinal;
        public long InstanceId;
        public int NewOwner;
        public DateTime Timestamp;
    }
}
