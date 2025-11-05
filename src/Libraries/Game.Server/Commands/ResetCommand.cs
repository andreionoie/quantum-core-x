using QuantumCore.API.Game;
using QuantumCore.API.Game.Types;

namespace QuantumCore.Game.Commands;

[Command("r", "Resets the players hp + sp to their max")]
[Command("reset", "Resets the players hp + sp to their max")]
public class ResetCommand : ICommandHandler
{
    public Task ExecuteAsync(CommandContext context)
    {
        context.Player.Health = context.Player.GetPoint(EPoint.MaxHp);
        context.Player.Mana = context.Player.GetPoint(EPoint.MaxSp);
        context.Player.SendPoints();

        return Task.CompletedTask;
    }
}
