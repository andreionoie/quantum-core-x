using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Affects;
using QuantumCore.API.Systems.Tickers;
using QuantumCore.Game.World.Entities;
using static QuantumCore.Game.Constants.SchedulingConstants;

namespace QuantumCore.Game.Systems.Tickers;

public sealed class PlayerHpPassiveRestoreTicker(IPlayerEntity player, uint intervalMs = PassiveRegenIntervalMs)
    : GatedTickerEngine<IPlayerEntity>(player, TimeSpan.FromMilliseconds(intervalMs))
{
    private const uint MovedRecentlyThresholdSeconds = 3;

    // Tunable rates
    private const uint BaseRestorePerSecond = 5;
    private const double PercentageRestorePerSecond = 1.666;
    private const double PercentageRestorePerSecondWhenMoving = PercentageRestorePerSecond / 5;

    private double _fractionalHpAccumulator;
    
    protected override bool UpdateState(IPlayerEntity player, TimeSpan processed)
    {
        var flags = player.Affects.GetActiveFlags();
        // Do not regen while dead, knocked down (hp==0 during stun-to-death), or under poison
        if (player.Dead || player.GetPoint(EPoint.Hp) == 0 || flags.Has(EAffect.Poison))
        {
            _fractionalHpAccumulator = 0;
            return false;
        }

        if (player.GetPoint(EPoint.Hp) >= player.GetPoint(EPoint.MaxHp))
        {
            // Already fully restored
            _fractionalHpAccumulator = 0;
            return false;
        }

        double percentageRestore;
        if (GameServer.Instance.ServerTime - player.Mobility.LastMovementAt < MovedRecentlyThresholdSeconds * 1000)
        {
            percentageRestore = PercentageRestorePerSecondWhenMoving;
        }
        else
        {
            percentageRestore = PercentageRestorePerSecond;
        }
        
        var restorePerSecond = 
            BaseRestorePerSecond + player.GetPoint(EPoint.MaxHp) * (percentageRestore / 100);
        
        // bonus restore from items/buffs
        if (player.GetPoint(EPoint.HpRegen) > 0)
        {
           var bonus = restorePerSecond * (player.GetPoint(EPoint.HpRegen) / 100.0);
           restorePerSecond += bonus;
        }
        
        if (restorePerSecond <= 0)
        {
            _fractionalHpAccumulator = 0;
            return false;
        }

        // scale the amount to the interval passed from last tick
        _fractionalHpAccumulator += restorePerSecond * processed.TotalSeconds;

        // Only change points when at least one entire unit of HP has accumulated
        var integralHpAmount = (int)_fractionalHpAccumulator;
        if (integralHpAmount <= 0)
        {
            return false;
        }

        _fractionalHpAccumulator -= integralHpAmount;
        player.AddPoint(EPoint.Hp, integralHpAmount);
        return true;
    }
}
