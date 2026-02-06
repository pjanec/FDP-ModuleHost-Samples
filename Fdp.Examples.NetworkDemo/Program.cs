using System;
using System.Threading.Tasks;

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
        
        using var app = new NetworkDemoApp();
        await app.Start(instanceId, isReplay, recordingPath);
        
        Console.WriteLine("==========================================");
        Console.WriteLine("           Values Running...              ");
        Console.WriteLine("==========================================");
        
        var cts = new System.Threading.CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
        
        int frameCount = 0;
        
        try 
        {
            while (!cts.Token.IsCancellationRequested)
            {
                // Core loop extracted to app.Update
                app.Update(0.1f);
                
                if (frameCount % 60 == 0) // Less frequent printing
                {
                    app.PrintStatus();
                }
                
                await Task.Delay(33); // ~30Hz loop
                frameCount++;
            }
        }
        catch (Exception ex)
        {
             Console.WriteLine($"[Error] {ex.Message}");
             Console.WriteLine(ex.StackTrace);
        }
    }
}
