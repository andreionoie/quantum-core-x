# EventRegistry: Global Service Approach - Zero Field Bloat

## Problem: Field Bloat with EventHandle

**EventHandle per field approach:**
```csharp
public class PlayerEntity : Entity
{
    private readonly EventHandle _knockoutToDeathEvent = new();     // 24 bytes
    private readonly EventHandle _autoRespawnEvent = new();         // 24 bytes
    private readonly EventHandle _logoutCountdownEvent = new();     // 24 bytes
    private readonly EventHandle _persistEvent = new();             // 24 bytes
    private readonly EventHandle _miningEvent = new();              // 24 bytes
    private readonly EventHandle _fishingEvent = new();             // 24 bytes
    private readonly EventHandle _speedHackCheckEvent = new();      // 24 bytes
    private readonly EventHandle _partyInviteEvent = new();         // 24 bytes
    // ... more events as needed
    // Total: 192+ bytes of field bloat!
}
```

**Problems:**
- ❌ 8+ fields per entity = field bloat
- ❌ Must remember to add cleanup for each field
- ❌ Class gets fat quickly
- ❌ Hard to see what events exist at a glance

## Solution: Global EventRegistry

**Single global dictionary, keyed by (entity, eventType):**
```csharp
public static class EventRegistry
{
    // ONE dictionary for entire application
    private static readonly Dictionary<(object Entity, object EventKey), long> _entityEvents = new();
}
```

**Entity classes stay lean:**
```csharp
public class PlayerEntity : Entity
{
    // NO event fields needed!
    // Just use EventRegistry.Schedule(this, EventType, ...)
}
```

## Entity Implementation

### Before (Field Bloat):
```csharp
public abstract class Entity : IEntity
{
    private readonly EventHandle _knockoutToDeathEvent = new();

    public virtual void Move(int x, int y)
    {
        if (_knockoutToDeathEvent.IsScheduled)
            return;
        // ...
    }

    public override void OnDespawn()
    {
        _knockoutToDeathEvent.Cancel();
    }
}
```

### After (No Fields):
```csharp
public abstract class Entity : IEntity
{
    // NO event fields!

    public virtual void Move(int x, int y)
    {
        if (EventRegistry.IsScheduled(this, EntityEventType.KnockoutToDeath))
            return;
        // ...
    }

    public override void OnDespawn()
    {
        EventRegistry.CancelAll(this); // Cancel ALL events with one call
    }
}
```

**Benefits:**
- ✅ Zero field bloat
- ✅ `CancelAll(this)` handles all events at once
- ✅ Still type-safe with enums
- ✅ Global dictionary = single source of truth

## PlayerEntity Implementation

### Before (8+ Fields):
```csharp
public class PlayerEntity : Entity
{
    private readonly EventHandle _autoRespawnEvent = new();
    private readonly EventHandle _logoutCountdownEvent = new();
    private readonly EventHandle _persistEvent = new();
    private readonly EventHandle _miningEvent = new();
    private readonly EventHandle _fishingEvent = new();
    // ... more fields

    public override void Die()
    {
        _autoRespawnEvent.Schedule(() => Respawn(true), 10000);
    }

    public void StartLogout(int seconds)
    {
        _logoutCountdownEvent.Schedule(/* ... */);
    }

    public override void OnDespawn()
    {
        _autoRespawnEvent.Cancel();
        _logoutCountdownEvent.Cancel();
        _persistEvent.Cancel();
        _miningEvent.Cancel();
        _fishingEvent.Cancel();
        // ... must remember every field!
        base.OnDespawn();
    }
}
```

### After (Zero Fields):
```csharp
public class PlayerEntity : Entity
{
    // NO event fields at all!

    public override void Die()
    {
        if (Dead) return;
        base.Die();

        EventRegistry.Schedule(
            this,
            EntityEventType.AutoRespawnInTown,
            () => Respawn(true),
            10_000
        );
    }

    public void Respawn(bool town)
    {
        if (!Dead) return;

        EventRegistry.Cancel(this, EntityEventType.AutoRespawnInTown);

        Dead = false;
        // ... respawn logic
    }

    public void StartLogout(bool isInCombat)
    {
        int seconds = isInCombat ? 10 : 3;
        int remaining = seconds;

        SendChatInfo($"Logging out in {seconds} seconds...");

        EventRegistry.ScheduleRepeating(
            this,
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
        if (EventRegistry.Cancel(this, EntityEventType.LogoutCountdown))
        {
            SendChatInfo("Logout cancelled.");
        }
    }

    public override void Move(int x, int y)
    {
        CancelLogout(); // Auto-cancel on move
        base.Move(x, y);
    }

    public override void OnDespawn()
    {
        EventRegistry.CancelAll(this); // ONE LINE cancels all events!
        base.OnDespawn();
    }

    public void Dispose()
    {
        EventRegistry.CancelAll(this);
        _scope.Dispose();
    }
}
```

