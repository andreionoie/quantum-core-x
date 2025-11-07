namespace QuantumCore.Core.Event;

/// <summary>
/// Event registry for managing multiple events with a single field.
/// Each entity owns one EventRegistry instance instead of multiple EventHandle fields.
/// </summary>
public class EventRegistry
{
    // Maps event keys to event IDs for this specific entity
    private readonly Dictionary<object, long> _events = new();

    /// <summary>
    /// Schedule an event by key. Automatically cancels any existing event with the same key.
    /// </summary>
    /// <param name="key">Unique key for this event (typically an enum value)</param>
    /// <param name="callback">Action to execute when event fires</param>
    /// <param name="delay">Delay before event fires</param>
    public void Schedule(object key, Action callback, TimeSpan delay)
    {
        Cancel(key); // Auto-cancel existing

        var eventId = EventSystem.EnqueueEvent(() =>
        {
            callback();
            _events.Remove(key); // Self-cleanup
            return 0; // One-shot
        }, (int)delay.TotalMilliseconds);

        _events[key] = eventId;
    }

    /// <summary>
    /// Schedule a repeating event by key.
    /// Callback returns the next delay as TimeSpan, or TimeSpan.Zero to stop.
    /// </summary>
    public void ScheduleRepeating(object key, Func<TimeSpan> callback, TimeSpan initialDelay)
    {
        Cancel(key);

        var eventId = EventSystem.EnqueueEvent(() =>
        {
            var nextDelay = callback();
            if (nextDelay == TimeSpan.Zero)
            {
                _events.Remove(key);
                return 0; // Stop
            }
            return (int)nextDelay.TotalMilliseconds;
        }, (int)initialDelay.TotalMilliseconds);

        _events[key] = eventId;
    }

    /// <summary>
    /// Cancel a specific event by key.
    /// </summary>
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
    /// Cancel all events. Call this in OnDespawn().
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
    /// Number of currently scheduled events.
    /// </summary>
    public int Count => _events.Count;
}
