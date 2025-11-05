using QuantumCore.API.Core.Models;
using QuantumCore.API.Extensions;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Tickers;
using static QuantumCore.API.Game.Types.Skills.EShamanSkillGroup;
using static QuantumCore.API.Game.Types.Skills.ESuraSkillGroup;
using static QuantumCore.Game.Constants.SchedulingConstants;

namespace QuantumCore.Game.Systems.Tickers;

public sealed class PlayerSpPassiveRestoreTicker(IPlayerEntity player, uint intervalMs = PassiveRegenIntervalMs)
    : GatedTickerEngine<IPlayerEntity>(player, TimeSpan.FromMilliseconds(intervalMs))
{
    private const uint CombatGracePeriodSeconds = 3;
    private const uint MovementGracePeriodSeconds = 3;

    private double _fractionalSpAccumulator;

    // Tunable rates based on current state
    private static (double BasePerSecond, double PercentOfMaxPerSecond) GetRestoreRate(PlayerData player, PlayerState state)
    {
        const double OneThird = 1.0 / 3;

        var isManaHeavyCharacter = player.IsSkillGroup(SuraBlackMagic)
                                || player.IsSkillGroup(ShamanDragon)
                                || player.IsSkillGroup(ShamanHealing);
        if (isManaHeavyCharacter)   // more generous restoration for spellcaster characters
        {
            return state switch
            {
                PlayerState.InCombat =>
                    (BasePerSecond: 2 * OneThird, PercentOfMaxPerSecond: OneThird),
                PlayerState.Moving =>
                    (BasePerSecond: 3 * OneThird, PercentOfMaxPerSecond: 2 * OneThird),
                PlayerState.Idle or PlayerState.IdleHpNotFull =>
                    (BasePerSecond: 10 * OneThird, PercentOfMaxPerSecond: 3 * OneThird),

                _ => throw new ArgumentOutOfRangeException(nameof(state))
            };
        }

        return state switch
        {
            PlayerState.InCombat =>
                (BasePerSecond: 2 * OneThird, PercentOfMaxPerSecond: 0.5 * OneThird),
            PlayerState.Moving or PlayerState.IdleHpNotFull =>
                (BasePerSecond: 2 * OneThird, PercentOfMaxPerSecond: OneThird),
            PlayerState.Idle =>
                (BasePerSecond: 9 * OneThird, PercentOfMaxPerSecond: OneThird),

            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
    }

    protected override bool UpdateState(IPlayerEntity player, TimeSpan processed)
    {
        if (player.Dead)
        {
            _fractionalSpAccumulator = 0;
            return false;
        }

        if (player.GetPoint(EPoint.Sp) >= player.GetPoint(EPoint.MaxSp))
        {
            // Already fully restored
            _fractionalSpAccumulator = 0;
            return false;
        }

        PlayerState state;
        if (player.MsSinceLastAttacked() < CombatGracePeriodSeconds * 1000)
        {
            state = PlayerState.InCombat;
        }
        else if (GameServer.Instance.ServerTime - player.Mobility.LastMovementAt < MovementGracePeriodSeconds * 1000)
        {
            state = PlayerState.Moving;
        }
        else
        {
            state = player.GetPoint(EPoint.Hp) < player.GetPoint(EPoint.MaxHp)
                ? PlayerState.IdleHpNotFull
                : PlayerState.Idle;
        }

        var restoreRate = GetRestoreRate(player.Player, state);
        var restorePerSecond =
            restoreRate.BasePerSecond + player.GetPoint(EPoint.MaxSp) * (restoreRate.PercentOfMaxPerSecond / 100);

        // bonus restore from points
        if (player.GetPoint(EPoint.SpRegen) > 0)
        {
            var bonus = restorePerSecond * (player.GetPoint(EPoint.SpRegen) / 100.0);
            restorePerSecond += bonus;
        }

        if (restorePerSecond <= 0)
        {
            _fractionalSpAccumulator = 0;
            return false;
        }

        // scale the amount to the interval passed from last tick
        _fractionalSpAccumulator += restorePerSecond * processed.TotalSeconds;

        // Only change points when at least one entire unit of SP has accumulated
        var integralSpAmount = (int)_fractionalSpAccumulator;
        if (integralSpAmount <= 0)
        {
            return false;
        }

        _fractionalSpAccumulator -= integralSpAmount;
        player.AddPoint(EPoint.Sp, integralSpAmount);
        return true;
    }

    private enum PlayerState
    {
        Idle,
        IdleHpNotFull,
        Moving,
        InCombat
    }
}
