# FDP Distributed Logging and Testing - Task Details

**Reference:** See [LOGGING-AND-TESTING-DESIGN.md](./LOGGING-AND-TESTING-DESIGN.md) for architectural design and component specifications.

---

## Phase 1: Logging Foundation

### FDPLT-001: Add NLog Dependencies

**Description:**  
Add NLog NuGet packages to all relevant projects that will use logging.

**Affected Projects:**
- `ModuleHost/FDP.Kernel/FDP.Kernel.csproj`
- `Fdp.Examples.NetworkDemo/Fdp.Examples.NetworkDemo.csproj`
- `ModuleHost.Network.Cyclone/ModuleHost.Network.Cyclone.csproj`
- `FDP.Toolkit.Replication/FDP.Toolkit.Replication.csproj`

**Actions:**
1. Add `<PackageReference Include="NLog" Version="5.2.8" />` to each project file
2. Verify packages restore successfully
3. Run `dotnet build` to confirm no conflicts

**Success Criteria:**
- ✅ All projects build successfully with NLog reference
- ✅ No version conflicts or warnings
- ✅ `NLog.dll` appears in output folders

**Verification:**
```powershell
dotnet list ModuleHost/FDP.Kernel/FDP.Kernel.csproj package | Select-String "NLog"
dotnet build ModuleHost/FDP.Kernel/FDP.Kernel.csproj
```

---

### FDPLT-002: Implement FdpLog Facade

**Description:**  
Create the high-performance static logging facade with generic type parameter for automatic logger naming and zero-allocation hot path support.

**File:** `ModuleHost/FDP.Kernel/Logging/FdpLog.cs`

**Implementation Requirements:**
1. Static generic class `FdpLog<T>` 
2. Private static field `_logger` initialized via `LogManager.GetLogger(typeof(T).FullName)`
3. Public boolean properties: `IsTraceEnabled`, `IsDebugEnabled`, `IsInfoEnabled`, `IsWarnEnabled`
4. Methods: `Trace()`, `Debug()`, `Info()`, `Warn()`, `Error()`
5. All methods marked with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
6. Guard checks inside each method (e.g., `if (!_logger.IsDebugEnabled) return;`)
7. Error method accepts optional `Exception` parameter

**Code Structure:**
```csharp
namespace FDP.Kernel.Logging
{
    public static class FdpLog<T>
    {
        private static readonly Logger _logger = LogManager.GetLogger(typeof(T).FullName);
        
        public static bool IsDebugEnabled => _logger.IsDebugEnabled;
        // ... other flags
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Debug(string message)
        {
            if (_logger.IsDebugEnabled) _logger.Debug(message);
        }
        // ... other methods
    }
}
```

**Success Criteria:**
- ✅ Class compiles without errors
- ✅ Can call `FdpLog<MyClass>.Info("test")` from any project
- ✅ Logger name matches full type name (verify via NLog internal target)
- ✅ Zero allocation when logging disabled (manual inspection of IL or benchmark)

**Unit Test:**
```csharp
[Fact]
public void FdpLog_Uses_Correct_Logger_Name()
{
    // Trigger logger creation
    FdpLog<FdpLogTests>.Info("Test");
    
    // Verify NLog has logger with full type name
    var logger = LogManager.Configuration.AllTargets;
    // ... assertion
}
```

---

### FDPLT-003: Implement LogSetup Configuration

**Description:**  
Create centralized NLog configuration module with support for different execution contexts (development, testing, production).

**File:** `Fdp.Examples.NetworkDemo/Configuration/LogSetup.cs`

**Implementation Requirements:**
1. Static class `LogSetup`
2. Method: `ConfigureForDevelopment(int nodeId, bool verboseTrace = false)`
3. Method: `ConfigureForTesting(string testName)`
4. Method: `ConfigureForProduction(int nodeId)`
5. Each method creates `LoggingConfiguration` programmatically
6. File target with layout: `${longdate}|${level}|Node-${scopeproperty:NodeId}|${logger:shortName}|${message}`
7. Async wrapper with `OverflowAction = Discard`, `QueueLimit = 10000`
8. Rule-based filtering:
   - Default: Info+
   - Verbose: Trace for `ModuleHost.Network.*`, `FDP.Toolkit.Replication.*`
   - Production: Warn+ for `Fdp.Kernel.*`

