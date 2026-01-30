# Task EXT-6-4: Network Integration Demo (Peer-to-Peer)

**Added to Phase 6** - Validates complete extraction

## Description
Create a comprehensive console demo application that runs as two peer instances, each spawning entities visible to the other. Demonstrates DDS networking, geographic transforms, smoothing, and entity lifecycle. Output is structured for automated testing.

## Objectives
- Validate complete extraction (Core + Cyclone + Geographic modules)
- Show peer-to-peer entity synchronization
- Demonstrate geographic coordinate transforms
- Show network smoothing in action
- Provide testable console output for CI/CD

## Architecture

### Application: `Fdp.Examples.NetworkDemo`

**Two Instances:**
- **Instance A** (Domain 1, AppInstance 100) - "Alpha Node"
- **Instance B** (Domain 1, AppInstance 200) - "Bravo Node"

**Each Instance:**
1. Spawns 3 entities locally (Tank, Jeep, Helicopter)
2. Publishes EntityMaster + EntityState for local entities
3. Receives and renders entities from peer
4. Applies geographic transforms (WGS84 → Local Cartesian)
5. Applies network smoothing
6. Logs structured output for validation

## Implementation

### Step 1: Create Project

```xml
<!-- Fdp.Examples.NetworkDemo/Fdp.Examples.NetworkDemo.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\ModuleHost\ModuleHost.Core\ModuleHost.Core.csproj" />
    <ProjectReference Include="..\..\ModuleHost.Network.Cyclone\ModuleHost.Network.Cyclone.csproj" />
    <ProjectReference Include="..\..\Fdp.Modules.Geographic\Fdp.Modules.Geographic.csproj" />
  </ItemGroup>
</Project>
```

### Step 2: Define Components

```csharp
// Fdp.Examples.NetworkDemo/Components/DemoComponents.cs
using System.Numerics;

namespace Fdp.Examples.NetworkDemo.Components
{
    public struct Position
    {
        public Vector3 LocalCartesian;  // Local coordinates
    }

    public struct PositionGeodetic
    {
        public double Latitude;
        public double Longitude;
        public double Altitude;
    }

    public struct Velocity
    {
        public Vector3 Value;
    }

    public struct EntityType
    {
        public string Name; // "Tank", "Jeep", "Helicopter"
        public int TypeId;  // Corresponds to DIS type
    }

    public struct NetworkedEntity
    {
        public long NetworkId;
        public int OwnerNodeId;
        public bool IsLocallyOwned;
    }
}
```

### Step 3: Create Demo Program

