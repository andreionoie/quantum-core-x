using QuantumCore.API.Game;

namespace QuantumCore.Game.Commands;

[Command("restart_town", "Respawns in town")]
[CommandNoPermission]
public class RestartTownCommand : ICommandHandler
{
    public Task ExecuteAsync(CommandContext context)
    {
        // TODO: minimum wait time after death 6-7 seconds
        context.Player.Respawn(true);
        return Task.CompletedTask;
    }
}