**Code Structure:**
```csharp
public static class LogSetup
{
    public static void ConfigureForDevelopment(int nodeId, bool verboseTrace = false)
    {
        var config = new LoggingConfiguration();
        
        var logFile = new FileTarget("logFile")
        {
            FileName = $"logs/node_{nodeId}.log",
            Layout = "...",
            KeepFileOpen = true,
            AutoFlush = false
        };
        
        var asyncFile = new AsyncTargetWrapper(logFile)
        {
            OverflowAction = AsyncTargetWrapperOverflowAction.Discard,
            QueueLimit = 10000
        };
        
        config.AddRule(LogLevel.Info, LogLevel.Fatal, asyncFile);
        if (verboseTrace)
        {
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, asyncFile, "ModuleHost.Network.*");
        }
        
        LogManager.Configuration = config;
    }
}
```

**Success Criteria:**
- ✅ Can call `LogSetup.ConfigureForDevelopment(100)` at startup
- ✅ Log file created at `logs/node_100.log`
- ✅ Verbose mode enables trace for network modules only
- ✅ Production mode suppresses kernel noise

**Integration Test:**
```csharp
[Fact]
public void LogSetup_Creates_NodeSpecific_LogFile()
{
    LogSetup.ConfigureForDevelopment(100);
    FdpLog<LogSetupTests>.Info("Test message");
    
    Assert.True(File.Exists("logs/node_100.log"));
    var content = File.ReadAllText("logs/node_100.log");
    Assert.Contains("Test message", content);
}
```

---

### FDPLT-004: Add Scope Context to NetworkDemoApp

**Description:**  
Wrap the entire execution of `NetworkDemoApp.Start()` with `ScopeContext.PushProperty("NodeId", ...)` to enable automatic node identification in logs.

**File:** `Fdp.Examples.NetworkDemo/NetworkDemoApp.cs`

**Implementation Requirements:**
1. Modify `Start(int nodeId, ...)` method
2. Wrap entire method body in `using (ScopeContext.PushProperty("NodeId", nodeId)) { ... }`
3. Ensure scope remains active throughout initialization and run loop
4. Add initial log statement to verify context: `FdpLog<NetworkDemoApp>.Info($"Starting Node {nodeId}");`

**Code Changes:**
```csharp
public async Task Start(int nodeId, bool replayMode, string recPath = null)
{
    using (NLog.ScopeContext.PushProperty("NodeId", nodeId))
    {
        FdpLog<NetworkDemoApp>.Info($"Starting Node {nodeId}...");
        
        // ... existing initialization code ...
        
        await RunLoopAsync(cancellationToken);
    }
}
```

**Success Criteria:**
- ✅ All logs from `NetworkDemoApp` and nested calls show correct NodeId
- ✅ Context flows across `await` boundaries
- ✅ Context flows into systems and modules initialized by the app
- ✅ Different nodes write to different log files when running concurrently

**Verification:**
Run two instances:
```powershell
# Terminal 1
dotnet run --project Fdp.Examples.NetworkDemo -- --node 100

# Terminal 2
dotnet run --project Fdp.Examples.NetworkDemo -- --node 200
```

Check `logs/node_100.log` and `logs/node_200.log` contain only their respective node's logs.

---

### FDPLT-005: Replace Console.WriteLine in CycloneNetworkModule

**Description:**  
Replace all `Console.WriteLine` calls with appropriate `FdpLog<T>` calls in the Cyclone network module.

**File:** `ModuleHost.Network.Cyclone/CycloneNetworkModule.cs`

**Locations to Update:**
1. Constructor - initialization messages → `Info`
2. Translator registration loop → `Info` or `Debug`
3. Error handling → `Error`
4. Any verbose debug output → `Debug` with guard check

