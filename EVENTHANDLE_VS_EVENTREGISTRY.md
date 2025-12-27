# EventHandle vs EventRegistry: Avoiding State Duplication

## The Problem with EventRegistry

**EventRegistry maintains a dictionary:**
```csharp
private readonly Dictionary<object, long> _events = new();
```

**EventSystem also maintains a dictionary:**
```csharp
private static readonly Dictionary<long, Event> PendingEvents = new();
```

**State duplication issues:**
1. **Two sources of truth**: EventRegistry says event exists, EventSystem says it doesn't
2. **Desync on natural completion**: Event fires → EventSystem removes it → EventRegistry relies on callback to self-cleanup
3. **Exception safety**: If callback throws before `_events.Remove(key)`, state is permanently desynced
4. **Memory overhead**: Two dictionaries for the same events
5. **Complexity**: Harder to reason about lifecycle

## Solution: EventHandle (Single Source of Truth)

**EventHandle stores only a nullable long:**
```csharp
public class EventHandle
{
    private long? _eventId; // Just the ID, no dictionary
}
```

**EventSystem is the single source of truth:**
- EventHandle is just a thin wrapper around the ID
- No duplicate state, no desync possible
- Matches reference server's `LPEVENT` pattern

## Comparison: Entity Implementation

### With EventRegistry (Dictionary-based):

```csharp
public abstract class Entity : IEntity
{
    // Single field but contains a dictionary
    protected readonly EventRegistry Events;

    protected Entity(IAnimationManager animationManager, uint vid)
    {
        Events = new EventRegistry($"Entity:{vid}");
    }

    public virtual void Move(int x, int y)
    {
        if (Events.IsScheduled(EntityEventType.KnockoutToDeath))
            return;
        // ...
    }

    public virtual int Damage(IEntity attacker, EDamageType damageType, int damage)
    {
        if (Health <= 0 && !Events.IsScheduled(EntityEventType.KnockoutToDeath))
        {
            Events.Schedule(EntityEventType.KnockoutToDeath, () => Die(), 3000);
        }
        return damage;
    }

    public override void OnDespawn()
    {
        Events.CancelAll(); // Must iterate dictionary and cancel each
    }
}
```

**Memory per entity:**
- EventRegistry object: 24 bytes (object header)
- Dictionary: ~80+ bytes (initial capacity, even if empty)
- Per event: 32+ bytes (dictionary entry overhead)
- **Total: ~104+ bytes even with no events**

**Desync risk:**
```csharp
// In EventRegistry.Schedule():
var eventId = EventSystem.EnqueueEvent(() =>
{
    callback();
    _events.Remove(key); // What if callback throws? Leaked entry!
    return 0;
}, timeout);
_events[key] = eventId;
```

### With EventHandle (Nullable long):

```csharp
public abstract class Entity : IEntity
{
    // Individual handles - explicit and clear
    private readonly EventHandle _knockoutToDeathEvent = new();

    protected Entity(IAnimationManager animationManager, uint vid)
    {
        _animationManager = animationManager;
        Vid = vid;
    }

    public virtual void Move(int x, int y)
    {
        if (_knockoutToDeathEvent.IsScheduled)
            return;
        // ...
    }

    public virtual int Damage(IEntity attacker, EDamageType damageType, int damage)
    {
        if (Health <= 0 && !_knockoutToDeathEvent.IsScheduled)
        {
            _knockoutToDeathEvent.Schedule(() => Die(), 3000);
        }
        return damage;
    }

    public override void OnDespawn()
    {
        _knockoutToDeathEvent.Cancel(); // Direct, no iteration
    }
}
```

**Memory per entity:**
- EventHandle object: 24 bytes (object header + nullable long)
- **Total: 24 bytes, zero overhead when no event scheduled**

**No desync risk:**
```csharp
// In EventHandle.Schedule():
_eventId = EventSystem.EnqueueEvent(() =>
{
    var result = callback();
    if (result == 0)
    {
        _eventId = null; // Simple, exception-safe
    }
    return result;
}, timeout);
```

If callback throws, EventSystem will clean up the event. The `_eventId = null` just tracks local state.

## PlayerEntity Implementation

### With EventRegistry:

```csharp
public class PlayerEntity : Entity
{
    // Inherits Events from base class
    // How many events does it have? Unclear without looking at enum

    public override void Die()
    {
        Events.Schedule(EntityEventType.AutoRespawnInTown, () => Respawn(true), 10000);
    }

    public void StartLogout(int seconds)
    {
        Events.ScheduleLogoutCountdown(() => Connection.Close(), seconds);
    }

    public override void Move(int x, int y)
    {
        if (Events.CancelLogoutCountdown())
        {
            SendChatInfo("Logout cancelled");
        }
        base.Move(x, y);
    }

    public override void OnDespawn()
    {
        Events.CancelAll(); // Cancels all events, but which ones?
        base.OnDespawn();
    }
}
```

**Issues:**
- Not obvious which events exist
- `CancelAll()` is a blunt instrument
- Dictionary grows with different event types
- Helper methods like `CancelLogoutCountdown()` hide the registry key