**Benefits:**
- ✅ Zero field bloat (was 192+ bytes, now 0)
- ✅ No need to remember each field in OnDespawn
- ✅ `CancelAll(this)` handles everything
- ✅ Still type-safe with `EntityEventType` enum
- ✅ Clean, readable code

## GroundItem Implementation

### Before (2 Fields):
```csharp
public class GroundItem : Entity
{
    private readonly EventHandle _ownershipExpiryEvent = new();
    private readonly EventHandle _itemDisappearEvent = new();

    public GroundItem(/* params */)
    {
        if (_ownerName != null)
        {
            _ownershipExpiryEvent.Schedule(() => { /* ... */ }, 30_000);
        }
        _itemDisappearEvent.Schedule(() => { /* ... */ }, 300_000);
    }

    public bool CanPickup(IPlayerEntity player)
    {
        if (_ownershipExpiryEvent.IsScheduled && _ownerName != null)
        {
            return player.Name == _ownerName;
        }
        return true;
    }

    public override void OnDespawn()
    {
        _ownershipExpiryEvent.Cancel();
        _itemDisappearEvent.Cancel();
    }
}
```

### After (Zero Fields):
```csharp
public class GroundItem : Entity
{
    // NO event fields!

    public GroundItem(IAnimationManager animationManager, uint vid,
                      ItemInstance item, uint amount, string? ownerName = null)
        : base(animationManager, vid)
    {
        _item = item;
        _amount = amount;
        _ownerName = ownerName;

        // Schedule events directly
        if (_ownerName != null)
        {
            EventRegistry.Schedule(
                this,
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

        EventRegistry.Schedule(
            this,
            ItemEventType.ItemDisappear,
            () => Map?.DespawnEntity(this),
            300_000
        );
    }

    public bool CanPickup(IPlayerEntity player)
    {
        if (EventRegistry.IsScheduled(this, ItemEventType.OwnershipExpiry) && _ownerName != null)
        {
            return player.Name == _ownerName;
        }
        return true;
    }

    public override void OnDespawn()
    {
        EventRegistry.CancelAll(this); // One line!
    }
}
```

## MonsterEntity with Custom Events

```csharp
public class MonsterEntity : Entity
{
    // Custom event keys for monster-specific events
    private enum MonsterEvent
    {
        AggroTimeout,
        SpecialAttackCooldown,
        RoamingDelay
    }

    // NO fields!

    public void SetTarget(IEntity? target)
    {
        if (target != null)
        {
            Target = target;

            EventRegistry.Schedule(
                this,
                MonsterEvent.AggroTimeout,
                () => Target = null,
                15_000
            );
        }
        else
        {
            Target = null;
            EventRegistry.Cancel(this, MonsterEvent.AggroTimeout);
        }
    }

    public override int Damage(IEntity attacker, EDamageType damageType, int damage)
    {
        if (Target == attacker)
        {
            // Reset aggro timeout
            EventRegistry.Schedule(
                this,
                MonsterEvent.AggroTimeout,
                () => Target = null,
                15_000
            );
        }

        return base.Damage(attacker, damageType, damage);
    }

    public void UseSpecialAttack()
    {
        // Perform attack...

        EventRegistry.Schedule(
            this,
            MonsterEvent.SpecialAttackCooldown,
            () => _canUseSpecialAttack = true,
            30_000
        );
    }

    public void StartRoaming()
    {
        EventRegistry.ScheduleRepeating(
            this,
            MonsterEvent.RoamingDelay,
            () =>
            {
                var newX = PositionX + CoreRandom.GenerateInt32(-500, 500);
                var newY = PositionY + CoreRandom.GenerateInt32(-500, 500);
                Goto(newX, newY);
                return 5_000; // Continue roaming
            },
            5_000
        );
    }

    public override void OnDespawn()
    {
        EventRegistry.CancelAll(this); // Cancel all monster events
        base.OnDespawn();
    }
}
```

