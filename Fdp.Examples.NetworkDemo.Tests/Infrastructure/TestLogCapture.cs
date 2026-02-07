using System;
using System.Collections.Concurrent;
using NLog;
using NLog.Targets;

namespace Fdp.Examples.NetworkDemo.Tests.Infrastructure
{
    [Target("TestLogCapture")]
    public sealed class TestLogCapture : TargetWithLayout
    {
        public ConcurrentQueue<string> Logs { get; } = new ConcurrentQueue<string>();

        protected override void Write(LogEventInfo logEvent)
        {
            string logMessage = this.Layout.Render(logEvent);
            Logs.Enqueue(logMessage);
        }

        public void Clear()
        {
            Logs.Clear();
        }
    }
}
