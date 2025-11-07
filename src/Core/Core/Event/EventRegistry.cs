namespace QuantumCore.Core.Event;

/// <summary>
/// Global event registry for managing entity-scoped events without field bloat.
/// Single source of truth: events are stored here, keyed by (entity, eventType).
/// No per-entity dictionaries, no duplicate state with EventSystem.
/// </summary>
public static class EventRegistry
{
    // Key: (entity reference, event key) -> Value: event ID
    private static readonly Dictionary<(object Entity, object EventKey), long> _entityEvents = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Schedule an event for an entity by key.
    /// Automatically cancels any existing event with the same (entity, key) pair.
    /// </summary>
    /// <param name="entity">The owning entity (typically 'this')</param>
    /// <param name="eventKey">Unique key for this event type (typically an enum)</param>
    /// <param name="callback">Callback to execute</param>
    /// <param name="delayMs">Delay in milliseconds</param>
    public static void Schedule(object entity, object eventKey, Action callback, int delayMs)
    {
        var key = (entity, eventKey);

        lock (_lock)
        {
            // Cancel existing event if any
            if (_entityEvents.TryGetValue(key, out var existingEventId))
            {
                EventSystem.CancelEvent(existingEventId);
            }

            // Schedule new event
            var eventId = EventSystem.EnqueueEvent(() =>
            {
                callback();
                lock (_lock)
                {
                    _entityEvents.Remove(key); // Self-cleanup
                }
                return 0; // One-shot
            }, delayMs);

            _entityEvents[key] = eventId;
        }
    }

    /// <summary>
    /// Schedule a repeating event for an entity by key.
    /// </summary>
    public static void ScheduleRepeating(object entity, object eventKey, Func<int> callback, int delayMs)
    {
        var key = (entity, eventKey);

        lock (_lock)
        {
            // Cancel existing event if any
            if (_entityEvents.TryGetValue(key, out var existingEventId))
            {
                EventSystem.CancelEvent(existingEventId);
            }

            // Schedule repeating event
            var eventId = EventSystem.EnqueueEvent(() =>
            {
                var nextDelay = callback();
                if (nextDelay == 0)
                {
                    lock (_lock)
                    {
                        _entityEvents.Remove(key);
                    }
                }
                return nextDelay;
            }, delayMs);

            _entityEvents[key] = eventId;
        }
    }

    /// <summary>
    /// Cancel a specific event for an entity.
    /// </summary>
    public static bool Cancel(object entity, object eventKey)
    {
        var key = (entity, eventKey);

        lock (_lock)
        {
            if (_entityEvents.TryGetValue(key, out var eventId))
            {
                EventSystem.CancelEvent(eventId);
                _entityEvents.Remove(key);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Cancel all events for a specific entity (call in OnDespawn).
    /// </summary>
    public static void CancelAll(object entity)
    {
        lock (_lock)
        {
            // Find all events for this entity
            var keysToRemove = _entityEvents.Keys
                .Where(k => ReferenceEquals(k.Entity, entity))
                .ToList();

            foreach (var key in keysToRemove)
            {
                var eventId = _entityEvents[key];
                EventSystem.CancelEvent(eventId);
                _entityEvents.Remove(key);
            }
        }
    }

    /// <summary>
    /// Check if an event is scheduled for an entity.
    /// </summary>
    public static bool IsScheduled(object entity, object eventKey)
    {
        var key = (entity, eventKey);
        lock (_lock)
        {
            return _entityEvents.ContainsKey(key);
        }
    }

    /// <summary>
    /// Get count of scheduled events for an entity (for debugging).
    /// </summary>
    public static int GetEntityEventCount(object entity)
    {
        lock (_lock)
        {
            return _entityEvents.Keys.Count(k => ReferenceEquals(k.Entity, entity));
        }
    }

    /// <summary>
    /// Get total count of all scheduled events (for debugging).
    /// </summary>
    public static int TotalEventCount
    {
        get
        {
            lock (_lock)
            {
                return _entityEvents.Count;
            }
        }
    }
}
