namespace QuantumCore.Core.Event;

/// <summary>
/// A handle for managing a single scheduled event with automatic cancellation support.
/// Provides reference-server-like ergonomics for one-shot events without maintaining duplicate state.
/// Each handle stores only a nullable event ID - no dictionary overhead.
/// </summary>
public class EventHandle
{
    private long? _eventId;

    /// <summary>
    /// Whether an event is currently scheduled.
    /// Note: This only tracks whether we scheduled an event. The event may have already fired
    /// in EventSystem. Use this for quick checks like "can't move while knocked out".
    /// </summary>
    public bool IsScheduled => _eventId.HasValue;

    /// <summary>
    /// Schedules a new event, automatically cancelling any previously scheduled event.
    /// </summary>
    /// <param name="callback">The callback to execute when the event fires. Return 0 to cancel, or a timeout in ms to reschedule.</param>
    /// <param name="timeout">Initial timeout in milliseconds</param>
    public void Schedule(Func<int> callback, int timeout)
    {
        Cancel();

        _eventId = EventSystem.EnqueueEvent(() =>
        {
            var result = callback();
            if (result == 0)
            {
                _eventId = null; // Self-cleanup for one-shot events
            }
            return result;
        }, timeout);
    }

    /// <summary>
    /// Schedules a one-shot event (convenience method).
    /// </summary>
    public void Schedule(Action callback, int timeout)
    {
        Schedule(() =>
        {
            callback();
            return 0; // One-shot
        }, timeout);
    }

    /// <summary>
    /// Cancels the currently scheduled event if any.
    /// </summary>
    /// <returns>True if an event was scheduled (doesn't guarantee it was in EventSystem)</returns>
    public bool Cancel()
    {
        if (_eventId.HasValue)
        {
            EventSystem.CancelEvent(_eventId.Value);
            _eventId = null;
            return true;
        }
        return false;
    }
}
