using System;
using System.Collections.Generic;
using CycloneDDS.Runtime;
using Fdp.Interfaces; // For IDataReader, IDataWriter
using CoreInstanceState = Fdp.Interfaces.NetworkInstanceState;
using CycloneDdsInstanceState = CycloneDDS.Runtime.DdsInstanceState;

namespace ModuleHost.Network.Cyclone.Services
{
    public class CycloneDataReader<T> : IDataReader where T : struct
    {
        private readonly DdsReader<T> _reader;
        private readonly string _topicName;

        public CycloneDataReader(DdsReader<T> reader, string topicName)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _topicName = topicName;
        }

        public string TopicName => _topicName;
        
        public void Dispose() { }

        public IEnumerable<IDataSample> TakeSamples()
        {
            using var scope = _reader.Take();
            var list = new List<IDataSample>(scope.Count);
            
            var infos = scope.Infos;

            for (int i = 0; i < scope.Count; i++)
            {
                var data = scope[i];
                var info = infos[i];

                CoreInstanceState state = CoreInstanceState.Alive;
                
                switch (info.InstanceState)
                {
                    case CycloneDdsInstanceState.Alive: state = CoreInstanceState.Alive; break;
                    case CycloneDdsInstanceState.NotAliveDisposed: state = CoreInstanceState.NotAliveDisposed; break;
                    case CycloneDdsInstanceState.NotAliveNoWriters: state = CoreInstanceState.NotAliveNoWriters; break;
                }

                list.Add(new SampleData
                {
                    Data = data,
                    InstanceState = state,
                    InstanceId = info.InstanceHandle,
                    EntityId = 0
                });
            }
            return list;
        }
    }
    
    public class CycloneDataWriter<T> : IDataWriter where T : struct
    {
        private readonly DdsWriter<T> _writer;
        private readonly string _topicName;
        
        public CycloneDataWriter(DdsWriter<T> writer, string topicName)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _topicName = topicName;
        }
        
        public string TopicName => _topicName;
        
        public void Write(object sample)
        {
            if (sample is T typedSample)
                _writer.Write(typedSample);
        }
        
        public void Dispose() { }
        public void Dispose(long networkEntityId) 
        { 
            // Generic dispose by ID requires constructing a key sample.
            // For now, skipping explicit disposal 
        } 
    }
}
