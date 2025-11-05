using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Tickers;
using static QuantumCore.API.Game.Types.EPoint;
using static QuantumCore.Game.Constants.SchedulingConstants;

namespace QuantumCore.Game.Systems.Tickers;

public sealed class PlayerSpRecoveryTicker(IPlayerEntity player, uint intervalMs = AffectsUpdateIntervalMs)
    : GatedTickerEngine<IPlayerEntity>(player, TimeSpan.FromMilliseconds(intervalMs))
{
    private const uint RecoverMaxPercentagePerSecond = 7; // valid values: 0 - 100
    
    protected override bool UpdateState(IPlayerEntity player, TimeSpan processed)
    {
        var hpChanged = false;
        if (player.GetPoint(SpRecovery) > 0)
        {
            if (player.GetPoint(Sp) < player.GetPoint(MaxSp))
            {
                // Recover with defined capped percentage
                var maxRecoverable =
                    player.GetPoint(MaxSp) * RecoverMaxPercentagePerSecond / 100.0 * processed.TotalSeconds;
                var toRecover = Math.Clamp(player.GetPoint(SpRecovery), 0, maxRecoverable);
                if (toRecover > 0)
                {
                    player.AddPoint(Sp, (int)toRecover);
                    player.AddPoint(SpRecovery, -(int)toRecover);
                    hpChanged = true;
                }
            }
            else
            {
                // Clear recovery bucket if fully recovered
                player.SetPoint(SpRecovery, 0);
            }
        }

        if (player.GetPoint(SpRecoverContinue) > 0)
        {
            if (player.GetPoint(Sp) < player.GetPoint(MaxSp))
            {
                var toRecoverPerSecond = Math.Clamp(player.GetPoint(SpRecoverContinue), 0, player.GetPoint(MaxSp)); 
                var toRecover = toRecoverPerSecond * processed.TotalSeconds;
                if (toRecover > 0)
                {
                    player.AddPoint(Sp, (int)toRecover);
                    hpChanged = true;
                }
            }
        }

        // TODO: implement the auto HP recover item with capacity stored in socket

        return hpChanged;
    }
}

