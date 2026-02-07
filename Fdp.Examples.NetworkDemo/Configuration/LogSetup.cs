using NLog;
using NLog.Config;
using NLog.Targets;

namespace Fdp.Examples.NetworkDemo.Configuration
{
    /// <summary>
    /// Centralized NLog configuration for different execution contexts.
    /// Provides presets for development, testing, and production environments.
    /// </summary>
    public static class LogSetup
    {
        /// <summary>
        /// Configure logging for development mode with optional verbose tracing.
        /// </summary>
        /// <param name="nodeId">The node identifier for log file naming</param>
        /// <param name="verboseTrace">Enable trace-level logging for network and replication modules</param>
        public static void ConfigureForDevelopment(int nodeId, bool verboseTrace = false)
        {
            var config = new LoggingConfiguration();

            // FILE TARGET with dynamic filename based on NodeId
            var logFile = new FileTarget("logFile")
            {
                FileName = $"logs/node_{nodeId}.log",
                Layout = "${longdate}|${level:uppercase=true}|Node-${scopeproperty:NodeId}|${logger:shortName}|${message}${exception:format=tostring}",
                KeepFileOpen = true,
                ConcurrentWrites = false,
                AutoFlush = false,
                ArchiveAboveSize = 10485760, // 10MB
                MaxArchiveFiles = 5
            };

            // ASYNC WRAPPER for background I/O (protects hot path)
            var asyncFile = new NLog.Targets.Wrappers.AsyncTargetWrapper(logFile)
            {
                OverflowAction = NLog.Targets.Wrappers.AsyncTargetWrapperOverflowAction.Discard,
                QueueLimit = 10000,
                BatchSize = 100,
                TimeToSleepBetweenBatches = 10
            };

            // CONSOLE TARGET for real-time feedback
            var logConsole = new ColoredConsoleTarget("logConsole")
            {
                Layout = "${time} | ${level:uppercase=true:padding=-5} | [${scopeproperty:NodeId}] ${logger:shortName=true} | ${message}"
            };

            // RULES - Global default
            config.AddRule(LogLevel.Info, LogLevel.Fatal, asyncFile);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logConsole);

            if (verboseTrace)
            {
                // Enable deep tracing for network and replication logic
                config.AddRule(LogLevel.Trace, LogLevel.Fatal, asyncFile, "ModuleHost.Network.*");
                config.AddRule(LogLevel.Trace, LogLevel.Fatal, asyncFile, "FDP.Toolkit.Replication.*");
                config.AddRule(LogLevel.Debug, LogLevel.Fatal, asyncFile, "Fdp.Examples.NetworkDemo.*");
            }
            else
            {
                // Suppress noisy kernel logs in normal development mode
                config.AddRule(LogLevel.Warn, LogLevel.Fatal, asyncFile, "FDP.Kernel.*");
            }

            LogManager.Configuration = config;
        }

        /// <summary>
        /// Configure logging for testing with in-memory capture.
        /// Call this from test fixtures, not production code.
        /// </summary>
        /// <param name="testName">Test name for log file identification</param>
        public static void ConfigureForTesting(string testName)
        {
            var config = new LoggingConfiguration();

            // FILE TARGET for test logs
            var logFile = new FileTarget("testLogFile")
            {
                FileName = $"test-logs/{testName}.log",
                Layout = "[${scopeproperty:NodeId}] ${logger:shortName} | ${message}${exception:format=tostring}",
                KeepFileOpen = false,
                AutoFlush = true
            };

            // Enable trace for all modules in tests
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logFile);

            LogManager.Configuration = config;
        }

        /// <summary>
        /// Configure logging for production with optimized performance.
        /// Only logs warnings and errors by default.
        /// </summary>
        /// <param name="nodeId">The node identifier for log file naming</param>
        public static void ConfigureForProduction(int nodeId)
        {
            var config = new LoggingConfiguration();

            // FILE TARGET with compact layout
            var logFile = new FileTarget("logFile")
            {
                FileName = $"logs/node_{nodeId}.log",
                Layout = "${longdate}|${level}|${logger:shortName}|${message}${exception:format=toString}",
                KeepFileOpen = true,
                ConcurrentWrites = false,
                AutoFlush = false,
                ArchiveAboveSize = 52428800, // 50MB
                MaxArchiveFiles = 10,
                ArchiveNumbering = ArchiveNumberingMode.Rolling
            };

            // ASYNC WRAPPER with larger buffer for production
            var asyncFile = new NLog.Targets.Wrappers.AsyncTargetWrapper(logFile)
            {
                OverflowAction = NLog.Targets.Wrappers.AsyncTargetWrapperOverflowAction.Discard,
                QueueLimit = 50000,
                BatchSize = 200,
                TimeToSleepBetweenBatches = 50
            };

            // RULES - Only warnings and errors in production
            config.AddRule(LogLevel.Warn, LogLevel.Fatal, asyncFile);

            LogManager.Configuration = config;
        }

        /// <summary>
        /// Shuts down logging system gracefully, flushing pending messages.
        /// Call this at application shutdown.
        /// </summary>
        public static void Shutdown()
        {
            LogManager.Shutdown();
        }
    }
}