**Example Changes:**
```csharp
// Before
Console.WriteLine($"Registered {_customTranslators.Count} translators");

// After
FdpLog<CycloneNetworkModule>.Info($"Registered {_customTranslators.Count} translators");

// For verbose output
if (FdpLog<CycloneNetworkModule>.IsDebugEnabled)
{
    foreach (var t in _customTranslators)
        FdpLog<CycloneNetworkModule>.Debug($"  Translator: {t.TopicName} (ID: {t.DescriptorOrdinal})");
}
```

**Success Criteria:**
- ✅ No `Console.WriteLine` remaining in file
- ✅ All messages appear in log file during execution
- ✅ Appropriate log levels used (Info for lifecycle, Debug for details, Error for failures)
- ✅ Verbose output guarded by `IsDebugEnabled` check

**Verification:**
```csharp
grep -n "Console.WriteLine" ModuleHost.Network.Cyclone/CycloneNetworkModule.cs
# Should return no results
```

---

### FDPLT-006: Replace Console.WriteLine in GenericDescriptorTranslator

**Description:**  
Add comprehensive logging to the descriptor translator to diagnose replication issues.

**File:** `FDP.Toolkit.Replication/Translators/GenericDescriptorTranslator.cs`

**Key Logging Points:**
1. **ScanAndPublish** - Entry point
   - Trace: Query entity count
   - Trace: Per-entity authority check
   - Debug: Successful publish
2. **PollIngress** - Reception point
   - Trace: Sample received
   - Debug: Ghost created/updated
   - Warn: Invalid or orphaned data
3. **Error conditions** - Authority mismatches, missing components

**Example Implementation:**
```csharp
public void ScanAndPublish(...)
{
    if (FdpLog<GenericDescriptorTranslator<T>>.IsTraceEnabled)
    {
        FdpLog<GenericDescriptorTranslator<T>>.Trace(
            $"Scanning {typeof(T).Name}, Query has {query.Count()} entities");
    }
    
    foreach (var entity in query)
    {
        bool hasAuth = view.HasAuthority(entity, DescriptorOrdinal);
        
        if (FdpLog<GenericDescriptorTranslator<T>>.IsTraceEnabled)
            FdpLog<GenericDescriptorTranslator<T>>.Trace(
                $"Entity {entity.Index}: HasAuth({DescriptorOrdinal}) = {hasAuth}");
        
        if (hasAuth)
        {
            // ... write packet ...
            if (FdpLog<GenericDescriptorTranslator<T>>.IsDebugEnabled)
                FdpLog<GenericDescriptorTranslator<T>>.Debug(
                    $"Published {typeof(T).Name} for entity {entity.Index}");
        }
    }
}
```

**Success Criteria:**
- ✅ Can diagnose "Remote: 0" issue from logs
- ✅ Trace logs show every authority decision
- ✅ Debug logs show every successful publish/receive
- ✅ No performance impact when trace disabled

**Diagnostic Test:**
Create entity on Node A, verify log sequence:
- Node A: `Trace: HasAuth(5) = True`
- Node A: `Debug: Published descriptor for entity 65536`
- Node B: `Trace: Sample received for entity 65536`
- Node B: `Debug: Created ghost for entity 65536`

---

### FDPLT-007: Replace Console.WriteLine in Network Systems

**Description:**  
Update `CycloneNetworkEgressSystem` and `CycloneNetworkIngressSystem` with comprehensive logging.

**Files:**
- `ModuleHost.Network.Cyclone/Systems/CycloneNetworkEgressSystem.cs`
- `ModuleHost.Network.Cyclone/Systems/CycloneNetworkIngressSystem.cs`

**Egress System Logging:**
1. System initialization → `Info`
2. Per-translator scan start → `Trace`
3. Publish count summary → `Debug`

**Ingress System Logging:**
1. System initialization → `Info`
2. Per-translator poll start → `Trace`
3. Sample received count → `Debug`
4. Discovery events → `Info`

**Success Criteria:**
- ✅ Can trace complete egress flow from entity update to packet send
- ✅ Can trace complete ingress flow from packet receive to component update
- ✅ No Console.WriteLine remaining
- ✅ Performance impact < 1% when logging disabled

---

### FDPLT-008: Refactor NetworkDemoApp for Testability

**Description:**  
Split `Start()` method into separate initialization and run loop to support deterministic testing.

