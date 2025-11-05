using QuantumCore.API.Game;
using QuantumCore.API.Game.World;
using QuantumCore.Game.Extensions;

namespace QuantumCore.Game.Commands;

[Command("quit", "Quit the game")]
[CommandNoPermission]
public class QuitCommand(IWorld world) : ICommandHandler
{
    public Task ExecuteAsync(CommandContext context)
    {
        context.Player.StartCountdownEventCancellable(
            "End the game. Please wait.",
            "{0} seconds until quit.",
            () => Task.Run(async () =>
            {
                context.Player.SendChatCommand("quit");
                await context.Player.CalculatePlayedTimeAsync();
                await world.DespawnPlayerAsync(context.Player);
                context.Player.Disconnect();
            }));
        
        return Task.CompletedTask;
    }
}
