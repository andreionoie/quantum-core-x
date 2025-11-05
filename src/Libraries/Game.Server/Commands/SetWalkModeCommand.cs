using QuantumCore.API.Game;
using QuantumCore.API.Systems.Mobility;

namespace QuantumCore.Game.Commands;

[Command("set_walk_mode", "Switch to walking mode and set preference")]
[CommandNoPermission]
public sealed class SetWalkModeCommand : ICommandHandler
{
    public Task ExecuteAsync(CommandContext context)
    {
        context.Player.Mobility.SetPreferredMode(EMobilityMode.Walk, GameServer.Instance.ServerTime);
        return Task.CompletedTask;
    }
}
