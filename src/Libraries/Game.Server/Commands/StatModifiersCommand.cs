using CommandLine;
using QuantumCore.API.Game;
using QuantumCore.API.Game.World;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.Commands;

[Command("stat_modifiers", "Shows stat modifiers for the player")]
public class StatModifiersCommand : ICommandHandler<StatModifiersCommandOptions>
{
    private readonly IWorld _world;

    public StatModifiersCommand(IWorld world)
    {
        _world = world;
    }

    public Task ExecuteAsync(CommandContext<StatModifiersCommandOptions> context)
    {
        var player = context.Player;

        var target = player;
        if (context.Arguments.PlayerName is not null)
        {
            var other = _world.GetPlayer(context.Arguments.PlayerName);
            if (other is null)
            {
                player.SendChatInfo($"Player '{context.Arguments.PlayerName}' not found.");
                return Task.CompletedTask;
            }

            target = other;
        }

        if (target is PlayerEntity targetPlayer)
        {
            var modifiers = targetPlayer.AllStatModifiers;
            foreach (var line in modifiers.Split(Environment.NewLine))
            {
                player.SendChatInfo(line);
            }
        }
        else
        {
            player.SendChatInfo("Target is not a player entity.");
        }

        return Task.CompletedTask;
    }
}

public class StatModifiersCommandOptions
{
    [Value(0, Required = false, HelpText = "Player Name (optional, defaults to self)")]
    public string? PlayerName { get; set; }
}
