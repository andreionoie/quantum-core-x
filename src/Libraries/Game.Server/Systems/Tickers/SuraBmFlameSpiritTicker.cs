using QuantumCore.API.Core.Models;
using QuantumCore.API.Extensions;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Tickers;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.Packets;
using static QuantumCore.API.Game.Types.Skills.ESuraSkillGroup;
using static QuantumCore.API.Game.World.EEntityType;
using static QuantumCore.Game.Constants.SchedulingConstants;

namespace QuantumCore.Game.Systems.Tickers;

// TODO: finish implementation, right now we only send the projectile animation
public sealed class SuraBmFlameSpiritTicker(IPlayerEntity player, uint intervalMs = FlameSpiritHitRateMs)
    : GatedTickerEngine<IPlayerEntity>(player, TimeSpan.FromMilliseconds(intervalMs))
{
    private const uint MaxRange = 1000;
    
    protected override bool UpdateState(IPlayerEntity player, TimeSpan processed)
    {
        if (!player.Player.IsSkillGroup(SuraBlackMagic))
        {
            return false;
        }

        if (!player.Affects.GetActiveFlags().Has(EAffect.FlameSpirit))
        {
            return false;
        }

        if (player.PositionIsAttr(EMapAttribute.NonPvp))
        {
            return false;
        }

        List<IEntity> targetCandidates = [];
        foreach (var nearby in player.NearbyEntities)
        {
            if (nearby == player ||
                nearby.Type is not (Monster or MetinStone or Player or PolymorphPlayer) ||
                nearby.PositionIsAttr(EMapAttribute.NonPvp))
            {
                continue;
            }

            if (MathUtils.Distance(player.PositionX, player.PositionY, nearby.PositionX, nearby.PositionY) < MaxRange)
            {
                targetCandidates.Add(nearby);
            }
        }
        
        if (targetCandidates.Count == 0)
        {
            return false;
        }
        var randomTarget = targetCandidates[CoreRandom.GenerateInt32(0, targetCandidates.Count)];

        var projectileFx = new ProjectileFx
        {
            Source = player.Vid,
            Destination = randomTarget.Vid,
            Type = (byte)EProjectileFx.FlameSpirit
        };
        
        player.Connection.Send(projectileFx);
        foreach (var nearbyPlayer in player.GetNearbyPlayers())
        {
            nearbyPlayer.Connection.Send(projectileFx);
        } 
        
        // TODO: compute and deal damage, make proper implementation

        return false;
    }

    public void ArmFirstTickDelay()
    {
        ResetIntervalAccum();
        ArmInitialDelay(TimeSpan.FromSeconds(1));
    }
}
