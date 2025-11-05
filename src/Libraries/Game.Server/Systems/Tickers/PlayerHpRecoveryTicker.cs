using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Tickers;
using static QuantumCore.API.Game.Types.EPoint;
using static QuantumCore.Game.Constants.SchedulingConstants;

namespace QuantumCore.Game.Systems.Tickers;

public sealed class PlayerHpRecoveryTicker(IPlayerEntity player, uint intervalMs = AffectsUpdateIntervalMs)
    : GatedTickerEngine<IPlayerEntity>(player, TimeSpan.FromMilliseconds(intervalMs))
{
    private const uint RecoverMaxPercentagePerSecond = 7; // valid values: 0 - 100
    
    protected override bool UpdateState(IPlayerEntity player, TimeSpan processed)
    {
        var hpChanged = false;
        if (player.GetPoint(HpRecovery) > 0)
        {
            if (player.GetPoint(Hp) < player.GetPoint(MaxHp))
            {
                // Recover with defined capped percentage
                var maxRecoverable =
                    player.GetPoint(MaxHp) * RecoverMaxPercentagePerSecond / 100.0 * processed.TotalSeconds;
                var toRecover = Math.Clamp(player.GetPoint(HpRecovery), 0, maxRecoverable);
                if (toRecover > 0)
                {
                    player.AddPoint(Hp, (int)toRecover);
                    player.AddPoint(HpRecovery, -(int)toRecover);
                    hpChanged = true;
                }
            }
            else
            {
                // Clear recovery bucket if fully recovered
                player.SetPoint(HpRecovery, 0);
            }
        }

        if (player.GetPoint(HpRecoverContinue) > 0)
        {
            if (player.GetPoint(Hp) < player.GetPoint(MaxHp))
            {
               var toRecoverPerSecond = Math.Clamp(player.GetPoint(HpRecoverContinue), 0, player.GetPoint(MaxHp)); 
               var toRecover = toRecoverPerSecond * processed.TotalSeconds;
               if (toRecover > 0)
               {
                   player.AddPoint(Hp, (int)toRecover);
                   hpChanged = true;
               }
            }
        }

        // TODO: implement the auto HP recover item with capacity stored in socket

        return hpChanged;
    }
}

