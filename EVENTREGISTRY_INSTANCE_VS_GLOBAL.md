# EventRegistry: Instance vs Global - Best Approach

## Three Approaches Compared

### 1. Multiple EventHandle Fields (Field Bloat)
```csharp
public class PlayerEntity : Entity
{
    private readonly EventHandle _knockoutEvent = new();       // 24 bytes
    private readonly EventHandle _autoRespawnEvent = new();    // 24 bytes
    private readonly EventHandle _logoutEvent = new();         // 24 bytes
    // ... 5+ more fields
    // Total: 192+ bytes
}
```

**Problems:**
- ❌ Field bloat (8+ fields)
- ❌ Must remember to cancel each in OnDespawn
- ❌ Gets worse as more events are added

### 2. Global EventRegistry Service
```csharp
// Global dictionary
Dictionary<(object Entity, object EventKey), long> _entityEvents

// Usage
EventRegistry.Schedule(this, EntityEventType.KnockoutToDeath, callback, delay);
```

**Problems:**
- ❌ Entity references as dictionary keys (GC pressure)
- ❌ Must pass `this` to every call
- ❌ Less encapsulated
- ❌ Global state harder to reason about

### 3. EventRegistry Instance Field ✅ (BEST)
```csharp
public abstract class Entity : IEntity
{
    // Single field per entity
    protected readonly EventRegistry Events = new();  // ~8 bytes pointer + internal dict
}

// Usage
Events.Schedule(EntityEventType.KnockoutToDeath, callback, delay);
```

**Benefits:**
- ✅ Single field (8 byte pointer + internal dictionary only if events exist)
- ✅ No entity references as keys
- ✅ Better encapsulation
- ✅ Cleaner API (no passing `this`)
- ✅ Automatic GC when entity disposed
- ✅ One-line cleanup: `Events.CancelAll()`

## Entity Implementation

```csharp
public abstract class Entity : IEntity
{
    private readonly IAnimationManager _animationManager;

    // Single event registry field
    protected readonly EventRegistry Events = new();

    // ... rest of entity fields

    protected Entity(IAnimationManager animationManager, uint vid)
    {
        _animationManager = animationManager;
        Vid = vid;
    }

    public virtual void Move(int x, int y)
    {
        // Clean API - no passing 'this'
        if (Events.IsScheduled(EntityEventType.KnockoutToDeath))
            return;

        PositionX = x;
        PositionY = y;
        PositionChanged = true;
    }

    public virtual int Damage(IEntity attacker, EDamageType damageType, int damage)
    {
        // ... damage calculation ...

        if (Health <= 0)
        {
            if (!Events.IsScheduled(EntityEventType.KnockoutToDeath))
            {
                this.BroadcastNearby(new KnockoutCharacter { Vid = Vid });

                // Single line, clean API
                Events.Schedule(
                    EntityEventType.KnockoutToDeath,
                    () => Die(),
                    SchedulingConstants.KnockoutToDeathDelaySeconds * 1000
                );
            }
        }

        return damage;
    }

    public virtual void Die()
    {
        Dead = true;
        Events.Cancel(EntityEventType.KnockoutToDeath);
        // ... death logic
    }

    public override void OnDespawn()
    {
        Events.CancelAll(); // One line cancels all events
    }
}
```

## PlayerEntity Implementation

```csharp
public class PlayerEntity : Entity, IPlayerEntity
{
    // Inherits Events from base class - no additional fields needed!

    public override void Die()
    {
        if (Dead) return;
        base.Die();

        // Clean, readable API
        Events.Schedule(
            EntityEventType.AutoRespawnInTown,
            () => Respawn(true),
            SchedulingConstants.PlayerAutoRespawnDelaySeconds * 1000
        );
    }

    public void Respawn(bool town)
    {
        if (!Dead) return;

        Events.Cancel(EntityEventType.AutoRespawnInTown);

        Dead = false;
        // ... respawn logic
    }

    public void StartLogout(bool isInCombat)
    {
        int seconds = isInCombat ? 10 : 3;
        int remaining = seconds;

        SendChatInfo($"Logging out in {seconds} seconds...");

        Events.ScheduleRepeating(
            EntityEventType.LogoutCountdown,
            () =>
            {
                if (remaining <= 0)
                {
                    Connection.Close();
                    return 0; // Stop
                }
                SendChatInfo($"{remaining} seconds remaining...");
                remaining--;
                return 1000; // Continue
            },
            1000
        );
    }

    public void CancelLogout()
    {
        if (Events.Cancel(EntityEventType.LogoutCountdown))
        {
            SendChatInfo("Logout cancelled.");
        }
    }

    public override void Move(int x, int y)
    {
        CancelLogout();
        base.Move(x, y);
    }

    public override void OnDespawn()
    {
        Events.CancelAll(); // One line!
        base.OnDespawn();
    }

    public void Dispose()
    {
        Events.CancelAll();
        _scope.Dispose();
    }
}
```

## GroundItem Implementation

