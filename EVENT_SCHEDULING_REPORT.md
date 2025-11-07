# Event Scheduling Ergonomics Report

## Executive Summary

This report analyzes the ergonomics issue with event scheduling in QuantumCore-X compared to the reference Metin2 server implementation. The core issue is the verbosity and potential error-proneness of managing one-shot (non-repeating) scheduled events that need to be cancellable.

## Background

The user mentioned investigating `private long? _countdownEventId;` in the `PlayerEntity` class. **This property does not currently exist** in the codebase, which is part of the ergonomics issue - there's no established pattern for storing event IDs for entity-specific one-shot events.

## Reference Server Implementation (cCorax2/Source_code)

### Event System Architecture

The reference C++ implementation uses a pointer-based event system:

**Core Types:**
```cpp
typedef struct event EVENT;
typedef boost::intrusive_ptr<EVENT> LPEVENT;
typedef long (*TEVENTFUNC) (LPEVENT event, long processing_time);
```

**Event Creation and Cancellation:**
```cpp
LPEVENT event_create(TEVENTFUNC func, event_info_data* info, long when);
void event_cancel(LPEVENT * event);  // Automatically sets pointer to NULL
```

### Pattern for One-Shot Events in CHARACTER Class

The `CHARACTER` class (equivalent to `PlayerEntity`) stores dedicated event pointers for each type of one-shot event:

**Event Member Variables (char.h:1703-1722):**
```cpp
LPEVENT m_pkDeadEvent;
LPEVENT m_pkStunEvent;
LPEVENT m_pkSaveEvent;
LPEVENT m_pkRecoveryEvent;
LPEVENT m_pkTimedEvent;         // For logout/quit countdowns
LPEVENT m_pkFishingEvent;
LPEVENT m_pkAffectEvent;
LPEVENT m_pkPoisonEvent;
LPEVENT m_pkFireEvent;
LPEVENT m_pkWarpNPCEvent;
LPEVENT m_pkMiningEvent;
LPEVENT m_pkWarpEvent;
LPEVENT m_pkCheckSpeedHackEvent;
LPEVENT m_pkDestroyWhenIdleEvent;
LPEVENT m_pkPetSystemUpdateEvent;
```

### Example: Timed Event (Logout/Quit Countdown)

**Event Function (cmd_general.cpp:261-332):**
```cpp
EVENTFUNC(timed_event)
{
    TimedEventInfo* info = dynamic_cast<TimedEventInfo*>(event->info);
    if (info == NULL) return 0;

    LPCHARACTER ch = info->ch;
    if (ch == NULL) return 0;

    if (info->left_second <= 0)
    {
        ch->m_pkTimedEvent = NULL;  // Clear the event pointer

        // Execute final action (logout, quit, etc.)
        switch (info->subcmd)
        {
            case SCMD_LOGOUT:
                // Handle logout
                break;
            // ...
        }
        return 0;  // Return 0 to cancel event
    }
    else
    {
        ch->ChatPacket(CHAT_TYPE_INFO, "%d seconds remaining", info->left_second);
        --info->left_second;
    }

    return PASSES_PER_SEC(1);  // Return non-zero to reschedule in 1 second
}
```

**Creating the Event (cmd_general.cpp:382-396):**
```cpp
TimedEventInfo* info = AllocEventInfo<TimedEventInfo>();
info->ch = ch;
info->subcmd = subcmd;
info->left_second = ch->IsPosition(POS_FIGHTING) ? 10 : 3;

ch->m_pkTimedEvent = event_create(timed_event, info, 1);
```

**Cancelling the Event (cmd_general.cpp:345-350):**
```cpp
if (ch->m_pkTimedEvent)
{
    ch->ChatPacket(CHAT_TYPE_INFO, "Cancelled");
    event_cancel(&ch->m_pkTimedEvent);  // Automatically sets m_pkTimedEvent to NULL
    return;
}
```

