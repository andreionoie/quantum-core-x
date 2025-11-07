# EventRegistry: TimeSpan API for Type Safety and Clarity

## Problem with Milliseconds

Using raw milliseconds is error-prone and unclear:

```csharp
// What unit is this? ms? seconds?
Events.Schedule(key, callback, 3000);

// Easy to make mistakes
Events.Schedule(key, callback, 10);      // Is this 10ms or 10 seconds?
Events.Schedule(key, callback, 10000);   // Typo? Should be 1000?

// Repeating events returning ms is unclear
Events.ScheduleRepeating(key, () =>
{
    DoSomething();
    return 1000; // What does 1000 mean?
}, 5000);
```

## Solution: TimeSpan API

TimeSpan is self-documenting and type-safe:

```csharp
// Crystal clear
Events.Schedule(key, callback, TimeSpan.FromSeconds(3));

// Impossible to confuse units
Events.Schedule(key, callback, TimeSpan.FromMilliseconds(10));
Events.Schedule(key, callback, TimeSpan.FromSeconds(10));
Events.Schedule(key, callback, TimeSpan.FromMinutes(5));

// Repeating events are clear
Events.ScheduleRepeating(key, () =>
{
    DoSomething();
    return TimeSpan.FromSeconds(1); // Obviously 1 second
}, TimeSpan.FromSeconds(5));

// Stop repeating is explicit
Events.ScheduleRepeating(key, () =>
{
    if (done)
        return TimeSpan.Zero; // Stop

    DoSomething();
    return TimeSpan.FromSeconds(1);
}, TimeSpan.FromSeconds(1));
```

## EventRegistry API

```csharp
public class EventRegistry
{
    /// <summary>
    /// Schedule a one-shot event.
    /// </summary>
    public void Schedule(object key, Action callback, TimeSpan delay)
    {
        // Converts to ms internally for EventSystem
        var eventId = EventSystem.EnqueueEvent(() =>
        {
            callback();
            _events.Remove(key);
            return 0;
        }, (int)delay.TotalMilliseconds);

        _events[key] = eventId;
    }

    /// <summary>
    /// Schedule a repeating event.
    /// Callback returns TimeSpan for next delay, or TimeSpan.Zero to stop.
    /// </summary>
    public void ScheduleRepeating(object key, Func<TimeSpan> callback, TimeSpan initialDelay)
    {
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
}
```

**Benefits:**
- ✅ TimeSpan on the outside (type-safe, clear)
- ✅ Milliseconds on the inside (EventSystem compatibility)
- ✅ Conversion hidden in EventRegistry

## Extension Method Examples

### Before (Milliseconds):

```csharp
public static void ScheduleKnockoutToDeath(this Entity entity)
{
    entity.Events.Schedule(
        EntityEventType.KnockoutToDeath,
        () => entity.Die(),
        SchedulingConstants.KnockoutToDeathDelaySeconds * 1000  // ❌ Manual conversion
    );
}

public static void ScheduleLogoutCountdown(this PlayerEntity player, int seconds)
{
    int remaining = seconds;
    player.Events.ScheduleRepeating(
        EntityEventType.LogoutCountdown,
        () =>
        {
            if (remaining <= 0)
            {
                player.Connection.Close();
                return 0; // ❌ What does 0 mean?
            }
            player.SendChatInfo($"{remaining} seconds remaining...");
            remaining--;
            return 1000; // ❌ Is this milliseconds?
        },
        1000 // ❌ What unit?
    );
}
```

### After (TimeSpan):

```csharp
public static void ScheduleKnockoutToDeath(this Entity entity)
{
    entity.Events.Schedule(
        EntityEventType.KnockoutToDeath,
        () => entity.Die(),
        TimeSpan.FromSeconds(SchedulingConstants.KnockoutToDeathDelaySeconds) // ✅ Clear
    );
}

public static void ScheduleLogoutCountdown(this PlayerEntity player, int seconds)
{
    int remaining = seconds;
    player.Events.ScheduleRepeating(
        EntityEventType.LogoutCountdown,
        () =>
        {
            if (remaining <= 0)
            {
                player.Connection.Close();
                return TimeSpan.Zero; // ✅ Explicit: stop repeating
            }
            player.SendChatInfo($"{remaining} seconds remaining...");
            remaining--;
            return TimeSpan.FromSeconds(1); // ✅ Explicit: 1 second
        },
        TimeSpan.FromSeconds(1) // ✅ Explicit: initial delay
    );
}
```

## Usage in Entity Classes

