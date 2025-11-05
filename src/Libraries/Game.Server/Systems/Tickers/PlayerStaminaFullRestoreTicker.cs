using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Tickers;
using static QuantumCore.API.Game.Types.EPoint;
using static QuantumCore.Game.Constants.SchedulingConstants;

namespace QuantumCore.Game.Systems.Tickers;

public sealed class PlayerStaminaFullRestoreTicker(IPlayerEntity player, uint intervalMs = PassiveRegenIntervalMs)
    : GatedTickerEngine<IPlayerEntity>(player, TimeSpan.FromMilliseconds(intervalMs))
{
    private const uint StopRestoreDelaySeconds = 3;
    private const uint WalkRestoreDelaySeconds = 5;

    protected override bool UpdateState(IPlayerEntity player, TimeSpan processed)
    {
        if (player.Dead)
        {
            return false;
        }

        if (player.GetPoint(Stamina) >= player.GetPoint(MaxStamina))
        {
            return false;
        }

        var now = GameServer.Instance.ServerTime;

        if (player is { State: EEntityState.Moving, Mobility.IsCurrentlyWalking: true })
        {
            if (now - player.Mobility.LastWalkStartAt < WalkRestoreDelaySeconds * 1000)
            {
                return false;
            }
        }
        else if (now - player.Mobility.LastMovementAt < StopRestoreDelaySeconds * 1000)
        {
            return false;
        }

        player.SetPoint(Stamina, player.GetPoint(MaxStamina));
        
        return true;
    }
}