```csharp
// Fdp.Examples.NetworkDemo/Program.cs
using Fdp.Kernel;
using Fdp.Examples.NetworkDemo.Components;
using Fdp.Modules.Geographic;
using ModuleHost.Core;
using ModuleHost.Network.Cyclone;
using CycloneDDS.Runtime;

namespace Fdp.Examples.NetworkDemo;

class Program
{
    static async Task Main(string[] args)
    {
        // Parse instance ID from args
        int instanceId = args.Length > 0 ? int.Parse(args[0]) : 100;
        string nodeName = instanceId == 100 ? "Alpha" : "Bravo";
        
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine($"║  Network Demo - {nodeName} Node (ID: {instanceId})  ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine();
        
        // Setup
        var world = new EntityRepository();
        RegisterComponents(world);
        
        var kernel = new ModuleHostKernel(world);
        
        // Network setup
        var participant = new DdsParticipant(domainId: 0);
        participant.EnableSenderTracking(new SenderIdentityConfig
        {
            AppDomainId = 1,
            AppInstanceId = instanceId
        });
        
        var nodeMapper = new NodeIdMapper(appDomain: 1, appInstance: instanceId);
        var idAllocator = new DdsIdAllocator(participant, $"Node_{instanceId}");
        var topology = new StaticNetworkTopology(localNodeId: instanceId);
        
        var networkModule = new CycloneNetworkModule(
            participant, nodeMapper, idAllocator, topology,
            kernel.GetEntityLifecycleModule()
        );
        kernel.RegisterModule(networkModule);
        
        // Geographic module (origin: Berlin)
        var geoModule = new GeographicModule(
            new WGS84Transform(originLat: 52.52, originLon: 13.405)
        );
        kernel.RegisterModule(geoModule);
        
        kernel.Initialize();
        
        Console.WriteLine("[INIT] Kernel initialized");
        Console.WriteLine("[INIT] Waiting for peer discovery...");
        await Task.Delay(2000);
        
        // Spawn local entities
        SpawnLocalEntities(world, instanceId);
        
        Console.WriteLine($"[SPAWN] Created 3 local entities");
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║           Monitoring Network             ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine();
        
        // Main loop
        int frameCount = 0;
        while (frameCount < 100) // Run for 100 frames (~10 seconds)
        {
            kernel.Tick(deltaTime: 0.1f);
            
            if (frameCount % 10 == 0)
            {
                PrintStatus(world, nodeMapper, instanceId);
            }
            
            await Task.Delay(100);
            frameCount++;
        }
        
        Console.WriteLine();
        Console.WriteLine("[SHUTDOWN] Demo complete");
        
        kernel.Shutdown();
        participant.Dispose();
    }

    static void RegisterComponents(EntityRepository world)
    {
        world.RegisterComponent<Position>();
        world.RegisterComponent<PositionGeodetic>();
        world.RegisterComponent<Velocity>();
        world.RegisterComponent<EntityType>();
        world.RegisterComponent<NetworkedEntity>();
    }

    static void SpawnLocalEntities(EntityRepository world, int instanceId)
    {
        var types = new[] { ("Tank", 1), ("Jeep", 2), ("Helicopter", 3) };
        
        foreach (var (name, typeId) in types)
        {
            var entity = world.CreateEntity();
            
            world.AddComponent(entity, new EntityType { Name = name, TypeId = typeId });
            world.AddComponent(entity, new Position 
            { 
                LocalCartesian = new Vector3(
                    Random.Shared.Next(-1000, 1000),
                    Random.Shared.Next(-1000, 1000),
                    0
                )
            });
            world.AddComponent(entity, new PositionGeodetic
            {
                Latitude = 52.52 + Random.Shared.NextDouble() * 0.01,
                Longitude = 13.405 + Random.Shared.NextDouble() * 0.01,
                Altitude = 0
            });
            world.AddComponent(entity, new Velocity 
            { 
                Value = new Vector3(10, 5, 0) 
            });
        }
    }

    static void PrintStatus(EntityRepository world, NodeIdMapper mapper, int localInstanceId)
    {
        var query = world.Query()
            .With<EntityType>()
            .With<Position>()
            .With<NetworkedEntity>()
            .Build();

        Console.WriteLine($"[STATUS] Frame snapshot:");
        
        int localCount = 0;
        int remoteCount = 0;
        
        query.ForEach((Entity e, ref EntityType type, ref Position pos, ref NetworkedEntity net) =>
        {
            bool isLocal = net.IsLocallyOwned;
            string owner = isLocal ? "LOCAL" : "REMOTE";
            
            if (isLocal) localCount++;
            else remoteCount++;
            
            Console.WriteLine($"  [{owner}] {type.Name,-12} " +
                            $"Pos: ({pos.LocalCartesian.X:F1}, {pos.LocalCartesian.Y:F1}) " +
                            $"NetID: {net.NetworkId} " +
                            $"Owner: {net.OwnerNodeId}");
        });
        
        Console.WriteLine($"[STATUS] Local: {localCount}, Remote: {remoteCount}");
        
        // Testable output marker
        Console.WriteLine($"TEST_OUTPUT: LOCAL={localCount} REMOTE={remoteCount}");
        Console.WriteLine();
    }
}
```

### Step 4: Create Automated Tests