```csharp
public class Entity : IEntity
{
    protected readonly EventRegistry Events = new();

    public virtual int Damage(IEntity attacker, EDamageType damageType, int damage)
    {
        if (Health <= 0)
        {
            if (!this.IsKnockedOut())
            {
                this.BroadcastNearby(new KnockoutCharacter { Vid = Vid });
                this.ScheduleKnockoutToDeath(); // Extension hides TimeSpan details
            }
        }
        return damage;
    }
}

public class GroundItem : Entity
{
    public GroundItem(/* params */)
    {
        if (_ownerName != null)
        {
            // Optional TimeSpan parameter - defaults are in extension methods
            this.ScheduleOwnershipExpiry(); // Uses TimeSpan.FromSeconds(30)

            // Or override with custom delay
            this.ScheduleOwnershipExpiry(TimeSpan.FromMinutes(1));
        }

        // Clear, readable defaults
        this.ScheduleItemDisappear(); // Uses TimeSpan.FromMinutes(5)
    }
}
```

## Advanced: TimeSpan Arithmetic

TimeSpan supports arithmetic operations for complex scenarios:

```csharp
public static void ScheduleDynamicCooldown(this MonsterEntity monster, int level)
{
    // Base cooldown reduces with level
    var baseCooldown = TimeSpan.FromSeconds(30);
    var levelReduction = TimeSpan.FromSeconds(level * 0.5);
    var actualCooldown = baseCooldown - levelReduction;

    // Ensure minimum cooldown
    var minimumCooldown = TimeSpan.FromSeconds(5);
    var finalCooldown = actualCooldown < minimumCooldown
        ? minimumCooldown
        : actualCooldown;

    monster.Events.Schedule(
        MonsterEventType.SpecialAttackCooldown,
        () => monster.EnableSpecialAttack(),
        finalCooldown
    );
}

public static void ScheduleProgressiveDelay(this Entity entity)
{
    var delays = new[]
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    };

    int index = 0;
    entity.Events.ScheduleRepeating(
        EntityEventType.ProgressiveEvent,
        () =>
        {
            if (index >= delays.Length)
                return TimeSpan.Zero; // Stop

            DoSomething();
            return delays[index++]; // Progressive delays
        },
        delays[0]
    );
}
```

## Comparison Table

| Aspect | Milliseconds (int) | TimeSpan |
|--------|-------------------|----------|
| Clarity | ❌ `3000` - what unit? | ✅ `TimeSpan.FromSeconds(3)` |
| Type safety | ❌ Any int accepted | ✅ TimeSpan type enforced |
| Stop signal | ❌ `return 0` - unclear | ✅ `return TimeSpan.Zero` - explicit |
| Arithmetic | ❌ `delay * 1000 / 60` | ✅ `TimeSpan.FromSeconds(delay / 60)` |
| Intellisense | ❌ Just a number | ✅ Properties: TotalSeconds, TotalMinutes, etc. |
| Refactoring | ❌ Hard to change units | ✅ Easy: change FromSeconds to FromMinutes |
| Readability | ⭐⭐ | ⭐⭐⭐⭐⭐ |

## Real-World Examples

### Knockout to Death (3 seconds):
```csharp
// Before
Events.Schedule(key, callback, 3000);

// After
Events.Schedule(key, callback, TimeSpan.FromSeconds(3));
```

### Item Ownership Expiry (30 seconds):
```csharp
// Before
Events.Schedule(key, callback, 30000);

// After
Events.Schedule(key, callback, TimeSpan.FromSeconds(30));
```

### Item Disappear (5 minutes):
```csharp
// Before
Events.Schedule(key, callback, 300000); // ??? What is this number?

// After
Events.Schedule(key, callback, TimeSpan.FromMinutes(5)); // ✅ Crystal clear
```

### Logout Countdown (per-second ticks):
```csharp
// Before
Events.ScheduleRepeating(key, () =>
{
    DoTick();
    return 1000; // 1000 what?
}, 1000);

// After
Events.ScheduleRepeating(key, () =>
{
    DoTick();
    return TimeSpan.FromSeconds(1); // ✅ 1 second
}, TimeSpan.FromSeconds(1));
```

## Backward Compatibility with EventSystem

EventSystem still uses milliseconds internally, but EventRegistry handles the conversion:

```csharp
// EventSystem API (unchanged)
EventSystem.EnqueueEvent(callback, milliseconds)

// EventRegistry API (TimeSpan wrapper)
Events.Schedule(key, callback, TimeSpan.FromSeconds(3))
// ↓ Converts internally
EventSystem.EnqueueEvent(callback, 3000)
```

This keeps EventSystem simple while providing a better API at the EventRegistry level.

## Conclusion

**TimeSpan API benefits:**
- ✅ Type-safe (can't pass wrong units)
- ✅ Self-documenting (clear what the value means)
- ✅ Explicit stop signal (`TimeSpan.Zero` vs `0`)
- ✅ Supports arithmetic operations
- ✅ Better Intellisense support
- ✅ Industry standard (.NET best practice)

**Migration:**
- EventSystem stays unchanged (still uses ms internally)
- EventRegistry provides TimeSpan wrapper
- Extension methods use TimeSpan
- Entity classes get cleaner, more readable code

This is how production-quality C# code should look!
