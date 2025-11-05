using CommandLine;
using QuantumCore.API.Game;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Affects;
using static QuantumCore.Game.Systems.Tickers.AffectsTickerHelpers;

namespace QuantumCore.Game.Commands;

[Command("set_maxsp", "Set max sp temporarily")]
public class SetMaxSpCommand : ICommandHandler<SetMaxSpCommandOptions>
{
    public Task ExecuteAsync(CommandContext<SetMaxSpCommandOptions> context)
    {
        context.Player.Affects.Remove(AffectType.From(EAffectType.None), EPoint.MaxSp);
        var affect = new EntityAffect
        {
            ModifiedPointId = EPoint.MaxSp,
            ModifiedPointDelta = (int)context.Arguments.Value - (int)context.Player.GetPoint(EPoint.MaxSp),
            RemainingDuration = EntityAffect.PermanentAffectDurationThreshold,
            DoNotPersist = true
        };

        context.Player.Affects.Upsert(affect);

        var newMax = context.Player.GetPoint(EPoint.MaxSp);
        context.Player.Mana = Math.Min(context.Player.Mana, newMax);
        context.Player.SendPoints();

        return Task.CompletedTask;
    }
}

public class SetMaxSpCommandOptions
{
    [Value(0, Required = true)] public uint Value { get; set; }
}
