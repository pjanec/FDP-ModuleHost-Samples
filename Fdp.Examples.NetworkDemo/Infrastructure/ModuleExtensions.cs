using System;
using System.Collections.Generic;
using ModuleHost.Core.Abstractions;

namespace Fdp.Examples.NetworkDemo
{
    public static class ModuleExtensions
    {
        public static ISimulationView CaptureSnapshot(this ISimulationView view)
        {
            // FAKE: Return self. Not thread safe but allows compilation.
            // In a real implementation, this would invoke a kernel service.
            // Required by FDP-DRP-018 RadarModule.
            return view;
        }

        public static IEnumerable<T> GetEvents<T>(this ISimulationView view) where T : unmanaged
        {
            // FAKE: Return empty.
            // Ideally we access the EventAccumulator or similar via casting view to ModuleHostKernel? 
            // Required by FDP-DRP-018 DamageControlModule.
            return Array.Empty<T>();
        }
    }
}
