using System;
using System.Collections.Generic;
using System.Linq;
using ModuleHost.Core.Network;

namespace ModuleHost.Network.Cyclone.Tests.Mocks
{
    public class MockDataSample : IDataSample
    {
        public object Data { get; set; }
        public DdsInstanceState InstanceState { get; set; } = DdsInstanceState.Alive;
        
        public long EntityId
        {
            get
            {
                if (Data is ModuleHost.Network.Cyclone.Topics.EntityStateTopic est) return est.EntityId;
                if (Data is ModuleHost.Network.Cyclone.Topics.EntityMasterTopic emt) return emt.EntityId;
                if (Data is long l) return l; // For disposal keys
                if (Data is ModuleHost.Core.Network.Messages.OwnershipUpdate ou) return ou.EntityId;
                // Callback to old types if strictly necessary, but preferably not.
                if (Data is EntityStateDescriptor esd) return esd.EntityId;
                return 0;
            }
        }

		public long InstanceId { get; set; } = 0;
	}

    public class MockDataReader : IDataReader
    {
        private readonly List<IDataSample> _samples;
        
        public string TopicName => "MockTopic";

        public MockDataReader(params object[] samples)
        {
            _samples = samples.Select(s => 
            {
                if (s is IDataSample ds) return ds;
                
                var state = DdsInstanceState.Alive;
                if (s is long) state = DdsInstanceState.NotAliveDisposed;

                return (IDataSample)new MockDataSample 
                { 
                    Data = s, 
                    InstanceState = state 
                };
            }).ToList();
        }
        
        public IEnumerable<IDataSample> TakeSamples()
        {
            var result = _samples.ToList();
            _samples.Clear();
            return result;
        }
        
        public void Dispose() { }
    }
    
    public class MockDataWriter : IDataWriter
    {
        public List<object> WrittenSamples { get; } = new List<object>();
        public List<long> DisposedIds { get; } = new List<long>();
        
        public string TopicName => "MockTopic";

        public void Write(object sample)
        {
            WrittenSamples.Add(sample);
        }

        public void Dispose(long networkEntityId)
        {
            DisposedIds.Add(networkEntityId);
        }
        
        public void Dispose() { }
        
        public void Clear()
        {
            WrittenSamples.Clear();
            DisposedIds.Clear();
        }
    }
}