**File:** `Fdp.Examples.NetworkDemo/NetworkDemoApp.cs`

**Required Changes:**
1. Rename `Start` → `InitializeAsync` (removes the while loop)
2. Add new method `Update(float deltaTime)` - single frame update
3. Add new method `RunLoopAsync(CancellationToken ct)` - continuous execution
4. Modify `Program.cs` to call all three methods

**New Structure:**
```csharp
public async Task InitializeAsync(int nodeId, bool replayMode, string recPath = null)
{
    using (ScopeContext.PushProperty("NodeId", nodeId))
    {
        // All initialization code
        // NO while loop
    }
}

public void Update(float deltaTime)
{
    _world.Update(deltaTime);
}

public async Task RunLoopAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        Update(0.016f);
        await Task.Delay(16);
    }
}
```

**Program.cs Update:**
```csharp
var app = new NetworkDemoApp();
await app.InitializeAsync(nodeId, replayMode, recPath);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => { cts.Cancel(); e.Cancel = true; };

await app.RunLoopAsync(cts.Token);
```

**Success Criteria:**
- ✅ Can call `InitializeAsync` without starting game loop
- ✅ Can call `Update()` manually for exact frame control
- ✅ Interactive app still works normally with `RunLoopAsync`
- ✅ Tests can run N frames deterministically

**Unit Test:**
```csharp
[Fact]
public async Task Can_Run_Single_Frame_Update()
{
    var app = new NetworkDemoApp();
    await app.InitializeAsync(100, false);
    
    // Should not throw or hang
    app.Update(0.016f);
    app.Update(0.016f);
}
```

---

## Phase 2: Test Infrastructure

### FDPLT-009: Create Test Project

**Description:**  
Create new xUnit test project for distributed system testing.

**Actions:**
1. Create project: `Fdp.Examples.NetworkDemo.Tests/Fdp.Examples.NetworkDemo.Tests.csproj`
2. Add dependencies:
   - `Microsoft.NET.Test.Sdk`
   - `xunit`
   - `xunit.runner.visualstudio`
   - `NLog`
   - Project reference to `Fdp.Examples.NetworkDemo`
3. Create folder structure:
   - `Framework/` - test infrastructure
   - `Scenarios/` - actual test cases
4. Add to `Samples.sln`

**Project File:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
    <PackageReference Include="NLog" Version="5.2.8" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Fdp.Examples.NetworkDemo\Fdp.Examples.NetworkDemo.csproj" />
  </ItemGroup>
</Project>
```

**Success Criteria:**
- ✅ Project builds successfully
- ✅ Can run empty test with `dotnet test`
- ✅ Shows up in Visual Studio Test Explorer
- ✅ Can reference NetworkDemoApp types

---

### FDPLT-010: Implement TestLogCapture

**Description:**  
Create in-memory NLog target for capturing logs during tests without file I/O.

**File:** `Fdp.Examples.NetworkDemo.Tests/Framework/TestLogCapture.cs`

**Implementation:**
```csharp
using NLog;
using NLog.Targets;
using System.Collections.Concurrent;

namespace Fdp.Examples.NetworkDemo.Tests.Framework
{
    [Target("TestMemory")]
    public class TestLogCapture : TargetWithLayout
    {
        public ConcurrentQueue<string> Logs { get; } = new();

        protected override void Write(LogEventInfo logEvent)
        {
            string message = Layout.Render(logEvent);
            Logs.Enqueue(message);
        }

        public bool Contains(string substring)
        {
            return Logs.Any(log => log.Contains(substring));
        }

        public void Clear() => Logs.Clear();
        