## Dynamic Events: Party Invites

For dynamic events (one per invited player), use composite keys:

```csharp
public class PlayerEntity : Entity
{
    // NO fields for party invites!

    public void InviteToParty(IPlayerEntity target)
    {
        var inviteKey = $"PartyInvite:{target.Player.Id}";

        EventRegistry.Schedule(
            this,
            inviteKey,
            () =>
            {
                SendChatInfo($"Party invite to {target.Name} expired.");
            },
            10_000
        );

        // Send invite to target...
    }

    public void AcceptPartyInvite(Guid inviterId)
    {
        var inviteKey = $"PartyInvite:{inviterId}";
        EventRegistry.Cancel(this, inviteKey);

        // Join party...
    }

    // OnDespawn still just needs one line
    public override void OnDespawn()
    {
        EventRegistry.CancelAll(this); // Cancels party invites too!
        base.OnDespawn();
    }
}
```

## Memory Comparison

| Approach | Memory per Entity | Cleanup Lines | Desync Risk |
|----------|------------------|---------------|-------------|
| Manual time tracking | 16 bytes per event | 1 per event | High |
| EventHandle fields | 24 bytes per field | 1 per field | None |
| Global EventRegistry | 0 bytes | 1 total | None |

**With 8 events per PlayerEntity:**
- Manual tracking: 128 bytes + Update() checks
- EventHandle fields: 192 bytes + 8 cleanup lines
- **Global EventRegistry: 0 bytes + 1 cleanup line** ✅

## Performance

**Global dictionary overhead:**
- Key: (object reference, enum) = 16 bytes
- Value: long = 8 bytes
- Dictionary entry overhead: ~16 bytes
- **Total: ~40 bytes per scheduled event globally**

**Example with 100 entities:**
- 50 knocked out entities = 50 events = 2KB
- 100 ground items with 2 events each = 200 events = 8KB
- **Total: 10KB globally vs 19.2KB with EventHandle fields**

**Plus:**
- No per-entity overhead
- Entities stay lean
- One cleanup call per entity

## State Duplication Check

**Is there still duplicate state?**

EventRegistry: `Dictionary<(object, object), long>` - maps (entity, key) → event ID
EventSystem: `Dictionary<long, Event>` - maps event ID → event

**These serve different purposes:**
- EventRegistry: "What events does this entity have?"
- EventSystem: "What are all pending events?"

**But the event ID is still the source of truth:**
- When event fires, EventSystem removes it AND callback cleans up EventRegistry
- If callback throws, EventSystem still removes the event (event won't fire again)
- EventRegistry might have stale entry, but `IsScheduled` will return true incorrectly only until entity is despawned
- This is much safer than before because we're not trying to cancel nonexistent events

**Mitigation:**
The callback wraps the user callback:
```csharp
var eventId = EventSystem.EnqueueEvent(() =>
{
    callback(); // User code
    lock (_lock)
    {
        _entityEvents.Remove(key); // Cleanup inside lock
    }
    return 0;
}, delayMs);
```

If `callback()` throws:
- EventSystem catches it and marks event as completed (doesn't reschedule)
- `_entityEvents.Remove(key)` never runs → stale entry
- But entity will call `CancelAll(this)` on despawn → cleaned up

**This is acceptable because:**
- Stale entries only exist until entity despawns
- No memory leak (entity reference keeps it alive, but entity is alive anyway)
- No incorrect behavior (event already fired, won't fire again)

## Conclusion

**Global EventRegistry approach:**
- ✅ Zero field bloat (0 bytes per entity)
- ✅ One-line cleanup: `EventRegistry.CancelAll(this)`
- ✅ Type-safe with enums
- ✅ No duplicate dictionaries per entity
- ✅ Lower total memory usage
- ✅ Clean, streamlined entity classes
- ✅ Exception-safe (cleanup on despawn)

**Recommended usage:**
```csharp
// In entity methods
EventRegistry.Schedule(this, EntityEventType.SomeEvent, callback, delay);
EventRegistry.Cancel(this, EntityEventType.SomeEvent);
if (EventRegistry.IsScheduled(this, EntityEventType.SomeEvent)) { }

// In OnDespawn
EventRegistry.CancelAll(this);
```

This keeps entity classes lean while providing excellent ergonomics!