**Cleanup on Character Destruction (char.cpp:508-528):**
```cpp
CHARACTER::~CHARACTER()
{
    event_cancel(&m_pkWarpNPCEvent);
    event_cancel(&m_pkRecoveryEvent);
    event_cancel(&m_pkDeadEvent);
    event_cancel(&m_pkSaveEvent);
    event_cancel(&m_pkTimedEvent);
    event_cancel(&m_pkStunEvent);
    event_cancel(&m_pkFishingEvent);
    event_cancel(&m_pkPoisonEvent);
    event_cancel(&m_pkFireEvent);
    event_cancel(&m_pkPartyRequestEvent);
    event_cancel(&m_pkWarpEvent);
    event_cancel(&m_pkCheckSpeedHackEvent);
    event_cancel(&m_pkMiningEvent);
    event_cancel(&m_pkDestroyWhenIdleEvent);
    // ...
}
```

### Example: Warp NPC Event

**Event Function (char.cpp:6426-6452):**
```cpp
EVENTFUNC(warp_npc_event)
{
    char_event_info* info = dynamic_cast<char_event_info*>(event->info);
    if (info == NULL || info->ch == NULL) return 0;

    LPCHARACTER ch = info->ch;

    if (!ch->GetSectree())
    {
        ch->m_pkWarpNPCEvent = NULL;
        return 0;
    }

    FuncCheckWarp f(ch);
    if (f.Valid())
        ch->GetSectree()->ForEachAround(f);

    return passes_per_sec / 2;  // Repeats every 0.5 seconds
}
```

**Starting the Event (char.cpp:6455-6468):**
```cpp
void CHARACTER::StartWarpNPCEvent()
{
    if (m_pkWarpNPCEvent)  // Already running
        return;

    if (!IsWarp() && !IsGoto())
        return;

    char_event_info* info = AllocEventInfo<char_event_info>();
    info->ch = this;

    m_pkWarpNPCEvent = event_create(warp_npc_event, info, passes_per_sec / 2);
}
```

### Example: Stun Event

**Event Creation (char_battle.cpp:434):**
```cpp
m_pkStunEvent = event_create(StunEvent, info, PASSES_PER_SEC(3));
```

**Cancellation (char.cpp:1104-1105):**
```cpp
event_cancel(&m_pkDeadEvent);
event_cancel(&m_pkStunEvent);
```

## QuantumCore-X Current Implementation

### Event System Architecture

QuantumCore-X uses an ID-based event system:

**Core Class (EventSystem.cs:6-83):**
```csharp
public class EventSystem
{
    private static readonly Dictionary<long, Event> PendingEvents = new();
    private static long _nextEventId = 1;

    public static void EnqueueEvent(Func<int> callback, int timeout)
    {
        lock (PendingEvents)
        {
            var id = _nextEventId++;
            var evt = new Event(id, timeout) { Callback = callback };
            PendingEvents[id] = evt;
        }
    }

    public static void CancelEvent(long eventId)
    {
        lock (PendingEvents)
        {
            PendingEvents.Remove(eventId);
        }
    }
}
```

**Event Class (Event.cs:3-15):**
```csharp
public class Event
{
    public long Id { get; }
    public required Func<int> Callback { get; init; }
    public int Time { get; set; }
}
```

### Current Usage Patterns

**Example 1: Repeating Event (GameConnection.cs:46-50):**
```csharp
EventSystem.EnqueueEvent(() =>
{
    Send(ping);
    return pingInterval;  // Repeat
}, pingInterval);
```

**Example 2: One-Shot Event (Map.cs:389-394):**
```csharp
EventSystem.EnqueueEvent(() =>
{
    SpawnGroup(group);
    return 0;  // Don't repeat
}, group.SpawnPoint.RespawnTime * 1000);
```

### Current Issues

1. **No return value captured**: `EnqueueEvent` returns `void`, so there's no way to store the event ID for later cancellation
2. **No established pattern** for entity-specific one-shot events
3. **Manual cleanup required**: No automatic cleanup when entity is destroyed

