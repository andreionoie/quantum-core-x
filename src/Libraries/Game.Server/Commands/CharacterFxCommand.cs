using CommandLine;
using QuantumCore.API.Game;
using QuantumCore.API.Game.Types;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.Commands;

[Command("effect", "Triggers a special visual effect on your character.")]
public class CharacterFxCommand : ICommandHandler<CharacterFxCommandOptions>
{
    public Task ExecuteAsync(CommandContext<CharacterFxCommandOptions> context)
    {
        var player = context.Player;

        if (player is not PlayerEntity playerEntity)
        {
            return Task.CompletedTask;
        }

        try
        {
            var effectType = EnumUtils<ECharacterFx>.CheckedCast(context.Arguments.EffectId);
            playerEntity.BroadcastCharacterFx(effectType);
            player.SendChatInfo($"Triggered effect: {effectType} (id={context.Arguments.EffectId}).");
        }
        catch (Exception e)
        {
            player.SendChatInfo(e.Message);
        }

        return Task.CompletedTask;
    }
}

public class CharacterFxCommandOptions
{
    [Value(0, Required = true, HelpText = "The numeric ID of the special effect to display.")] public int EffectId { get; set; }
}
