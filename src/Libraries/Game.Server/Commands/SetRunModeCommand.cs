using QuantumCore.API.Game;
using QuantumCore.API.Systems.Mobility;

namespace QuantumCore.Game.Commands;

[Command("set_run_mode", "Switch to running mode and set preference")]
[CommandNoPermission]
public sealed class SetRunModeCommand : ICommandHandler
{
    public Task ExecuteAsync(CommandContext context)
    {
        context.Player.Mobility.SetPreferredMode(EMobilityMode.Run, GameServer.Instance.ServerTime);
        return Task.CompletedTask;
    }
}