        public string[] GetLogsForNode(int nodeId)
        {
            string prefix = $"[{nodeId}]";
            return Logs.Where(l => l.StartsWith(prefix)).ToArray();
        }
    }
}
```

**Success Criteria:**
- ✅ Inherits from `TargetWithLayout`
- ✅ Thread-safe log storage
- ✅ Helper methods for querying
- ✅ Can be added to NLog configuration

**Unit Test:**
```csharp
[Fact]
public void TestLogCapture_Stores_Messages()
{
    var capture = new TestLogCapture 
    { 
        Layout = "${message}" 
    };
    
    var config = new LoggingConfiguration();
    config.AddRule(LogLevel.Debug, LogLevel.Fatal, capture);
    LogManager.Configuration = config;
    
    FdpLog<TestLogCaptureTests>.Info("Test message");
    
    Assert.True(capture.Contains("Test message"));
}
```

---

### FDPLT-011: Implement DistributedTestEnv

**Description:**  
Create test orchestration class that manages multiple node instances.

**File:** `Fdp.Examples.NetworkDemo.Tests/Framework/DistributedTestEnv.cs`

**Implementation:**
```csharp
public class DistributedTestEnv : IDisposable
{
    public NetworkDemoApp NodeA { get; private set; }
    public NetworkDemoApp NodeB { get; private set; }
    
    private Task _taskA;
    private Task _taskB;
    private CancellationTokenSource _cts;
    private readonly ITestOutputHelper _output;
    private readonly TestLogCapture _logCapture;

    public DistributedTestEnv(ITestOutputHelper output)
    {
        _output = output;
        _cts = new CancellationTokenSource();
        
        // Setup in-memory logging
        _logCapture = new TestLogCapture 
        { 
            Layout = "[${scopeproperty:NodeId}] ${logger:shortName} | ${message}" 
        };
        
        var config = new LoggingConfiguration();
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, _logCapture);
        LogManager.Configuration = config;
    }

    public async Task StartNodesAsync()
    {
        NodeA = new NetworkDemoApp();
        NodeB = new NetworkDemoApp();

        _taskA = Task.Run(async () => {
            using (ScopeContext.PushProperty("NodeId", 100))
            {
                await NodeA.InitializeAsync(100, false);
            }
        });

        _taskB = Task.Run(async () => {
            await Task.Delay(100); // Stagger start
            using (ScopeContext.PushProperty("NodeId", 200))
            {
                await NodeB.InitializeAsync(200, false);
            }
        });

        await Task.WhenAll(_taskA, _taskB);
    }

    public async Task RunFrames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            NodeA?.Update(0.016f);
            NodeB?.Update(0.016f);
            await Task.Delay(16);
        }
    }

    public async Task WaitForCondition(
        Func<NetworkDemoApp, bool> condition, 
        NetworkDemoApp target, 
        int timeoutMs = 5000)
    {
        int elapsed = 0;
        while (elapsed < timeoutMs)
        {
            if (condition(target)) return;
            await Task.Delay(100);
            elapsed += 100;
        }
        throw new TimeoutException($"Condition not met within {timeoutMs}ms");
    }

    public void AssertLogContains(int nodeId, string message)
    {
        var logs = _logCapture.GetLogsForNode(nodeId);
        if (!logs.Any(l => l.Contains(message)))
            throw new Xunit.Sdk.XunitException(
                $"Node {nodeId} logs did not contain: '{message}'");
    }

    public void Dispose()
    {
        _cts.Cancel();
        NodeA?.Dispose();
        NodeB?.Dispose();
        LogManager.Shutdown();
    }
}
```

**Success Criteria:**
- ✅ Can start two nodes concurrently
- ✅ Logs isolated by NodeId
- ✅ Timeout-based condition waiting
- ✅ Log assertion helpers work
- ✅ Proper cleanup on dispose

---

## Phase 3: Test Cases

### FDPLT-012: Test - AsyncLocal Scope Verification

**Description:**  
Verify that NLog scope context flows correctly across Task.Run and await boundaries.

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/InfrastructureTests.cs`