## The Ergonomics Problem

### What Would Be Needed in QuantumCore-X

To implement the same pattern as the reference server, you would need:

**PlayerEntity.cs (hypothetical):**
```csharp
public class PlayerEntity : Entity
{
    private long? _timedEventId;      // For logout/quit countdown
    private long? _warpEventId;       // For warp handling
    private long? _stunEventId;       // For stun duration
    private long? _fishingEventId;    // For fishing activity
    private long? _miningEventId;     // For mining activity
    private long? _saveEventId;       // For periodic save
    private long? _recoveryEventId;   // For HP/SP recovery
    // ... and many more

    public void StartTimedEvent(int seconds, Action onComplete)
    {
        // Cancel existing event if any
        if (_timedEventId.HasValue)
        {
            EventSystem.CancelEvent(_timedEventId.Value);
            _timedEventId = null;
        }

        // Create new event
        int remaining = seconds;
        _timedEventId = EventSystem.EnqueueEvent(() =>
        {
            if (remaining <= 0)
            {
                _timedEventId = null;
                onComplete();
                return 0;  // Don't repeat
            }

            SendChatInfo($"{remaining} seconds remaining");
            remaining--;
            return 1000;  // Repeat in 1 second
        }, 1000);
    }

    public void CancelTimedEvent()
    {
        if (_timedEventId.HasValue)
        {
            EventSystem.CancelEvent(_timedEventId.Value);
            _timedEventId = null;
        }
    }

    // Cleanup on disposal
    public override void Dispose()
    {
        if (_timedEventId.HasValue)
            EventSystem.CancelEvent(_timedEventId.Value);
        if (_warpEventId.HasValue)
            EventSystem.CancelEvent(_warpEventId.Value);
        if (_stunEventId.HasValue)
            EventSystem.CancelEvent(_stunEventId.Value);
        // ... cancel all other events

        base.Dispose();
    }
}
```

### Problems with This Approach

1. **Verbose**: Every event type needs 3 pieces of code:
   - A nullable long field
   - Null checking and cancellation before creating new event
   - Manual cleanup in Dispose

2. **Error-prone**: Easy to forget to:
   - Cancel existing event before creating new one
   - Clear the ID after cancellation
   - Add cleanup in Dispose method

3. **API Mismatch**: `EnqueueEvent` doesn't return the event ID, so the API needs to be changed first

4. **No Compile-Time Safety**: Using `long?` for event IDs provides no type safety - you could accidentally use the wrong ID

## Comparison Summary

