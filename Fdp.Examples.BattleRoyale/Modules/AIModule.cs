namespace Fdp.Examples.BattleRoyale.Modules;

using Fdp.Kernel;
using Fdp.Examples.BattleRoyale.Components;
using ModuleHost.Core;
using ModuleHost.Core.Abstractions;
using System.Numerics;

public class AIModule : IModule
{
    public string Name => "AI";
    public ModuleTier Tier => ModuleTier.Slow;
    public int UpdateFrequency => 6; // 10 Hz
    
    public IEnumerable<Type> GetRequiredComponents()
    {
        yield return typeof(Position);
        yield return typeof(AIState);
        yield return typeof(Health);
        yield return typeof(Velocity);
        yield return typeof(Damage); 
    }
    
    public void Tick(ISimulationView view, float deltaTime)
    {
        var cmd = view.GetCommandBuffer();
        
        // Get bots
        var bots = view.Query().With<Position>().With<AIState>().Build();
        
        // Get all players (for targeting)
        var players = view.Query().With<Position>().With<Health>().Build();
        
        int decisionsCount = 0;
        int projectilesSpawned = 0;
        
        foreach (var bot in bots)
        {
            ref readonly var botPos = ref view.GetComponentRO<Position>(bot);
            ref readonly var aiState = ref view.GetComponentRO<AIState>(bot);
            
            // Find nearest player
            Entity target = Entity.Null;
            float minDist = float.MaxValue;
            
            foreach (var player in players)
            {
                ref readonly var playerPos = ref view.GetComponentRO<Position>(player);
                float distSq = (playerPos.Value - botPos.Value).LengthSquared();
                
                if (distSq < minDist)
                {
                    minDist = distSq;
                    target = player;
                }
            }
            
            if (target != Entity.Null)
            {
                ref readonly var targetPos = ref view.GetComponentRO<Position>(target);
                
                // Move toward target
                var toTarget = targetPos.Value - botPos.Value;
                float dist = toTarget.Length();
                
                Vector3 n = Vector3.Zero;
                if (dist > 0.001f)
                {
                     n = Vector3.Normalize(toTarget);
                }
                
                if (dist > 0.1f)
                {
                    cmd.SetComponent(bot, new Velocity
                    {
                        Value = n * 5.0f * aiState.AggressionLevel
                    });
                    
                    decisionsCount++;
                }
                
                // Shoot if close enough
                if (dist < 20.0f && aiState.AggressionLevel > 0.5f)
                {
                    // Spawn projectile
                    var proj = cmd.CreateEntity();
                    cmd.AddComponent(proj, new Position { Value = botPos.Value });
                    cmd.AddComponent(proj, new Velocity { Value = n * 30.0f });
                    cmd.AddComponent(proj, new Damage { Amount = 10.0f });
                    
                    projectilesSpawned++;
                }
            }
        }
        
        if (view.Tick % 60 == 0)
            Console.WriteLine($"[AI @ T={view.Time:F1}s] {decisionsCount} decisions, {projectilesSpawned} shots");
    }
}