**Implementation:**
```csharp
public class InfrastructureTests : IDisposable
{
    private readonly TestLogCapture _logCapture;

    public InfrastructureTests()
    {
        _logCapture = new TestLogCapture 
        { 
            Layout = "[${scopeproperty:NodeId}] ${message}" 
        };
        
        var config = new LoggingConfiguration();
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, _logCapture);
        LogManager.Configuration = config;
    }

    [Fact]
    public async Task Logging_Scope_Flows_Through_Async_And_Tasks()
    {
        var task1 = Task.Run(async () => {
            using (ScopeContext.PushProperty("NodeId", 100))
            {
                FdpLog<InfrastructureTests>.Info("Start Task 1");
                await SimulateDeepLibraryCall();
                FdpLog<InfrastructureTests>.Info("End Task 1");
            }
        });

        var task2 = Task.Run(async () => {
            using (ScopeContext.PushProperty("NodeId", 200))
            {
                FdpLog<InfrastructureTests>.Info("Start Task 2");
                await Task.Delay(5);
                await SimulateDeepLibraryCall();
                FdpLog<InfrastructureTests>.Info("End Task 2");
            }
        });

        await Task.WhenAll(task1, task2);

        // Verify all logs from Task 1 have NodeId 100
        var logs = _logCapture.Logs.ToArray();
        foreach (var log in logs)
        {
            if (log.Contains("Task 1"))
                Assert.StartsWith("[100]", log);
            else if (log.Contains("Task 2"))
                Assert.StartsWith("[200]", log);
        }
        
        // Verify deep calls maintain context
        Assert.Contains(logs, l => l == "[100] Deep library call");
        Assert.Contains(logs, l => l == "[200] Deep library call");
    }

    private async Task SimulateDeepLibraryCall()
    {
        await Task.Yield(); // Force thread switch
        FdpLog<InfrastructureTests>.Info("Deep library call");
    }

    public void Dispose() => LogManager.Shutdown();
}
```

**Success Criteria:**
- ✅ Test passes reliably
- ✅ No mixed/missing NodeIds in logs
- ✅ Context preserved across Task.Run
- ✅ Context preserved across await

---

### FDPLT-013: Test - Basic Entity Replication

**Description:**  
Verify entities created on Node A appear on Node B.

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/ReplicationTests.cs`

**Implementation:**
```csharp
[Fact]
public async Task Entity_Created_On_NodeA_Appears_On_NodeB()
{
    using var env = new DistributedTestEnv(_output);
    await env.StartNodesAsync();
    
    // Wait for DDS discovery
    await Task.Delay(2000);
    
    // Spawn tank on Node A
    var tankA = env.NodeA.SpawnTank();
    long netId = env.NodeA.GetNetworkId(tankA);
    
    _output.WriteLine($"Spawned tank with NetId {netId} on Node A");
    
    // Wait for replication
    await env.WaitForCondition(
        app => app.TryGetEntityByNetId(netId, out _), 
        env.NodeB, 
        timeoutMs: 3000);
    
    // Verify entity exists on Node B
    var tankB = env.NodeB.GetEntityByNetId(netId);
    Assert.NotEqual(Entity.Null, tankB);
    
    // Verify logs
    env.AssertLogContains(100, "Published descriptor");
    env.AssertLogContains(200, "Created ghost");
}
```

**Success Criteria:**
- ✅ Test completes in < 5 seconds
- ✅ Entity appears on remote node
- ✅ NetworkIdentity matches
- ✅ Logs show publish and receive events

---

### FDPLT-014: Test - Entity Lifecycle

**Description:**  
Verify complete entity lifecycle: creation, activation, destruction.

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/LifecycleTests.cs`

**Implementation:**
```csharp
[Fact]
public async Task Entity_Lifecycle_Creation_Activation_Destruction()
{
    using var env = new DistributedTestEnv(_output);
    await env.StartNodesAsync();
    await Task.Delay(2000);
    
    // 1. CREATION
    var tankA = env.NodeA.SpawnTank();
    long netId = env.NodeA.GetNetworkId(tankA);
    
    // 2. GHOST DETECTION
    await env.WaitForCondition(
        app => app.TryGetEntityByNetId(netId, out _),
        env.NodeB);
    
    var tankB = env.NodeB.GetEntityByNetId(netId);
    
    // 3. ACTIVATION (wait for mandatory data)
    await env.WaitForCondition(
        app => app.World.GetLifecycleState(tankB) == EntityLifecycle.Active,
        env.NodeB,
        timeoutMs: 3000);
    
    Assert.Equal(EntityLifecycle.Active, env.NodeB.World.GetLifecycleState(tankB));
    
    // 4. DESTRUCTION
    env.NodeA.World.DestroyEntity(tankA);
    await env.RunFrames(10);
    
    // 5. VERIFY DELETION PROPAGATED
    await env.WaitForCondition(
        app => !app.World.IsAlive(tankB),
        env.NodeB);
    
    Assert.False(env.NodeB.World.IsAlive(tankB));
}
```

