using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Mobility;
using QuantumCore.API.Systems.Tickers;
using QuantumCore.Game.Constants;
using static QuantumCore.API.Game.Types.EPoint;
using static QuantumCore.Game.Constants.SchedulingConstants;

namespace QuantumCore.Game.Systems.Tickers;

public sealed class PlayerStaminaConsumptionTicker(IPlayerEntity player, uint intervalMs = AffectsUpdateIntervalMs)
    : GatedTickerEngine<IPlayerEntity>(player, TimeSpan.FromMilliseconds(intervalMs))
{
    private const double RunningStaminaDrainPerSecond = 25.0;
    private const uint MaxConsumeWindowAfterAttackSeconds = 20;
    
    private double _fractionalStaminaAccumulator;

    private bool _isConsumingIdempotencyFlag;

    protected override bool UpdateState(IPlayerEntity player, TimeSpan processed)
    {
        var beforeUpdateStamina = player.GetPoint(Stamina);
        if (player.Dead)
        {
            StopClientConsumptionIdempotent(player, beforeUpdateStamina);
            return false;
        }

        if (beforeUpdateStamina == 0)
        {
            StopClientConsumptionIdempotent(player, beforeUpdateStamina);
            player.Mobility.ForceWalk(GameServer.Instance.ServerTime);
            return false;
        }

        var stateIsMoving = player.State == EEntityState.Moving;
        var activeModeRunning = player.Mobility.ActiveMode == EMobilityMode.Run;
        var attackedRecently = player.MsSinceLastAttacked() < MaxConsumeWindowAfterAttackSeconds * 1000;
        if (!stateIsMoving || player.Mobility.IsForcedToWalk || !activeModeRunning || !attackedRecently)
        {
            StopClientConsumptionIdempotent(player, beforeUpdateStamina);
            return false;
        }

        var perSecond = RunningStaminaDrainPerSecond;
        if (HasHalfRateStaminaItem(player))
        {
            perSecond *= 0.5;
        }
        
        StartClientConsumptionIdempotent(player, (uint)Math.Round(perSecond), beforeUpdateStamina);

        var computedCost = perSecond * processed.TotalSeconds;
        
        _fractionalStaminaAccumulator += computedCost;
        var staminaToConsume = (int)_fractionalStaminaAccumulator;
        if (staminaToConsume <= 0)
        {
            return false;
        }

        player.AddPoint(Stamina, -staminaToConsume);
        _fractionalStaminaAccumulator -= staminaToConsume;

        if (player.GetPoint(Stamina) == 0)
        {
            StopClientConsumptionIdempotent(player, player.GetPoint(Stamina));
            player.Mobility.ForceWalk(GameServer.Instance.ServerTime);
        }

        return true;
    }

    private void StartClientConsumptionIdempotent(IPlayerEntity player, uint perSecondRate, uint stamina)
    {
        if (_isConsumingIdempotencyFlag)
        {
            return;
        }
        _isConsumingIdempotencyFlag = true;
        
        player.SendChatCommand($"StartStaminaConsume {perSecondRate} {stamina}");
    }

    private void StopClientConsumptionIdempotent(IPlayerEntity player, uint stamina)
    {
        if (!_isConsumingIdempotencyFlag)
        {
            return;
        }
        _isConsumingIdempotencyFlag = false;
        
        player.SendChatCommand($"StopStaminaConsume {stamina}");
    }

    private static bool HasHalfRateStaminaItem(IPlayerEntity playerEntity)
    {
        return playerEntity.HasUniqueItemEquipped(ItemConstants.OrcStubbornnessId);
    }
}
