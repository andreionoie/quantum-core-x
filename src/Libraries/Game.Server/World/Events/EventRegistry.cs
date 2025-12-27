using QuantumCore.Core.Event;
using QuantumCore.Game.Constants;

namespace QuantumCore.Game.World.Events;

/// <summary>
/// Manages all one-shot events for an entity or item using a single field.
/// Provides type-safe event scheduling with automatic cleanup.
/// </summary>
public class EventRegistry
{
    private readonly Dictionary<object, long> _events = new();
    private readonly string? _ownerName; // Optional, for debugging

    public EventRegistry(string? ownerName = null)
    {
        _ownerName = ownerName;
    }

    /// <summary>
    /// Schedule an event by key (enum, string, or any object).
    /// Automatically cancels any existing event with the same key.
    /// </summary>
    /// <param name="key">Unique key for this event (typically an enum value)</param>
    /// <param name="callback">Action to execute when event fires</param>
    /// <param name="delayMs">Delay in milliseconds</param>
    public void Schedule(object key, Action callback, int delayMs)
    {
        Cancel(key); // Auto-cancel existing event with same key

        var eventId = EventSystem.EnqueueEvent(() =>
        {
            callback();
            _events.Remove(key); // Self-cleanup
            return 0; // One-shot event
        }, delayMs);

        _events[key] = eventId;
    }

    /// <summary>
    /// Schedule a repeating event by key.
    /// Automatically cancels any existing event with the same key.
    /// </summary>
    /// <param name="key">Unique key for this event</param>
    /// <param name="callback">Function returning next delay in ms (0 to stop)</param>
    /// <param name="delayMs">Initial delay in milliseconds</param>
    public void ScheduleRepeating(object key, Func<int> callback, int delayMs)
    {
        Cancel(key);

        var eventId = EventSystem.EnqueueEvent(() =>
        {
            var nextDelay = callback();
            if (nextDelay == 0)
            {
                _events.Remove(key);
            }
            return nextDelay;
        }, delayMs);

        _events[key] = eventId;
    }

    /// <summary>
    /// Cancel a specific event by key.
    /// </summary>
    /// <returns>True if an event was cancelled</returns>
    public bool Cancel(object key)
    {
        if (_events.TryGetValue(key, out var eventId))
        {
            EventSystem.CancelEvent(eventId);
            _events.Remove(key);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Cancel all events (useful for cleanup on entity destruction).
    /// </summary>
    public void CancelAll()
    {
        foreach (var eventId in _events.Values)
        {
            EventSystem.CancelEvent(eventId);
        }
        _events.Clear();
    }

    /// <summary>
    /// Check if an event is scheduled.
    /// </summary>
    public bool IsScheduled(object key) => _events.ContainsKey(key);

    /// <summary>
    /// Reschedule an event with a new delay (convenience method).
    /// </summary>
    public void Reschedule(object key, Action callback, int delayMs)
    {
        Schedule(key, callback, delayMs);
    }

    /// <summary>
    /// Number of currently scheduled events.
    /// </summary>
    public int Count => _events.Count;

    #region Static Factory Methods - Entity Events

    /// <summary>
    /// Schedule knockout-to-death transition (reference: char_battle.cpp StunEvent pattern).
    /// Entity enters knockout state, then dies after delay if not healed.
    /// </summary>
    public void ScheduleKnockoutToDeath(Action onDeath)
    {
        Schedule(
            EntityEventType.KnockoutToDeath,
            onDeath,
            SchedulingConstants.KnockoutToDeathDelaySeconds * 1000
        );
    }

    /// <summary>
    /// Schedule auto-respawn in town after player death (reference: char.cpp respawn logic).
    /// </summary>
    public void ScheduleAutoRespawnInTown(Action onRespawn)
    {
        Schedule(
            EntityEventType.AutoRespawnInTown,
            onRespawn,
            SchedulingConstants.PlayerAutoRespawnDelaySeconds * 1000
        );
    }

    /// <summary>
    /// Schedule logout/quit countdown (reference: cmd_general.cpp timed_event).
    /// Default: 3 seconds if idle, 10 seconds if in combat.
    /// </summary>
    public void ScheduleLogoutCountdown(Action onComplete, int seconds)
    {
        int remaining = seconds;
        ScheduleRepeating(
            EntityEventType.LogoutCountdown,
            () =>
            {
                if (remaining <= 0)
                {
                    onComplete();
                    return 0; // Stop
                }
                remaining--;
                return 1000; // Continue every second
            },
            1000
        );
    }

    /// <summary>
    /// Cancel logout countdown early (e.g., player moved or attacked).
    /// </summary>
    public bool CancelLogoutCountdown()
    {
        return Cancel(EntityEventType.LogoutCountdown);
    }

    /// <summary>
    /// Schedule stun duration (reference: char_battle.cpp m_pkStunEvent).
    /// Typically 3 seconds.
    /// </summary>
    public void ScheduleStunDuration(Action onStunEnd, int durationSeconds = 3)
    {
        Schedule(
            EntityEventType.StunDuration,
            onStunEnd,
            durationSeconds * 1000
        );
    }

    #endregion

    #region Static Factory Methods - Item Events

    /// <summary>
    /// Schedule item ownership expiry (reference: item ownership system).
    /// After delay, item becomes public and anyone can pick it up.
    /// Default: 30 seconds.
    /// </summary>
    public void ScheduleItemOwnershipExpiry(Action onOwnershipExpired, int seconds = 30)
    {
        Schedule(
            ItemEventType.OwnershipExpiry,
            onOwnershipExpired,
            seconds * 1000
        );
    }

    /// <summary>
    /// Schedule item disappear from ground (reference: item cleanup system).
    /// Default: 5 minutes (300 seconds).
    /// </summary>
    public void ScheduleItemDisappear(Action onDisappear, int seconds = 300)
    {
        Schedule(
            ItemEventType.ItemDisappear,
            onDisappear,
            seconds * 1000
        );
    }

    #endregion

    #region Query Helpers

    /// <summary>
    /// Check if entity is knocked out (waiting to die).
    /// </summary>
    public bool IsKnockedOut => IsScheduled(EntityEventType.KnockoutToDeath);

    /// <summary>
    /// Check if logout countdown is active.
    /// </summary>
    public bool IsLoggingOut => IsScheduled(EntityEventType.LogoutCountdown);

    /// <summary>
    /// Check if entity is stunned.
    /// </summary>
    public bool IsStunned => IsScheduled(EntityEventType.StunDuration);

    /// <summary>
    /// Check if item still has owner protection.
    /// </summary>
    public bool HasOwnerProtection => !IsScheduled(ItemEventType.OwnershipExpiry);

    #endregion
}
