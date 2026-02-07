using System;
using System.Threading.Tasks;
using Fdp.Examples.NetworkDemo.Configuration;

namespace Fdp.Examples.NetworkDemo;

class Program
{
    static async Task Main(string[] args)
    {
        // Parse arguments
        int instanceId = args.Length > 0 ? int.Parse(args[0]) : 100;
        string modeArg = args.Length > 1 ? args[1].ToLower() : "live";
        string recordingPath = args.Length > 2 ? args[2] : $"node_{instanceId}.fdp";
        
        bool isReplay = modeArg == "replay";
        
        LogSetup.ConfigureForDevelopment(instanceId);
        
        using var app = new NetworkDemoApp();
        await app.InitializeAsync(instanceId, isReplay, recordingPath, autoSpawn: true);
        
        Console.WriteLine("==========================================");
        Console.WriteLine("           Values Running...              ");
        Console.WriteLine("==========================================");
        
        var cts = new System.Threading.CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
        
        try 
        {
            await app.RunLoopAsync(cts.Token);
        }
        catch (Exception ex)
        {
             Console.WriteLine($"[Error] {ex.Message}");
             Console.WriteLine(ex.StackTrace);
        }
    }
}
