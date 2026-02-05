using System;
using System.Runtime.InteropServices;
using Fdp.Interfaces;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;

namespace ModuleHost.Network.Cyclone.Providers
{
    public class CycloneSerializationProvider<T> : ISerializationProvider where T : unmanaged
    {
        public int GetSize(object descriptor)
        {
            return Marshal.SizeOf<T>();
        }

        public void Encode(object descriptor, Span<byte> buffer)
        {
            if (descriptor is T val)
            {
                // Write unmanaged struct to span
                MemoryMarshal.Write(buffer, ref val);
            }
            else
            {
                throw new ArgumentException($"Expected type {typeof(T).Name}, got {descriptor?.GetType().Name}");
            }
        }

        public void Apply(Entity entity, ReadOnlySpan<byte> buffer, IEntityCommandBuffer cmd)
        {
            // Zero-copy read from span
            var val = MemoryMarshal.Read<T>(buffer);
            cmd.SetComponent(entity, val); 
        }
    }
}
