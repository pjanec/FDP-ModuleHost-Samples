using System;
using System.Collections.Generic;
using CycloneDDS.Runtime;
using ModuleHost.Core.Network;
using CoreInstanceState = ModuleHost.Core.Network.DdsInstanceState;
using CycloneDdsInstanceState = CycloneDDS.Runtime.DdsInstanceState;

namespace ModuleHost.Network.Cyclone.Services
{
    public class CycloneDataReader<T, U> : IDataReader where U : struct
    {
        private readonly DdsReader<T, U> _reader;
        private readonly string _topicName;

        public CycloneDataReader(DdsReader<T, U> reader, string topicName)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _topicName = topicName;
        }

        public string TopicName => _topicName;

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

                list.Add(new DataSample
                {
                    Data = data,
                    InstanceState = state,
                    InstanceId = info.InstanceHandle
                });
            }
            return list;
        }

        public void Dispose()
        {
        }
    }

    public class CycloneDataWriter<T> : IDataWriter
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
            if (sample is T typedOrder)
            {
                _writer.Write(typedOrder);
            }
            else
            {
                throw new ArgumentException($"Expected sample of type {typeof(T).Name}, got {sample?.GetType().Name}");
            }
        }

        public void Dispose(long networkEntityId)
        {
        }

        public void Dispose()
        {
        }
    }
}
