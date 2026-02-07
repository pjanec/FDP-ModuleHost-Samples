using System;
using System.Collections.Generic;
using CycloneDDS.Runtime;
using Fdp.Interfaces; // For IDataReader, IDataWriter
using FDP.Kernel.Logging;
using CoreInstanceState = Fdp.Interfaces.NetworkInstanceState;
using CycloneDdsInstanceState = CycloneDDS.Runtime.DdsInstanceState;

namespace ModuleHost.Network.Cyclone.Services
{
    public class CycloneDataReader<T> : IDataReader where T : struct
    {
        private readonly DdsReader<T> _reader;
        private readonly string _topicName;
        private static System.Reflection.MemberInfo _entityIdMember;

        static CycloneDataReader()
        {

			// FIXME!!!
			// reflection is unacceptable!!!!


            _entityIdMember = (System.Reflection.MemberInfo)typeof(T).GetProperty("EntityId") 
                              ?? typeof(T).GetField("EntityId");
        }

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

                long entityId = 0;
                if (_entityIdMember != null)
                {
                     object val = null;
                     object boxed = data;
                     if (_entityIdMember is System.Reflection.PropertyInfo pi) val = pi.GetValue(boxed);
                     else if (_entityIdMember is System.Reflection.FieldInfo fi) val = fi.GetValue(boxed);
                     
                     if (val != null) entityId = Convert.ToInt64(val);
                }

                list.Add(new SampleData
                {
                    Data = data,
                    InstanceState = state,
                    InstanceId = info.InstanceHandle,
                    EntityId = entityId
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
            try 
            {
				// FIXME: we do not want to use reflection in a high performance path. We need to find other way
				// to set they keys on the struct - and not just EntityId but also the other keys if they exist. (partId)

				// Create default instance of T
				T sample = Activator.CreateInstance<T>();
                
                // Find EntityId property. 
                // We use reflection once. 
                // Optimization: Cached property info in static field? 
                // For now, simple reflection is acceptable as Dispose is not hot-path (Lifecycle event).
                
                var prop = typeof(T).GetProperty("EntityId");
                if (prop != null)
                {
                    // Check type of property
                    object val = networkEntityId;
                    
                    if (prop.PropertyType == typeof(ulong)) 
                        val = (ulong)networkEntityId;
                    else if (prop.PropertyType == typeof(int))
                        val = (int)networkEntityId;

                    // Set value on boxed struct
                    object boxed = sample;
                    prop.SetValue(boxed, val);
                    sample = (T)boxed;
                    
                    _writer.DisposeInstance(sample);
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash
                 FdpLog<CycloneDataWriter<T>>.Error($"[CycloneDataWriter] Error disposing entity {networkEntityId}: {ex.Message}");
            }
        } 
    }
}
