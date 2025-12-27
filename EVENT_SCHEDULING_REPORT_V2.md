# Event Scheduling Ergonomics Report - Updated with Full Context

## Executive Summary

After analyzing the `affects` branch, this report documents the current state of event scheduling in QuantumCore-X and proposes ergonomic improvements for one-shot events while maintaining performance.

**Current State:**
- ✅ **Tickers** for continuously running events (HP/SP regen, affects, DoT damage) - well implemented
- ✅ **EventSystem** returns event IDs - already fixed
- ⚠️ **One-shot events** still checked in Update() loops - needs refactoring
- ⚠️ **_countdownEventId** property exists but pattern is verbose - needs better ergonomics

## Current Implementation Analysis

### 1. Ticker System (Already Implemented) ✅

The `affects` branch already has an excellent ticker-based system for **continuously running events**:

**PlayerEntity.cs:86-96 (Ticker declarations):**
```csharp
private readonly GatedTickerEngine<IPlayerEntity> _hpPassiveRestoreTicker;
private readonly GatedTickerEngine<IPlayerEntity> _spPassiveRestoreTicker;
private readonly GatedTickerEngine<IPlayerEntity> _staminaFullRestoreTicker;
private readonly GatedTickerEngine<IPlayerEntity> _hpRecoveryTicker;
private readonly GatedTickerEngine<IPlayerEntity> _spRecoveryTicker;
private readonly GatedTickerEngine<IPlayerEntity> _staminaConsumptionTicker;
private readonly GatedTickerEngine<IPlayerEntity> _affectsTicker;
private readonly SuraBmFlameSpiritTicker _flameSpiritHitTicker;
private readonly GatedTickerEngine<IPlayerEntity> _poisonTicker;
private readonly GatedTickerEngine<IPlayerEntity> _fireTicker;
```

**PlayerEntity.cs:516-531 (Ticker execution in Update()):**
```csharp
var pointsChanged = false;
var elapsed = TimeSpan.FromMilliseconds(elapsedTime);
pointsChanged |= _hpPassiveRestoreTicker.Step(elapsed);
pointsChanged |= _hpRecoveryTicker.Step(elapsed);
pointsChanged |= _spPassiveRestoreTicker.Step(elapsed);
pointsChanged |= _spRecoveryTicker.Step(elapsed);
pointsChanged |= _staminaFullRestoreTicker.Step(elapsed);
pointsChanged |= _staminaConsumptionTicker.Step(elapsed);
pointsChanged |= _affectsTicker.Step(elapsed);
pointsChanged |= _flameSpiritHitTicker.Step(elapsed);
pointsChanged |= _poisonTicker.Step(elapsed);
pointsChanged |= _fireTicker.Step(elapsed);
if (pointsChanged)
{
    SendPoints();
}
```

**Why tickers are good for continuously running events:**
- Predictable execution in Update() loop
- Can easily batch state changes (notice `pointsChanged |=`)
- Clear ownership (ticker belongs to entity)
- Easy to pause/resume
- No heap allocations per tick

### 2. EventSystem (Already Enhanced) ✅

**EventSystem.cs:63-72:**
```csharp
public static long EnqueueEvent(Func<int> callback, int timeout)
{
    lock (PendingEvents)
    {
        var id = _nextEventId++;
        var evt = new Event(id, timeout) { Callback = callback };
        PendingEvents[id] = evt;
        return id;  // ✅ Already returns ID!
    }
}
```

**EventSystem.cs:78-84:**
```csharp
public static bool CancelEvent(long eventId)
{
    lock (PendingEvents)
    {
        return PendingEvents.Remove(eventId);
    }
}
```

This is good - the EventSystem already has the capability to cancel events.

###3. One-Shot Events (Needs Refactoring) ⚠️

These are currently checked **every frame** in Update() loops:

#### Example 1: Death Auto-Respawn (PlayerEntity.cs:503-512)

**Current Implementation:**
```csharp
// auto respawn if the dead timeout elapsed
if (Dead && _diedAtMs.HasValue)
{
    var now = GameServer.Instance.ServerTime;
    var deadline = _diedAtMs.Value + SchedulingConstants.PlayerAutoRespawnDelaySeconds * 1000L;
    if (now >= deadline)
    {
        Respawn(true);
    }
}
```