| Aspect | Reference Server (C++) | QuantumCore-X (C#) |
|--------|----------------------|-------------------|
| Event Handle | `LPEVENT` (smart pointer) | `long` (ID) |
| Storage | Direct member variable | Nullable long field |
| Creation | Returns event pointer | Returns void (needs fixing) |
| Cancellation | `event_cancel(&ptr)` - auto nullifies | Manual: `CancelEvent(id)` + set to null |
| Null Check | `if (m_pkEvent)` | `if (_eventId.HasValue)` |
| Automatic Cleanup | Yes - destructor cancels all | No - must manually track and cancel |
| Type Safety | Yes - each event is its own type | No - all IDs are `long` |
| Ergonomics | Excellent | Poor |

## Recommendations

### Option 1: Enhance EventSystem to Return Event IDs

**Modify EventSystem.cs:**
```csharp
public static long EnqueueEvent(Func<int> callback, int timeout)
{
    lock (PendingEvents)
    {
        var id = _nextEventId++;
        var evt = new Event(id, timeout) { Callback = callback };
        PendingEvents[id] = evt;
        return id;  // Return the ID
    }
}
```

This is a minimal fix but doesn't solve the ergonomics issue.

### Option 2: Create an Event Handle Class (Recommended)

Create a disposable event handle similar to the reference implementation:

**EventHandle.cs:**
```csharp
public class EventHandle : IDisposable
{
    private long? _eventId;

    public bool IsActive => _eventId.HasValue;

    internal void Set(long eventId)
    {
        Cancel();
        _eventId = eventId;
    }

    public void Cancel()
    {
        if (_eventId.HasValue)
        {
            EventSystem.CancelEvent(_eventId.Value);
            _eventId = null;
        }
    }

    public void Dispose() => Cancel();
}
```

**Enhanced EventSystem:**
```csharp
public static EventHandle EnqueueEvent(Func<int> callback, int timeout)
{
    var handle = new EventHandle();
    lock (PendingEvents)
    {
        var id = _nextEventId++;
        var evt = new Event(id, timeout) { Callback = callback };
        PendingEvents[id] = evt;
        handle.Set(id);
    }
    return handle;
}
```

**Usage in PlayerEntity:**
```csharp
public class PlayerEntity : Entity
{
    private readonly EventHandle _timedEvent = new();
    private readonly EventHandle _warpEvent = new();
    private readonly EventHandle _stunEvent = new();

    public void StartTimedEvent(int seconds, Action onComplete)
    {
        int remaining = seconds;
        _timedEvent.Cancel();  // Simple, safe cancellation

        EventSystem.EnqueueEvent(() =>
        {
            if (remaining <= 0)
            {
                onComplete();
                return 0;
            }
            SendChatInfo($"{remaining} seconds remaining");
            remaining--;
            return 1000;
        }, 1000, _timedEvent);  // Pass handle to be set
    }

    public override void Dispose()
    {
        _timedEvent.Dispose();  // Automatic cleanup
        _warpEvent.Dispose();
        _stunEvent.Dispose();
        base.Dispose();
    }
}
```

### Option 3: Entity-Aware Event System

Create a higher-level event system that automatically tracks and cancels events per entity:

**EntityEventManager.cs:**
```csharp
public class EntityEventManager
{
    private readonly Dictionary<(IEntity, string), EventHandle> _entityEvents = new();

    public void ScheduleEntityEvent(IEntity entity, string eventName,
        Func<int> callback, int timeout)
    {
        var key = (entity, eventName);

        if (_entityEvents.TryGetValue(key, out var existing))
        {
            existing.Cancel();
        }

        var handle = EventSystem.EnqueueEvent(callback, timeout);
        _entityEvents[key] = handle;
    }

    public void CancelEntityEvent(IEntity entity, string eventName)
    {
        var key = (entity, eventName);
        if (_entityEvents.TryGetValue(key, out var handle))
        {
            handle.Cancel();
            _entityEvents.Remove(key);
        }
    }

    public void CancelAllEntityEvents(IEntity entity)
    {
        var toRemove = _entityEvents.Keys
            .Where(k => k.Item1 == entity)
            .ToList();

        foreach (var key in toRemove)
        {
            _entityEvents[key].Cancel();
            _entityEvents.Remove(key);
        }
    }
}
```

## Conclusion

The reference server's event system provides superior ergonomics through:
1. **Automatic cleanup** via pointer management
2. **Simple cancellation** with `event_cancel(&ptr)`
3. **Clear state checking** with `if (ptr)`
4. **Type safety** through dedicated pointer types

QuantumCore-X's current ID-based system requires manual management of nullable longs, which is verbose and error-prone. **Option 2** (Event Handle Class) provides the best balance of ergonomics and safety while maintaining the ID-based architecture.

The mentioned `_countdownEventId` property doesn't exist yet, which illustrates the problem - developers recognize the need for storing event IDs but there's no established pattern for doing so elegantly.

## Next Steps

1. Implement Option 2 (Event Handle Class)
2. Create helper methods in Entity base class for common event patterns
3. Update existing code to use the new pattern
4. Document the pattern for future event-based features
5. Consider adding compile-time checks to prevent common errors
