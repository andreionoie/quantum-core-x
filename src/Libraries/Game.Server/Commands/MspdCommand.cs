using CommandLine;
using QuantumCore.API.Game;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Affects;
using static QuantumCore.Game.Systems.Tickers.AffectsTickerHelpers;

namespace QuantumCore.Game.Commands;

[Command("mspd", "Change your character move speed")]
public class MspdCommand : ICommandHandler<MspdCommandOptions>
{
    public Task ExecuteAsync(CommandContext<MspdCommandOptions> context)
    {
        context.Player.Affects.Remove(AffectType.From(EAffectType.None), EPoint.MoveSpeed);
        var affect = new EntityAffect
        {
            ModifiedPointId = EPoint.MoveSpeed,
            ModifiedPointDelta = context.Arguments.Value - (int)context.Player.GetPoint(EPoint.MoveSpeed),
            RemainingDuration = EntityAffect.PermanentAffectDurationThreshold,
            DoNotPersist = true
        };

        context.Player.Affects.Upsert(affect);

        return Task.CompletedTask;
    }
}

public class MspdCommandOptions
{
    [Value(0, Required = true)] public byte Value { get; set; }
}