### With EventHandle:

```csharp
public class PlayerEntity : Entity
{
    // Explicit event handles - self-documenting
    private readonly EventHandle _autoRespawnEvent = new();
    private readonly EventHandle _logoutCountdownEvent = new();

    public override void Die()
    {
        _autoRespawnEvent.Schedule(() => Respawn(true), 10000);
    }

    public void StartLogout(int seconds)
    {
        int remaining = seconds;
        _logoutCountdownEvent.Schedule(() =>
        {
            if (remaining <= 0)
            {
                Connection.Close();
                return 0; // Done
            }
            SendChatInfo($"{remaining} seconds...");
            remaining--;
            return 1000; // Continue
        }, 1000);
    }

    public override void Move(int x, int y)
    {
        if (_logoutCountdownEvent.Cancel())
        {
            SendChatInfo("Logout cancelled");
        }
        base.Move(x, y);
    }

    public override void OnDespawn()
    {
        // Explicit cleanup - clear which events we have
        _autoRespawnEvent.Cancel();
        _logoutCountdownEvent.Cancel();
        base.OnDespawn();
    }
}
```

**Benefits:**
- ✅ Self-documenting: see all events at a glance
- ✅ Direct access: `_logoutCountdownEvent.Cancel()`
- ✅ No hidden dictionary
- ✅ Explicit cleanup in OnDespawn

## Reference Server Pattern

This matches the reference server exactly:

**Reference C++ (char.h:1703-1722):**
```cpp
LPEVENT m_pkDeadEvent;
LPEVENT m_pkStunEvent;
LPEVENT m_pkSaveEvent;
LPEVENT m_pkRecoveryEvent;
LPEVENT m_pkTimedEvent;
LPEVENT m_pkFishingEvent;
LPEVENT m_pkPoisonEvent;
LPEVENT m_pkFireEvent;
LPEVENT m_pkWarpNPCEvent;
// ... explicit list of events
```

**C# with EventHandle:**
```csharp
private readonly EventHandle _deadEvent = new();
private readonly EventHandle _stunEvent = new();
private readonly EventHandle _saveEvent = new();
private readonly EventHandle _recoveryEvent = new();
private readonly EventHandle _timedEvent = new();
private readonly EventHandle _fishingEvent = new();
private readonly EventHandle _poisonEvent = new();
private readonly EventHandle _fireEvent = new();
private readonly EventHandle _warpNpcEvent = new();
// ... explicit list of events
```

**Why explicit list is good:**
- Clear ownership and lifecycle
- Easy to see all events in one place
- No hidden state in dictionaries
- Compile-time verification
- Easy to debug (inspect fields)

## Performance Comparison

| Aspect | EventRegistry | EventHandle |
|--------|---------------|-------------|
| Memory per entity (empty) | ~104 bytes | 24 bytes |
| Memory per event | +32 bytes (dict entry) | 0 (just the ID) |
| Lookup cost | O(n) dict lookup + hash | O(1) field access |
| Cancel cost | O(n) dict lookup | O(1) field access |
| CancelAll cost | O(n) iterate dict | O(1) per field |
| State duplication | ❌ Yes (two dicts) | ✅ No (just IDs) |
| Desync risk | ❌ Yes | ✅ No |

## When EventRegistry Makes Sense

**If you have truly dynamic events:**
```csharp
// Party invites to different players (unknown at compile time)
private readonly Dictionary<Guid, EventHandle> _partyInvites = new();

public void InviteToParty(Guid playerId)
{
    var handle = new EventHandle();
    handle.Schedule(() => InviteExpired(playerId), 10000);
    _partyInvites[playerId] = handle;
}
```

**But still use EventHandle, not EventRegistry!**
- Each entry is an EventHandle (nullable long)
- No duplicate state with EventSystem
- Clear ownership

## Recommendation

**Use EventHandle for entity events:**
```csharp
public abstract class Entity : IEntity
{
    private readonly EventHandle _knockoutToDeathEvent = new();
}

public class PlayerEntity : Entity
{
    private readonly EventHandle _autoRespawnEvent = new();
    private readonly EventHandle _logoutCountdownEvent = new();
}

public class MonsterEntity : Entity
{
    private readonly EventHandle _aggroTimeoutEvent = new();
    private readonly EventHandle _specialAttackCooldownEvent = new();
}
```

**Use Dictionary<TKey, EventHandle> for dynamic events:**
```csharp
public class PlayerEntity : Entity
{
    // Dynamic: one event per invited player
    private readonly Dictionary<Guid, EventHandle> _partyInvites = new();
}
```

**Benefits:**
- ✅ Single source of truth (EventSystem)
- ✅ No state duplication
- ✅ No desync bugs
- ✅ Lower memory overhead
- ✅ Faster (no dictionary lookups)
- ✅ Self-documenting
- ✅ Matches reference server pattern
- ✅ Exception-safe

**EventRegistry can be removed entirely** - it's unnecessary complexity that introduces bugs without providing real benefits over explicit EventHandle fields.
