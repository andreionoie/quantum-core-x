# EventRegistry Usage Examples

## Design Rationale

Based on reference server analysis, the `EventRegistry` pattern provides:

1. **Single field per entity** - stores all one-shot events in one place
2. **Type-safe enums** - `EntityEventType` and `ItemEventType` for standard events
3. **Static factories** - common patterns like `ScheduleKnockoutToDeath()`
4. **Automatic cleanup** - events self-remove and registry has `CancelAll()`
5. **Query helpers** - `IsKnockedOut`, `IsLoggingOut`, etc.

## Entity Class Integration

### Before (affects branch - manual time tracking):

```csharp
public abstract class Entity : IEntity
{
    // Manual time tracking
    private long? _knockedOutServerTime;

    public virtual void Update(double elapsedTime)
    {
        // Checked every frame for every entity!
        if (!Dead && _knockedOutServerTime.HasValue &&
            GameServer.Instance.ServerTime >= _knockedOutServerTime.Value + KnockoutToDeathDelaySeconds * 1000)
        {
            _knockedOutServerTime = null;
            Die();
        }
        // ... rest of update
    }

    public virtual void Move(int x, int y)
    {
        if (_knockedOutServerTime.HasValue) // Can't move while knocked out
            return;
        // ...
    }
}
```

### After (with EventRegistry):

```csharp
public abstract class Entity : IEntity
{
    // Single field for all events
    protected readonly EventRegistry Events;

    protected Entity(IAnimationManager animationManager, uint vid)
    {
        _animationManager = animationManager;
        Vid = vid;
        Events = new EventRegistry($"Entity:{vid}"); // Debug name
    }

    public virtual void Update(double elapsedTime)
    {
        // No knockout check needed - handled by event system!

        if (State == EEntityState.Moving)
        {
            // ... movement logic
        }
    }

    public virtual void Move(int x, int y)
    {
        if (Events.IsKnockedOut) // Simple property check
            return;
        // ...
    }

    public virtual int Damage(IEntity attacker, EDamageType damageType, int damage)
    {
        // ... damage calculation ...

        if (Health <= 0)
        {
            if (!Events.IsKnockedOut)
            {
                this.BroadcastNearby(new KnockoutCharacter { Vid = Vid });

                // Single line to schedule death
                Events.ScheduleKnockoutToDeath(() => Die());
            }
        }

        return damage;
    }

    // Cleanup on despawn
    public virtual void OnDespawn()
    {
        Events.CancelAll(); // Cancel all pending events
    }
}
```

**Benefits:**
- ✅ No Update() check (eliminates 30,000 checks/second for 500 entities)
- ✅ Clean property check: `Events.IsKnockedOut`
- ✅ One-line scheduling: `Events.ScheduleKnockoutToDeath(() => Die())`
- ✅ Automatic cleanup in OnDespawn

## PlayerEntity Class Integration

### Before (affects branch - manual time tracking):

```csharp
public class PlayerEntity : Entity, IPlayerEntity
{
    private long? _diedAtMs;
    private long? _countdownEventId;

    public override void Update(double elapsedTime)
    {
        if (Map == null) return;

        // Checked every frame!
        if (Dead && _diedAtMs.HasValue)
        {
            var now = GameServer.Instance.ServerTime;
            var deadline = _diedAtMs.Value + SchedulingConstants.PlayerAutoRespawnDelaySeconds * 1000L;
            if (now >= deadline)
            {
                Respawn(true);
            }
        }

        base.Update(elapsedTime);
        // ... ticker updates
    }

    public override void Die()
    {
        if (Dead) return;
        base.Die();
        _diedAtMs = GameServer.Instance.ServerTime; // Manual tracking
    }

    public void Respawn(bool town)
    {
        if (!Dead) return;
        _diedAtMs = null; // Manual cleanup
        // ... respawn logic
    }
}
```

### After (with EventRegistry):

```csharp
public class PlayerEntity : Entity, IPlayerEntity
{
    // No manual time tracking fields needed!

    public override void Update(double elapsedTime)
    {
        if (Map == null) return;

        // No death respawn check needed!

        base.Update(elapsedTime);

        // ... ticker updates
    }

    public override void Die()
    {
        if (Dead) return;
        base.Die();

        // Single line to schedule auto-respawn
        Events.ScheduleAutoRespawnInTown(() => Respawn(true));
    }

    public void Respawn(bool town)
    {
        if (!Dead) return;

        Events.Cancel(EntityEventType.AutoRespawnInTown); // Cancel auto-respawn

        Dead = false;
        // ... respawn logic
    }

    // Logout command handler
    public void StartLogoutCountdown(int seconds)
    {
        Events.ScheduleLogoutCountdown(() =>
        {
            // Logout complete
            Connection.Close();
        }, seconds);

        SendChatInfo($"Logging out in {seconds} seconds...");
    }

    // Cancel logout if player moves
    public override void Move(int x, int y)
    {
        if (Events.CancelLogoutCountdown())
        {
            SendChatInfo("Logout cancelled.");
        }
        base.Move(x, y);
    }
}
```

**Benefits:**
- ✅ No Update() check (eliminates 6,000 checks/second for 100 dead players)
- ✅ One-line scheduling for respawn
- ✅ Built-in logout countdown pattern from reference server
- ✅ Automatic cancellation on manual respawn

## GroundItem Class Integration

### Before (no event system):