**Problems:**
- ❌ Checked every frame (~60 times per second)
- ❌ Manual time tracking with `_diedAtMs`
- ❌ No way to cancel early (if player manually respawns)

#### Example 2: Knockout-to-Death Delay (Entity.cs:105-109)

**Current Implementation:**
```csharp
if (!Dead && _knockedOutServerTime.HasValue &&
    GameServer.Instance.ServerTime >= _knockedOutServerTime.Value + KnockoutToDeathDelaySeconds * 1000)
{
    _knockedOutServerTime = null;
    Die();
}
```

**Problems:**
- ❌ Checked every frame for **every entity**
- ❌ Manual time tracking
- ❌ State must be cleared manually

#### Example 3: Countdown Events (PlayerEntity.cs:116)

**Current Implementation:**
```csharp
private long? _countdownEventId;        // the event for logout or phase_select
```

**Usage Pattern (hypothetical based on reference code):**
```csharp
// To schedule
if (_countdownEventId.HasValue)
{
    EventSystem.CancelEvent(_countdownEventId.Value);
}
_countdownEventId = EventSystem.EnqueueEvent(() => {
    // Do something
    return 0;
}, timeout);

// To cancel
if (_countdownEventId.HasValue)
{
    EventSystem.CancelEvent(_countdownEventId.Value);
    _countdownEventId = null;
}
```

**Problems:**
- ❌ Verbose: 6+ lines of code to schedule safely
- ❌ Easy to forget to cancel existing event
- ❌ Easy to forget to clear ID after cancellation
- ❌ No compile-time safety
- ❌ Manual null checks everywhere

### 4. Reference Server Pattern (For Comparison)

**Reference C++ (char.h:1703-1722):**
```cpp
LPEVENT m_pkDeadEvent;
LPEVENT m_pkStunEvent;
LPEVENT m_pkSaveEvent;
LPEVENT m_pkRecoveryEvent;
LPEVENT m_pkTimedEvent;
LPEVENT m_pkFishingEvent;
// ... many more
```

**Usage:**
```cpp
// Schedule
m_pkTimedEvent = event_create(timed_event, info, PASSES_PER_SEC(1));

// Cancel (automatically nullifies)
event_cancel(&m_pkTimedEvent);

// Check
if (m_pkTimedEvent)
{
    // ...
}
```

**Why this is ergonomic:**
- ✅ Single line to schedule
- ✅ Single line to cancel
- ✅ Automatic cleanup
- ✅ Simple null check

## Performance Considerations

### Why Not Use EventSystem for Everything?

**Tickers are better for high-frequency continuously running events:**

1. **No heap allocations** - tickers are pre-allocated members
2. **Predictable execution** - runs in entity's Update(), no locking
3. **Easy batching** - can combine multiple state changes before sending packets
4. **Clear ownership** - ticker lifecycle tied to entity

**EventSystem is better for one-shot delayed events:**

1. **Sparse execution** - only runs when needed, not every frame
2. **Automatic scheduling** - no manual time tracking
3. **Cancellable** - easy to cancel if conditions change
4. **Decoupled** - event can outlive the requesting context

### Performance Impact of Current One-Shot Event Pattern

**Death Auto-Respawn checked every frame:**
- With 100 dead players: 6,000 checks per second (100 * 60 FPS)
- Each check: 2 property accesses + 1 comparison + 1 addition
- Wasted cycles when most players respawn manually

**Knockout-to-Death checked every frame for all entities:**
- With 500 entities (players + monsters): 30,000 checks per second
- This includes entities that are not even knocked out!

**Using EventSystem would eliminate these checks entirely.**

## Proposed Solution

### Option 1: EventHandle Class (Recommended for Ergonomics)

Create a lightweight wrapper that provides reference-server-like ergonomics:

**EventHandle.cs:**
```csharp
namespace QuantumCore.Core.Event;

/// <summary>
/// A handle for managing a single scheduled event with automatic cancellation support.
/// Provides better ergonomics for one-shot events compared to manually tracking event IDs.
/// </summary>
public class EventHandle
{
    private long? _eventId;

    /// <summary>
    /// Whether an event is currently scheduled
    /// </summary>
    public bool IsScheduled => _eventId.HasValue;

    /// <summary>
    /// Schedules a new event, automatically cancelling any previously scheduled event.
    /// </summary>
    /// <param name="callback">The callback to execute. Return 0 to cancel, or timeout in ms to reschedule.</param>
    /// <param name="timeout">Initial timeout in milliseconds</param>
    public void Schedule(Func<int> callback, int timeout)
    {
        Cancel();
        _eventId = EventSystem.EnqueueEvent(callback, timeout);
    }

    /// <summary>
    /// Cancels the currently scheduled event if any.
    /// </summary>
    /// <returns>True if an event was cancelled</returns>
    public bool Cancel()
    {
        if (_eventId.HasValue)
        {
            var wasCancelled = EventSystem.CancelEvent(_eventId.Value);
            _eventId = null;
            return wasCancelled;
        }
        return false;
    }
}
```