```csharp
// Fdp.Examples.NetworkDemo.Tests/NetworkDemoIntegrationTests.cs
using System.Diagnostics;
using Xunit;

namespace Fdp.Examples.NetworkDemo.Tests;

public class NetworkDemoIntegrationTests
{
    [Fact]
    public async Task TwoInstances_ExchangeEntities_WithinTimeout()
    {
        // Start ID allocator server
        using var serverProcess = StartIdAllocatorServer();
        await Task.Delay(1000);
        
        // Start Instance A (Alpha - ID 100)
        var processA = StartDemoInstance(100);
        
        // Start Instance B (Bravo - ID 200)
        var processB = StartDemoInstance(200);
        
        // Collect output
        var outputA = new List<string>();
        var outputB = new List<string>();
        
        processA.OutputDataReceived += (s, e) => 
        {
            if (e.Data != null) outputA.Add(e.Data);
        };
        processB.OutputDataReceived += (s, e) => 
        {
            if (e.Data != null) outputB.Add(e.Data);
        };
        
        processA.BeginOutputReadLine();
        processB.BeginOutputReadLine();
        
        // Wait for completion
        await Task.WhenAll(
            processA.WaitForExitAsync(),
            processB.WaitForExitAsync()
        );
        
        // Validate: Each instance should see 3 local + 3 remote entities
        var lastStatusA = outputA.LastOrDefault(line => line.StartsWith("TEST_OUTPUT:"));
        var lastStatusB = outputB.LastOrDefault(line => line.StartsWith("TEST_OUTPUT:"));
        
        Assert.NotNull(lastStatusA);
        Assert.NotNull(lastStatusB);
        
        // Parse: "TEST_OUTPUT: LOCAL=3 REMOTE=3"
        Assert.Contains("LOCAL=3", lastStatusA);
        Assert.Contains("REMOTE=3", lastStatusA);
        
        Assert.Contains("LOCAL=3", lastStatusB);
        Assert.Contains("REMOTE=3", lastStatusB);
    }

    [Fact]
    public async Task Demo_AppliesGeographicTransforms()
    {
        var process = StartDemoInstance(100);
        var output = new List<string>();
        
        process.OutputDataReceived += (s, e) => 
        {
            if (e.Data != null) output.Add(e.Data);
        };
        
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();
        
        // Verify geographic transform was applied
        Assert.Contains(output, line => line.Contains("Latitude:") && line.Contains("Longitude:"));
        Assert.Contains(output, line => line.Contains("LocalCartesian"));
    }

    private Process StartDemoInstance(int instanceId)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project Fdp.Examples.NetworkDemo -- {instanceId}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.Start();
        return process;
    }

    private Process StartIdAllocatorServer()
    {
        // Assumes EXT-2-7 ID allocator server is available
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project Fdp.Examples.IdAllocatorDemo",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.Start();
        return process;
    }
}
```

### Step 5: Create README

```markdown
# Network Demo - Peer-to-Peer Entity Sync

## Purpose
Demonstrates complete network extraction architecture:
- Core engine (entity management)
- Cyclone network plugin (DDS)
- Geographic module (coordinate transforms)

## Running Manually

### Terminal 1 (ID Server):
```bash
cd Fdp.Examples.IdAllocatorDemo
dotnet run
```

### Terminal 2 (Alpha Node):
```bash
cd Fdp.Examples.NetworkDemo
dotnet run -- 100
```

### Terminal 3 (Bravo Node):
```bash
cd Fdp.Examples.NetworkDemo
dotnet run -- 200
```

## Expected Output

Each node should see:
- **LOCAL: 3 entities** (spawned locally)
- **REMOTE: 3 entities** (received from peer)

Example:
```
[STATUS] Frame snapshot:
  [LOCAL]  Tank         Pos: (523.1, -234.5) NetID: 1 Owner: 100
  [LOCAL]  Jeep         Pos: (123.4, 567.8) NetID: 2 Owner: 100
  [LOCAL]  Helicopter   Pos: (-45.2, 678.9) NetID: 3 Owner: 100
  [REMOTE] Tank         Pos: (-123.4, 234.5) NetID: 4 Owner: 200
  [REMOTE] Jeep         Pos: (234.5, -123.4) NetID: 5 Owner: 200
  [REMOTE] Helicopter   Pos: (345.6, 456.7) NetID: 6 Owner: 200
[STATUS] Local: 3, Remote: 3
TEST_OUTPUT: LOCAL=3 REMOTE=3
```

## Automated Tests

```bash
cd Fdp.Examples.NetworkDemo.Tests
dotnet test
```

Tests verify:
- ✅ Peer discovery and entity exchange
- ✅ Geographic transforms applied
- ✅ Network smoothing active
- ✅ Structured output for CI/CD validation
```

## Success Criteria
- ✅ Two instances run simultaneously
- ✅ Each spawns 3 local entities
- ✅ Each receives 3 entities from peer (total 6 visible)
- ✅ Geographic transforms applied (WGS84 → Cartesian)
- ✅ Network smoothing demonstrated
- ✅ Console output is structured and testable
- ✅ Automated tests pass in CI/CD
- ✅ Demo validates complete extraction architecture

## Dependencies
- Phase 5 complete (Core simplified)
- Task EXT-2-7 complete (ID Allocator Server)
- Task EXT-6-1 complete (BattleRoyale bootstrap pattern)

## Estimated Duration
1 day

## References
- [EXTRACTION-DESIGN.md § Application Bootstrap](EXTRACTION-DESIGN.md#application-bootstrap-example)
- [EXTRACTION-REFINEMENTS.md § FastCycloneDDS Integration](EXTRACTION-REFINEMENTS.md#5-appendix-fastcyclonedds-integration-notes)

