using QuantumCore.API.Game;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.Commands;

[Command("do_clear_affect", "Clears all active affects")]
public class ClearAffectCommand : ICommandHandler
{
    public Task ExecuteAsync(CommandContext context)
    {
        context.Player.Affects.Clear();
        
        context.Player.SendChatInfo("All affects cleared.");
        return Task.CompletedTask;
    }
}