```csharp
public class GroundItem : Entity, IGroundItem
{
    private readonly string? _ownerName;
    private long? _spawnedAtMs;
    private long? _ownershipExpiredAtMs;

    public override void Update(double elapsedTime)
    {
        var now = GameServer.Instance.ServerTime;

        // Check ownership expiry
        if (_ownerName != null && !_ownershipExpiredAtMs.HasValue)
        {
            if (now >= _spawnedAtMs + 30000) // 30 seconds
            {
                _ownershipExpiredAtMs = now;
                // Broadcast ownership expired
            }
        }

        // Check item disappear
        if (now >= _spawnedAtMs + 300000) // 5 minutes
        {
            Map?.DespawnEntity(this);
        }
    }

    public bool CanPickup(IPlayerEntity player)
    {
        if (_ownerName != null && !_ownershipExpiredAtMs.HasValue)
        {
            return player.Name == _ownerName;
        }
        return true;
    }
}
```

### After (with EventRegistry):

```csharp
public class GroundItem : Entity, IGroundItem
{
    private readonly string? _ownerName;

    public GroundItem(IAnimationManager animationManager, uint vid, ItemInstance item,
                      uint amount, string? ownerName = null)
        : base(animationManager, vid)
    {
        _item = item;
        _amount = amount;
        _ownerName = ownerName;

        // Schedule events on creation
        if (_ownerName != null)
        {
            Events.ScheduleItemOwnershipExpiry(() =>
            {
                // Broadcast that item is now public
                Map?.BroadcastNearby(this, new ItemOwnership { Vid = Vid, Player = "" });
            });
        }

        Events.ScheduleItemDisappear(() =>
        {
            // Remove from world
            Map?.DespawnEntity(this);
        });
    }

    public override void Update(double elapsedTime)
    {
        // No checks needed - events handle everything!
    }

    public bool CanPickup(IPlayerEntity player)
    {
        // Simple property check
        if (_ownerName != null && Events.HasOwnerProtection)
        {
            return player.Name == _ownerName;
        }
        return true;
    }
}
```

**Benefits:**
- ✅ No Update() checks at all
- ✅ Events scheduled on construction
- ✅ Clean property check: `Events.HasOwnerProtection`
- ✅ Automatic despawn after timeout

## Custom Event Keys

For entity-specific events not covered by the enums:

```csharp
public class MonsterEntity : Entity
{
    private enum MonsterEvent
    {
        AggroTimeout,
        SpecialAttackCooldown,
        RoamingDelay
    }

    public void StartAggroTimeout()
    {
        Events.Schedule(
            MonsterEvent.AggroTimeout,
            () => Target = null, // Clear target
            15000 // 15 seconds
        );
    }

    public void UseSpecialAttack()
    {
        // Perform attack...

        // Schedule cooldown
        Events.Schedule(
            MonsterEvent.SpecialAttackCooldown,
            () => _specialAttackReady = true,
            30000 // 30 seconds
        );
    }
}
```

## Party Invite Events (Multi-key Pattern)

Reference server has `m_PartyInviteEventMap` - map of events per invited player:

```csharp
public class PlayerEntity : Entity
{
    // For tracking multiple invites to different players
    private readonly Dictionary<Guid, EventRegistry> _partyInvites = new();

    public void InviteToParty(IPlayerEntity target)
    {
        var registry = new EventRegistry($"PartyInvite:{target.Player.Id}");

        registry.Schedule(
            EntityEventType.PartyInviteTimeout,
            () =>
            {
                // Invite expired
                SendChatInfo($"Party invite to {target.Name} expired.");
                _partyInvites.Remove(target.Player.Id);
            },
            10000 // 10 seconds
        );

        _partyInvites[target.Player.Id] = registry;
    }

    public void CancelPartyInvite(Guid playerId)
    {
        if (_partyInvites.TryGetValue(playerId, out var registry))
        {
            registry.CancelAll();
            _partyInvites.Remove(playerId);
        }
    }

    public override void OnDespawn()
    {
        // Cancel all pending party invites
        foreach (var registry in _partyInvites.Values)
        {
            registry.CancelAll();
        }
        _partyInvites.Clear();

        base.OnDespawn();
    }
}
```

## Performance Comparison

### Current (affects branch):

```
Update() checks per second at 60 FPS:
- 500 entities × 60 = 30,000 knockout checks
- 100 dead players × 60 = 6,000 respawn checks
- 200 ground items × 60 = 12,000 ownership + despawn checks
-----------------------------------------------------------
Total: 48,000 unnecessary checks per second
```

### With EventRegistry:

```
Update() checks per second:
- 0 (events fire only when scheduled)
-----------------------------------------------------------
Total: 0 checks (events handled by EventSystem)
```

**Performance Improvement:** Eliminates 48,000 checks per second!

## Code Comparison Summary

| Aspect | Before (nullable long) | After (EventRegistry) |
|--------|------------------------|----------------------|
| Fields per entity | 2-3 nullable longs | 1 EventRegistry |
| Lines to schedule | 6+ lines | 1 line |
| Lines to cancel | 3 lines | 1 line |
| Update() checks | Multiple | Zero |
| Null safety | Manual | Automatic |
| Cleanup on despawn | Manual per field | `Events.CancelAll()` |
| Type safety | ❌ All `long?` | ✅ Typed enums |
| Query state | Manual null check | Property: `IsKnockedOut` |

## Migration Path

1. ✅ Add `EventRegistry` and enum types
2. ✅ Add `protected readonly EventRegistry Events` to Entity base class
3. ✅ Refactor knockout-to-death (Entity.cs)
4. ✅ Refactor auto-respawn (PlayerEntity.cs)
5. ✅ Refactor logout countdown (PlayerEntity.cs)
6. ✅ Refactor ground item timeouts (GroundItem.cs)
7. ✅ Remove manual time tracking fields
8. ✅ Remove Update() checks
9. ✅ Add `Events.CancelAll()` to OnDespawn methods

This maintains all the performance benefits of the ticker system while providing ergonomic one-shot event management!
