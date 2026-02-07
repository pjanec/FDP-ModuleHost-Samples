using Xunit;

// Disable parallel test execution because DistributedTestEnv modifies global static state (LogManager)
[assembly: CollectionBehavior(DisableTestParallelization = true)]