```csharp
public class GroundItem : Entity, IGroundItem
{
    // Inherits Events from base class

    public GroundItem(IAnimationManager animationManager, uint vid,
                      ItemInstance item, uint amount, string? ownerName = null)
        : base(animationManager, vid)
    {
        _item = item;
        _amount = amount;
        _ownerName = ownerName;

        // Schedule events on construction
        if (_ownerName != null)
        {
            Events.Schedule(
                ItemEventType.OwnershipExpiry,
                () =>
                {
                    _ownerName = null;
                    Map?.BroadcastNearby(this, new ItemOwnership
                    {
                        Vid = Vid,
                        Player = ""
                    });
                },
                30_000
            );
        }

        Events.Schedule(
            ItemEventType.ItemDisappear,
            () => Map?.DespawnEntity(this),
            300_000
        );
    }

    public bool CanPickup(IPlayerEntity player)
    {
        if (Events.IsScheduled(ItemEventType.OwnershipExpiry) && _ownerName != null)
        {
            return player.Name == _ownerName;
        }
        return true;
    }

    public override void OnDespawn()
    {
        Events.CancelAll();
    }
}
```

## Custom Events for Monsters

```csharp
public class MonsterEntity : Entity
{
    // Custom enum for monster-specific events
    private enum MonsterEvent
    {
        AggroTimeout,
        SpecialAttackCooldown,
        RoamingDelay
    }

    public void SetTarget(IEntity? target)
    {
        if (target != null)
        {
            Target = target;

            Events.Schedule(
                MonsterEvent.AggroTimeout,
                () => Target = null,
                15_000
            );
        }
        else
        {
            Target = null;
            Events.Cancel(MonsterEvent.AggroTimeout);
        }
    }

    public void UseSpecialAttack()
    {
        // Perform attack...

        Events.Schedule(
            MonsterEvent.SpecialAttackCooldown,
            () => _canUseSpecialAttack = true,
            30_000
        );
    }

    public override void OnDespawn()
    {
        Events.CancelAll(); // Cancels all monster events
        base.OnDespawn();
    }
}
```

## Dynamic Events: Party Invites

For dynamic events (one per invited player), use composite keys:

```csharp
public class PlayerEntity : Entity
{
    public void InviteToParty(IPlayerEntity target)
    {
        var inviteKey = $"PartyInvite:{target.Player.Id}";

        Events.Schedule(
            inviteKey,
            () => SendChatInfo($"Party invite to {target.Name} expired."),
            10_000
        );
    }

    public void AcceptPartyInvite(Guid inviterId)
    {
        var inviteKey = $"PartyInvite:{inviterId}";
        Events.Cancel(inviteKey);
    }

    // OnDespawn still just one line - cancels all party invites too
    public override void OnDespawn()
    {
        Events.CancelAll();
        base.OnDespawn();
    }
}
```

## Memory Comparison

| Approach | Per Entity | Notes |
|----------|------------|-------|
| Multiple EventHandle fields | 192+ bytes | 8+ fields × 24 bytes |
| Global EventRegistry | 0 bytes | But global dict with entity refs as keys |
| **EventRegistry instance** | **~8-16 bytes** | **Pointer + empty dict overhead** |

**With events scheduled:**
- EventRegistry instance: 8 bytes (pointer) + 40 bytes per event in internal dict
- Global EventRegistry: 40 bytes per event in global dict + entity reference key

**EventRegistry instance is better because:**
- No entity references as dictionary keys (better for GC)
- Dictionary is garbage collected when entity is disposed
- More encapsulated

## API Comparison

| Operation | Global EventRegistry | EventRegistry Instance |
|-----------|---------------------|----------------------|
| Schedule | `EventRegistry.Schedule(this, key, ...)` | `Events.Schedule(key, ...)` |
| Cancel | `EventRegistry.Cancel(this, key)` | `Events.Cancel(key)` |
| Check | `EventRegistry.IsScheduled(this, key)` | `Events.IsScheduled(key)` |
| Cleanup | `EventRegistry.CancelAll(this)` | `Events.CancelAll()` |

**Instance API is cleaner:**
- ✅ No need to pass `this`
- ✅ Shorter, more readable
- ✅ Better encapsulation

## GC Considerations

**Global EventRegistry:**
```csharp
Dictionary<(object Entity, object EventKey), long>
```
- Entity reference in key prevents GC until event is cleaned up
- If cleanup fails, entity can't be GC'd (memory leak)

**EventRegistry Instance:**
```csharp
class Entity {
    EventRegistry Events = new();
}
```
- When entity is disposed, EventRegistry is GC'd automatically
- Internal dictionary is GC'd with it
- No entity references as keys
- More GC-friendly

## Exception Safety

**Both approaches:**
```csharp
var eventId = EventSystem.EnqueueEvent(() =>
{
    callback(); // User code - might throw
    _events.Remove(key); // Cleanup
    return 0;
}, delayMs);
```

**If callback throws:**
- EventSystem marks event as complete (won't fire again)
- `_events.Remove(key)` doesn't run → stale entry
- Instance approach: Cleaned up when entity disposed
- Global approach: Stale entry with entity reference (worse)

**Instance approach is safer** because stale entries don't hold entity references.

## Recommendation

**Use EventRegistry instance field:**

```csharp
public abstract class Entity : IEntity
{
    // Single field - clean and simple
    protected readonly EventRegistry Events = new();

    public override void OnDespawn()
    {
        Events.CancelAll();
    }
}
```

**Benefits over global approach:**
- ✅ Cleaner API (no passing `this`)
- ✅ Better encapsulation
- ✅ No entity references as keys
- ✅ Automatic GC when entity disposed
- ✅ More GC-friendly
- ✅ Exception-safe (stale entries cleaned on entity disposal)

**Still avoids field bloat:**
- 1 field instead of 8+
- 1 line cleanup instead of 8+
- Same ergonomics as global approach

**This is the best solution!**
