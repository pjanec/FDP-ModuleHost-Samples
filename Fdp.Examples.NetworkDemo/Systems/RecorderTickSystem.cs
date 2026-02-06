using System;
using Fdp.Kernel;
using Fdp.Kernel.FlightRecorder;
using ModuleHost.Core.Abstractions;

namespace Fdp.Examples.NetworkDemo.Systems
{
    [UpdateInPhase(SystemPhase.PostSimulation)]
    public class RecorderTickSystem : IModuleSystem
    {
        private readonly AsyncRecorder _recorder;
        private readonly EntityRepository _repo;
        private uint _tickCount = 0;

        public RecorderTickSystem(AsyncRecorder recorder, EntityRepository repo)
        {
            _recorder = recorder;
            _repo = repo;
        }

        public void SetMinRecordableId(int minId)
        {
            _recorder.MinRecordableId = minId;
        }

        public void Execute(ISimulationView view, float dt)
        {
            // Capture frame.
            _recorder.CaptureFrame(_repo, _tickCount++);
        }
    }
}