**Success Criteria:**
- ✅ Ghost created before activation
- ✅ Activation happens only after mandatory data
- ✅ Destruction propagates within 200ms
- ✅ Logs show all lifecycle transitions

---

### FDPLT-015: Test - Orphan Protection

**Description:**  
Verify ghost remains inactive when mandatory descriptor is missing.

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/GhostProtocolTests.cs`

**Implementation:**
```csharp
[Fact]
public async Task Ghost_Stays_Inactive_Without_Mandatory_Data()
{
    using var env = new DistributedTestEnv(_output);
    
    // Configure Node A to NOT send Chassis descriptor (ID 5)
    // This requires adding a filter mechanism to egress system
    env.ConfigureEgressFilter(100, descriptorId => descriptorId != 5);
    
    await env.StartNodesAsync();
    await Task.Delay(2000);
    
    // Spawn tank (Master sent, Chassis blocked)
    var tankA = env.NodeA.SpawnTank();
    long netId = env.NodeA.GetNetworkId(tankA);
    
    // Wait for ghost
    await env.WaitForCondition(
        app => app.TryGetEntityByNetId(netId, out _),
        env.NodeB);
    
    var ghostB = env.NodeB.GetEntityByNetId(netId);
    
    // Run several frames
    await env.RunFrames(20);
    
    // Ghost should NOT be Active
    var state = env.NodeB.World.GetLifecycleState(ghostB);
    Assert.NotEqual(EntityLifecycle.Active, state);
    
    // Unblock Chassis
    env.ConfigureEgressFilter(100, null);
    
    // Now ghost should activate
    await env.WaitForCondition(
        app => app.World.GetLifecycleState(ghostB) == EntityLifecycle.Active,
        env.NodeB);
}
```

**Success Criteria:**
- ✅ Ghost created in Constructing state
- ✅ Remains inactive while data missing
- ✅ Activates immediately when data arrives
- ✅ Logs show "Waiting for mandatory descriptor"

---

### FDPLT-016: Test - Partial Ownership

**Description:**  
Verify split control doesn't cause data overwrites.

**File:** `Fdp.Examples.NetworkDemo.Tests/Scenarios/OwnershipTests.cs`

**Implementation:**
```csharp
[Fact]
public async Task Partial_Ownership_No_Overwrites()
{
    using var env = new DistributedTestEnv(_output);
    await env.StartNodesAsync();
    await Task.Delay(2000);
    
    // Setup: A owns Chassis, B owns Turret
    var tankA = env.NodeA.SpawnTank();
    long netId = env.NodeA.GetNetworkId(tankA);
    
    await env.WaitForCondition(
        app => app.TryGetEntityByNetId(netId, out _),
        env.NodeB);
    
    var tankB = env.NodeB.GetEntityByNetId(netId);
    
    // Transfer Turret ownership to B
    env.NodeB.RequestOwnership(tankB, DescriptorType.Turret);
    await env.RunFrames(5);
    
    // A updates Position
    var newPos = new Vector3(100, 0, 0);
    env.NodeA.SetPosition(tankA, newPos);
    
    // B updates Turret
    float newYaw = 45f;
    env.NodeB.SetTurretYaw(tankB, newYaw);
    
    await env.RunFrames(10);
    
    // Verify A has B's Turret, keeps its Position
    var turretA = env.NodeA.GetTurretYaw(tankA);
    var posA = env.NodeA.GetPosition(tankA);
    Assert.Equal(newYaw, turretA, 0.1f);
    Assert.Equal(newPos.X, posA.X, 0.1f);
    
    // Verify B has A's Position, keeps its Turret
    var turretB = env.NodeB.GetTurretYaw(tankB);
    var posB = env.NodeB.GetPosition(tankB);
    Assert.Equal(newYaw, turretB, 0.1f);
    Assert.Equal(newPos.X, posB.X, 0.1f);
    
    // Verify logs show egress skipping
    env.AssertLogContains(100, "Skipped Turret (Not Owner)");
    env.AssertLogContains(200, "Skipped Chassis (Not Owner)");
}
```

**Success Criteria:**
- ✅ Each node keeps its owned data
- ✅ Each node receives remote data
- ✅ No overwrites occur
- ✅ Logs show skipped descriptors

---

### FDPLT-017: Additional Test Cases

**Description:**  
Implement remaining test cases for comprehensive coverage.

**Tests to Implement:**
1. **Dynamic Ownership Transfer** - Runtime authority handoff
2. **Sub-Entity Hierarchy Cleanup** - Child destruction with parent
3. **Component Synchronization** - Data updates propagate correctly
4. **Deterministic Time Mode Switch** - Distributed barrier protocol
5. **Distributed Replay** - Replay triggers network egress

**Each test follows same pattern:**
- Setup environment
- Execute actions
- Verify state changes
- Assert log messages
- Cleanup

**Success Criteria:**
- ✅ All 10+ tests pass
- ✅ Test suite runs in < 60 seconds
- ✅ Tests are deterministic (no flakiness)
- ✅ Clear failure messages

---

## Phase 4: Documentation

### FDPLT-018: Update ONBOARDING.md

**Description:**  
Add section about logging and testing infrastructure to onboarding document.

**File:** `ONBOARDING.md`

**Content to Add:**
```markdown
## Logging Infrastructure

