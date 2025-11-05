using QuantumCore.API.Game;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Affects;
using static QuantumCore.Game.Systems.Tickers.AffectsTickerHelpers;

namespace QuantumCore.Game.Commands;

[Command("invisible", "Toggles invisibility affect on the player")]
public class InvisibleCommand : ICommandHandler
{
    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Player.Affects.RemoveAllOfType(AffectType.From(EAffectType.Invisibility)))
        {
            context.Player.SendCharacterUpdate();
            context.Player.SendPoints();
            context.Player.SendChatInfo("Invisibility removed.");
            
            return Task.CompletedTask;
        }

        var invisAffect = new EntityAffect
        {
            AffectType = AffectType.From(EAffectType.Invisibility),
            AffectFlag = EAffect.Invisibility,
            RemainingDuration = EntityAffect.PermanentAffectDurationThreshold,
            DoNotPersist = true
        };
            
        context.Player.Affects.Upsert(invisAffect);
        context.Player.SendChatInfo("Invisibility activated.");

        return Task.CompletedTask;
    }
}
