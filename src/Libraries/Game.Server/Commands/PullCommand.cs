using System.Numerics;
using QuantumCore.API.Game;
using QuantumCore.API.Game.World;
using QuantumCore.Core.Utils;

namespace QuantumCore.Game.Commands;

[Command("pull", "All monsters in range will quickly move towards you")]
[Command("pull_monster", "All monsters in range will quickly move towards you")]
public class PullCommand : ICommandHandler
{
    private const int MaxDistance = 3000;
    private const int MinDistance = 100;
    
    public Task ExecuteAsync(CommandContext context)
    {
        var p = context.Player;
        context.Player.ForEachNearbyEntity(e =>
        {
            if (e.Type == EEntityType.Monster)
            {
                var dist = Vector2.Distance(new Vector2(p.PositionX, p.PositionY),
                    new Vector2(e.PositionX, e.PositionY));
                if (dist is > MaxDistance or < MinDistance)
                    return;

                var degree = MathUtils.Rotation(p.PositionX - e.PositionX, p.PositionY - e.PositionY);

                var direction = MathUtils.GetDeltaByDegree(degree);
                var targetX = p.PositionX - direction.X * MinDistance;
                var targetY = p.PositionY - direction.Y * MinDistance;

                e.Goto((int)targetX, (int)targetY);
            }
        });
        return Task.CompletedTask;
    }
}