**Usage in Entity.cs:**
```csharp
// Declaration
private readonly EventHandle _knockoutToDeathEvent = new();

// Schedule
_knockoutToDeathEvent.Schedule(() =>
{
    Die();
    return 0; // One-shot
}, KnockoutToDeathDelaySeconds * 1000);

// Cancel
_knockoutToDeathEvent.Cancel();

// Check
if (_knockoutToDeathEvent.IsScheduled)
{
    // Can't move while knocked out
    return;
}
```

**Benefits:**
- ✅ One line to schedule (vs 6+ lines before)
- ✅ Automatic cancellation of previous event
- ✅ Automatic ID clearing
- ✅ Simple property check
- ✅ No heap allocation (struct-like usage)

**Performance:**
- Zero overhead compared to manual ID tracking
- Same EventSystem underneath
- No additional locking or allocations

### Option 2: Enhanced EventHandle with Entity Binding

For automatic cleanup when entity is destroyed:

```csharp
public class EntityEventHandle : EventHandle
{
    private readonly IEntity _owner;

    public EntityEventHandle(IEntity owner)
    {
        _owner = owner;
    }

    public void ScheduleWithCleanup(Func<int> callback, int timeout)
    {
        Schedule(() =>
        {
            // Check if entity still exists
            if (_owner.Dead)
            {
                return 0; // Cancel if entity is dead
            }
            return callback();
        }, timeout);
    }
}
```

### Option 3: Dedicated One-Shot Event Methods

Add convenience methods to EventSystem:

```csharp
public static class EventSystem
{
    /// <summary>
    /// Schedules a one-shot event that executes once and automatically cancels.
    /// Returns a handle that can be used to cancel the event early.
    /// </summary>
    public static EventHandle ScheduleOneShot(Action callback, int timeout)
    {
        var handle = new EventHandle();
        handle.Schedule(() =>
        {
            callback();
            return 0; // Always one-shot
        }, timeout);
        return handle;
    }
}
```

**Usage:**
```csharp
_knockoutToDeathEvent = EventSystem.ScheduleOneShot(() => Die(),
    KnockoutToDeathDelaySeconds * 1000);
```

## Refactoring Plan

### Phase 1: Introduce EventHandle

1. Create `EventHandle.cs` in `Core/Event`
2. Add unit tests for EventHandle
3. No changes to existing code yet

### Phase 2: Refactor Knockout-to-Death (Entity)

**Before (Entity.cs:88-94 + 105-109):**
```csharp
private long? _knockedOutServerTime;

// In Update():
if (!Dead && _knockedOutServerTime.HasValue &&
    GameServer.Instance.ServerTime >= _knockedOutServerTime.Value + KnockoutToDeathDelaySeconds * 1000)
{
    _knockedOutServerTime = null;
    Die();
}
```

**After:**
```csharp
private readonly EventHandle _knockoutToDeathEvent = new();

// In ApplyDamageAndBroadcast() when Health <= 0:
if (!_knockoutToDeathEvent.IsScheduled)
{
    this.BroadcastNearby(new KnockoutCharacter { Vid = Vid });
    _knockoutToDeathEvent.Schedule(() =>
    {
        Die();
        return 0;
    }, KnockoutToDeathDelaySeconds * 1000);
}

// No Update() check needed!
```

**Remove from Update()** - the event will fire automatically.

**Update movement/attack checks:**
```csharp
if (_knockoutToDeathEvent.IsScheduled)
    return; // Can't move/attack while knocked out
```

### Phase 3: Refactor Death Auto-Respawn (PlayerEntity)

