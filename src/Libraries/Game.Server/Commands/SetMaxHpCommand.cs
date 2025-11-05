using CommandLine;
using QuantumCore.API.Game;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Affects;
using static QuantumCore.Game.Systems.Tickers.AffectsTickerHelpers;

namespace QuantumCore.Game.Commands;

[Command("set_maxhp", "Set max hp temporarily")]
public class SetMaxHpCommand : ICommandHandler<SetMaxHpCommandOptions>
{
    public Task ExecuteAsync(CommandContext<SetMaxHpCommandOptions> context)
    {
        context.Player.Affects.Remove(AffectType.From(EAffectType.None), EPoint.MaxHp);
        var affect = new EntityAffect
        {
            ModifiedPointId = EPoint.MaxHp,
            ModifiedPointDelta = (int)context.Arguments.Value - (int)context.Player.GetPoint(EPoint.MaxHp),
            RemainingDuration = EntityAffect.PermanentAffectDurationThreshold,
            DoNotPersist = true
        };

        context.Player.Affects.Upsert(affect);

        var newMax = context.Player.GetPoint(EPoint.MaxHp);
        context.Player.Health = Math.Min(context.Player.Health, newMax);
        context.Player.SendPoints();

        return Task.CompletedTask;
    }
}

public class SetMaxHpCommandOptions
{
    [Value(0, Required = true)] public uint Value { get; set; }
}
