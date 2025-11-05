using QuantumCore.API.Game;
using QuantumCore.API.Game.World;
using QuantumCore.Caching;
using QuantumCore.Game.Extensions;

namespace QuantumCore.Game.Commands;

[Command("logout", "Logout from the game")]
[CommandNoPermission]
public class LogoutCommand(IWorld world, ICacheManager cacheManager) : ICommandHandler
{
    public Task ExecuteAsync(CommandContext context)
    {
        context.Player.StartCountdownEventCancellable(
            "Logging out. Please wait.",
            "{0} seconds until logout.",
            () => Task.Run(async () =>
            {
                await context.Player.CalculatePlayedTimeAsync();
                await world.DespawnPlayerAsync(context.Player);
                await cacheManager.Del("account:token:" + context.Player.Player.AccountId);
                context.Player.Disconnect();
            })
        );

        return Task.CompletedTask;
    }
}