The project uses NLog for high-performance logging with AsyncLocal context flow.

### Basic Usage
```csharp
using FDP.Kernel.Logging;

// In your class
if (FdpLog<MyClass>.IsDebugEnabled)
    FdpLog<MyClass>.Debug($"Important state: {value}");

FdpLog<MyClass>.Info("Lifecycle event");
FdpLog<MyClass>.Error("Critical failure", exception);
```

### Configuration
See `Fdp.Examples.NetworkDemo/Configuration/LogSetup.cs` for setup options.

Logs are written to `logs/node_{nodeId}.log` with automatic node identification.

## Testing

### Running Tests
```powershell
dotnet test Fdp.Examples.NetworkDemo.Tests
```

### Writing New Tests
See `Fdp.Examples.NetworkDemo.Tests/Scenarios/` for examples.

Use `DistributedTestEnv` to orchestrate multi-node scenarios.

## Troubleshooting
- **No logs appearing**: Check LogSetup.Configure() is called at startup
- **Missing NodeId in logs**: Verify ScopeContext.PushProperty() wraps execution
- **Test timeouts**: Increase WaitForCondition timeout or check DDS discovery
```

**Success Criteria:**
- ✅ Section integrated into existing document
- ✅ Clear instructions for new developers
- ✅ Examples are accurate and tested
- ✅ Links to relevant files

---

## Success Verification Checklist

### Logging Infrastructure
- [ ] NLog packages installed in all projects
- [ ] FdpLog facade compiles and works
- [ ] LogSetup creates node-specific log files
- [ ] AsyncLocal context flows across Tasks/awaits
- [ ] No Console.WriteLine in production code
- [ ] Zero allocation when logging disabled
- [ ] Granular module filtering works

### Test Infrastructure
- [ ] Test project builds and runs
- [ ] DistributedTestEnv orchestrates multiple nodes
- [ ] TestLogCapture intercepts logs
- [ ] Can verify both state and logs in tests
- [ ] Timeout-based waiting works reliably

### Test Coverage
- [ ] AsyncLocal scope test passes
- [ ] Basic replication test passes
- [ ] Entity lifecycle test passes
- [ ] Orphan protection test passes
- [ ] Partial ownership test passes
- [ ] All 10+ tests pass reliably
- [ ] Test execution < 60 seconds

### Documentation
- [ ] Design document complete
- [ ] Task details documented
- [ ] ONBOARDING.md updated
- [ ] Examples tested and accurate

### Integration
- [ ] Interactive NetworkDemo still works
- [ ] Logs appear during normal execution
- [ ] Can diagnose "Remote: 0" from logs
- [ ] Tests run in CI/CD pipeline
