using QuantumCore.API.Game.World;
using QuantumCore.Core.Event;
using QuantumCore.Game.Constants;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.World.Events;

/// <summary>
/// Extension methods for standard entity events.
/// Hides implementation details (delays, callbacks, event keys) from entity classes.
/// Similar to ticker pattern - entity just calls the method, implementation is encapsulated.
/// </summary>
public static class EntityEventExtensions
{
    /// <summary>
    /// Schedule knockout-to-death transition.
    /// Entity enters knockout state, then dies after delay if not healed.
    /// Reference: char_battle.cpp StunEvent pattern
    /// </summary>
    public static void ScheduleKnockoutToDeath(this Entity entity)
    {
        entity.Events.Schedule(
            EntityEventType.KnockoutToDeath,
            () => entity.Die(),
            SchedulingConstants.KnockoutToDeathDelaySeconds * 1000
        );
    }

    /// <summary>
    /// Cancel knockout-to-death if entity is healed.
    /// </summary>
    public static bool CancelKnockoutToDeath(this Entity entity)
    {
        return entity.Events.Cancel(EntityEventType.KnockoutToDeath);
    }

    /// <summary>
    /// Check if entity is knocked out (waiting to die).
    /// </summary>
    public static bool IsKnockedOut(this Entity entity)
    {
        return entity.Events.IsScheduled(EntityEventType.KnockoutToDeath);
    }

    /// <summary>
    /// Schedule auto-respawn in town after player death.
    /// Reference: char.cpp respawn logic
    /// </summary>
    public static void ScheduleAutoRespawn(this PlayerEntity player)
    {
        player.Events.Schedule(
            EntityEventType.AutoRespawnInTown,
            () => player.Respawn(true),
            SchedulingConstants.PlayerAutoRespawnDelaySeconds * 1000
        );
    }

    /// <summary>
    /// Cancel auto-respawn (e.g., player manually respawned).
    /// </summary>
    public static bool CancelAutoRespawn(this PlayerEntity player)
    {
        return player.Events.Cancel(EntityEventType.AutoRespawnInTown);
    }

    /// <summary>
    /// Schedule logout countdown with per-second notifications.
    /// Reference: cmd_general.cpp timed_event
    /// </summary>
    /// <param name="player">Player entity</param>
    /// <param name="seconds">Countdown duration (3 for idle, 10 for combat)</param>
    public static void ScheduleLogoutCountdown(this PlayerEntity player, int seconds)
    {
        int remaining = seconds;

        player.SendChatInfo($"Logging out in {seconds} seconds...");

        player.Events.ScheduleRepeating(
            EntityEventType.LogoutCountdown,
            () =>
            {
                if (remaining <= 0)
                {
                    player.Connection.Close();
                    return 0; // Stop
                }

                player.SendChatInfo($"{remaining} seconds remaining...");
                remaining--;
                return 1000; // Continue every second
            },
            1000
        );
    }

    /// <summary>
    /// Schedule quit countdown (returns to character select).
    /// </summary>
    public static void ScheduleQuitCountdown(this PlayerEntity player, int seconds)
    {
        int remaining = seconds;

        player.SendChatInfo($"Quitting in {seconds} seconds...");

        player.Events.ScheduleRepeating(
            EntityEventType.LogoutCountdown, // Reuse same event type
            () =>
            {
                if (remaining <= 0)
                {
                    player.SendChatCommand("quit");
                    return 0;
                }

                player.SendChatInfo($"{remaining} seconds remaining...");
                remaining--;
                return 1000;
            },
            1000
        );
    }

    /// <summary>
    /// Cancel logout/quit countdown.
    /// </summary>
    public static bool CancelLogoutCountdown(this PlayerEntity player)
    {
        if (player.Events.Cancel(EntityEventType.LogoutCountdown))
        {
            player.SendChatInfo("Logout cancelled.");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Check if logout countdown is active.
    /// </summary>
    public static bool IsLoggingOut(this PlayerEntity player)
    {
        return player.Events.IsScheduled(EntityEventType.LogoutCountdown);
    }

    /// <summary>
    /// Schedule stun duration.
    /// Reference: char_battle.cpp m_pkStunEvent
    /// </summary>
    public static void ScheduleStunDuration(this Entity entity, int durationSeconds = 3)
    {
        entity.Events.Schedule(
            EntityEventType.StunDuration,
            () => { /* Stun ends naturally */ },
            durationSeconds * 1000
        );
    }

    /// <summary>
    /// Check if entity is stunned.
    /// </summary>
    public static bool IsStunned(this Entity entity)
    {
        return entity.Events.IsScheduled(EntityEventType.StunDuration);
    }
}