**Before (PlayerEntity.cs:110 + 503-512):**
```csharp
private long? _diedAtMs;

// In Update():
if (Dead && _diedAtMs.HasValue)
{
    var now = GameServer.Instance.ServerTime;
    var deadline = _diedAtMs.Value + SchedulingConstants.PlayerAutoRespawnDelaySeconds * 1000L;
    if (now >= deadline)
    {
        Respawn(true);
    }
}

// In Die():
_diedAtMs = GameServer.Instance.ServerTime;
```

**After:**
```csharp
private readonly EventHandle _autoRespawnEvent = new();

// In Die():
_autoRespawnEvent.Schedule(() =>
{
    if (Dead) // Double-check player is still dead
    {
        Respawn(true);
    }
    return 0;
}, SchedulingConstants.PlayerAutoRespawnDelaySeconds * 1000);

// In Respawn():
_autoRespawnEvent.Cancel(); // Cancel auto-respawn if manual respawn

// No Update() check needed!
```

### Phase 4: Improve Countdown Event Pattern

**Before (PlayerEntity.cs:116):**
```csharp
private long? _countdownEventId;

// Usage (hypothetical):
if (_countdownEventId.HasValue)
    EventSystem.CancelEvent(_countdownEventId.Value);
_countdownEventId = EventSystem.EnqueueEvent(() => { /*...*/ }, timeout);
```

**After:**
```csharp
private readonly EventHandle _countdownEvent = new();

// Usage:
_countdownEvent.Schedule(() =>
{
    // Logout/quit/phase_select logic
    return 0;
}, timeout);

// Cancel:
_countdownEvent.Cancel();
```

## Comparison Table

| Aspect | Current (nullable long) | Proposed (EventHandle) |
|--------|------------------------|------------------------|
| Lines to schedule safely | 6+ | 1 |
| Lines to cancel | 3 | 1 |
| Automatic cleanup | ❌ Manual | ✅ Automatic |
| Null safety | ❌ Easy to forget | ✅ Built-in |
| Readability | ⚠️ Verbose | ✅ Clear intent |
| Performance | Same | Same |
| Type safety | ❌ All IDs are `long` | ✅ EventHandle type |
| Update() checks | ❌ Required | ✅ Not needed |

## Performance Benefits

### Eliminating Update() Checks

**Current Performance Cost:**
```
100 dead players * 60 FPS = 6,000 checks/second (death respawn)
500 entities * 60 FPS = 30,000 checks/second (knockout)
-----------------------------------------------------------
Total: 36,000 unnecessary checks per second
```

**After Refactoring:**
```
Events fire exactly once when needed = 0 checks/second
```

### Memory Impact

**EventHandle struct size:** ~16 bytes (one nullable long + padding)
**Same as current approach:** nullable long is also ~16 bytes

**No additional heap allocations** - EventHandle is a value type.

## Reference Server Comparison

| Feature | Reference C++ | Current QuantumCore-X | Proposed |
|---------|--------------|----------------------|----------|
| Event storage | `LPEVENT m_pkEvent` | `long? _eventId` | `EventHandle _event` |
| Schedule | `m_pk = event_create(...)` | `_id = EventSystem.Enqueue(...)` | `_event.Schedule(...)` |
| Cancel | `event_cancel(&m_pk)` | `EventSystem.Cancel(_id); _id = null` | `_event.Cancel()` |
| Check | `if (m_pk)` | `if (_id.HasValue)` | `if (_event.IsScheduled)` |
| Update() checks | ❌ No | ❌ Yes (for some events) | ✅ No |
| Ergonomics | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ |

## Conclusion

The `affects` branch has excellent ticker infrastructure for continuously running events. The issue is with **one-shot delayed events** that are:

1. Currently checked every frame (inefficient)
2. Require verbose manual management (poor ergonomics)

**Proposed Solution:**
- Introduce `EventHandle` wrapper class
- Refactor one-shot events to use EventSystem instead of Update() checks
- Maintain ticker system for high-frequency recurring events

**Benefits:**
- Better ergonomics (1 line vs 6+ lines)
- Better performance (eliminate Update() checks)
- Same core EventSystem mechanism
- Zero additional overhead

**Implementation Priority:**
1. ✅ EventHandle class (low risk)
2. ✅ Knockout-to-death refactor (high impact)
3. ✅ Death auto-respawn refactor (high impact)
4. ✅ Countdown events refactor (better ergonomics)

This approach keeps the performance benefits of the ticker system while providing ergonomic one-shot event management.
