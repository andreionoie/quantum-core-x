# EventRegistry: Decoupled Implementation Like Tickers

## Pattern: Hide Implementation Details

Just like tickers hide their implementation and only expose `Step()`, events should hide scheduling details and only expose trigger methods.

### Ticker Pattern (Reference)

```csharp
public class PlayerEntity : Entity
{
    // Tickers declared as fields
    private readonly GatedTickerEngine<IPlayerEntity> _hpPassiveRestoreTicker;
    private readonly GatedTickerEngine<IPlayerEntity> _spPassiveRestoreTicker;

    public override void Update(double elapsedTime)
    {
        // Clean usage - just call Step(), no implementation details
        var pointsChanged = false;
        var elapsed = TimeSpan.FromMilliseconds(elapsedTime);
        pointsChanged |= _hpPassiveRestoreTicker.Step(elapsed);
        pointsChanged |= _spPassiveRestoreTicker.Step(elapsed);

        if (pointsChanged)
        {
            SendPoints();
        }
    }
}
```

**Why this is clean:**
- ✅ No implementation details in PlayerEntity
- ✅ Just calls `Step()` - ticker knows what to do
- ✅ Ticker encapsulates timing, conditions, and effects

### Event Pattern (Same Approach)

**Extension methods hide implementation details:**

```csharp
// EntityEventExtensions.cs
public static class EntityEventExtensions
{
    public static void ScheduleKnockoutToDeath(this Entity entity)
    {
        entity.Events.Schedule(
            EntityEventType.KnockoutToDeath,
            () => entity.Die(),
            SchedulingConstants.KnockoutToDeathDelaySeconds * 1000
        );
    }

    public static bool IsKnockedOut(this Entity entity)
    {
        return entity.Events.IsScheduled(EntityEventType.KnockoutToDeath);
    }
}
```

**Clean usage in entity:**

```csharp
public class Entity : IEntity
{
    protected readonly EventRegistry Events = new();

    public virtual int Damage(IEntity attacker, EDamageType damageType, int damage)
    {
        // ... damage calculation ...

        if (Health <= 0)
        {
            if (!this.IsKnockedOut())
            {
                this.BroadcastNearby(new KnockoutCharacter { Vid = Vid });
                this.ScheduleKnockoutToDeath(); // Clean - no implementation details!
            }
        }

        return damage;
    }

    public virtual void Move(int x, int y)
    {
        if (this.IsKnockedOut()) // Clean check
            return;

        // ... movement logic
    }

    public override void OnDespawn()
    {
        Events.CancelAll(); // One line
    }
}
```

**Why this is clean:**
- ✅ No delays, callbacks, or event keys in Entity class
- ✅ Just calls `ScheduleKnockoutToDeath()` - extension method knows what to do
- ✅ Extension method encapsulates timing, callback, and event key
- ✅ Same pattern as tickers!

## Entity Class - Before vs After

### Before (Implementation Details Exposed):

```csharp
public class Entity : IEntity
{
    public virtual int Damage(IEntity attacker, EDamageType damageType, int damage)
    {
        if (Health <= 0)
        {
            if (!Events.IsScheduled(EntityEventType.KnockoutToDeath))
            {
                this.BroadcastNearby(new KnockoutCharacter { Vid = Vid });

                // Implementation details exposed:
                Events.Schedule(
                    EntityEventType.KnockoutToDeath,
                    () => Die(),
                    SchedulingConstants.KnockoutToDeathDelaySeconds * 1000
                );
            }
        }
        return damage;
    }

    public virtual void Move(int x, int y)
    {
        // Implementation detail exposed
        if (Events.IsScheduled(EntityEventType.KnockoutToDeath))
            return;
    }
}
```

**Problems:**
- ❌ Event key visible: `EntityEventType.KnockoutToDeath`
- ❌ Delay calculation visible: `SchedulingConstants.KnockoutToDeathDelaySeconds * 1000`
- ❌ Callback visible: `() => Die()`
- ❌ Must remember exact event key for checks

### After (Implementation Details Hidden):

```csharp
public class Entity : IEntity
{
    public virtual int Damage(IEntity attacker, EDamageType damageType, int damage)
    {
        if (Health <= 0)
        {
            if (!this.IsKnockedOut())
            {
                this.BroadcastNearby(new KnockoutCharacter { Vid = Vid });
                this.ScheduleKnockoutToDeath(); // Clean!
            }
        }
        return damage;
    }

    public virtual void Move(int x, int y)
    {
        if (this.IsKnockedOut()) // Clean!
            return;
    }
}
```

**Benefits:**
- ✅ No implementation details visible
- ✅ Semantic method names: `ScheduleKnockoutToDeath()`, `IsKnockedOut()`
- ✅ Implementation can change without touching Entity class
- ✅ Same pattern as tickers

## PlayerEntity Class - Before vs After

### Before (Implementation Details Exposed):

```csharp
public class PlayerEntity : Entity
{
    public override void Die()
    {
        if (Dead) return;
        base.Die();

        // Implementation details exposed
        Events.Schedule(
            EntityEventType.AutoRespawnInTown,
            () => Respawn(true),
            SchedulingConstants.PlayerAutoRespawnDelaySeconds * 1000
        );
    }

    public void Respawn(bool town)
    {
        if (!Dead) return;
        Events.Cancel(EntityEventType.AutoRespawnInTown); // Event key exposed
        // ... respawn logic
    }

    public void StartLogout(bool isInCombat)
    {
        int seconds = isInCombat ? 10 : 3;
        int remaining = seconds;

        // Lots of implementation details
        Events.ScheduleRepeating(
            EntityEventType.LogoutCountdown,
            () =>
            {
                if (remaining <= 0)
                {
                    Connection.Close();
                    return 0;
                }
                SendChatInfo($"{remaining} seconds remaining...");
                remaining--;
                return 1000;
            },
            1000
        );
    }

    public override void Move(int x, int y)
    {
        if (Events.Cancel(EntityEventType.LogoutCountdown))
        {
            SendChatInfo("Logout cancelled.");
        }
        base.Move(x, y);
    }
}
```

### After (Implementation Details Hidden):

```csharp
public class PlayerEntity : Entity
{
    public override void Die()
    {
        if (Dead) return;
        base.Die();

        this.ScheduleAutoRespawn(); // Clean!
    }

    public void Respawn(bool town)
    {
        if (!Dead) return;
        this.CancelAutoRespawn(); // Clean!
        // ... respawn logic
    }

    public void StartLogout(bool isInCombat)
    {
        int seconds = isInCombat ? 10 : 3;
        this.ScheduleLogoutCountdown(seconds); // Clean! Implementation hidden in extension method
    }

    public override void Move(int x, int y)
    {
        this.CancelLogoutCountdown(); // Clean! Sends chat message internally
        base.Move(x, y);
    }
}
```

**Benefits:**
- ✅ No event keys visible
- ✅ No delays or callbacks visible
- ✅ Semantic names: `ScheduleAutoRespawn()`, `CancelLogoutCountdown()`
- ✅ Reads like natural language
- ✅ Same cleanliness as ticker pattern

## GroundItem Class - Before vs After

### Before (Implementation Details Exposed):

```csharp
public class GroundItem : Entity
{
    public GroundItem(/* params */)
    {
        // Implementation details exposed
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
        // Event key exposed
        if (Events.IsScheduled(ItemEventType.OwnershipExpiry) && _ownerName != null)
        {
            return player.Name == _ownerName;
        }
        return true;
    }
}
```

### After (Implementation Details Hidden):

```csharp
public class GroundItem : Entity
{
    public GroundItem(/* params */)
    {
        // Clean - just trigger the events
        if (_ownerName != null)
        {
            this.ScheduleOwnershipExpiry();
        }

        this.ScheduleItemDisappear();
    }

    public bool CanPickup(IPlayerEntity player)
    {
        // Clean semantic check
        if (this.HasOwnerProtection() && _ownerName != null)
        {
            return player.Name == _ownerName;
        }
        return true;
    }
}
```

**Benefits:**
- ✅ Constructor is clean and readable
- ✅ Semantic names: `ScheduleOwnershipExpiry()`, `HasOwnerProtection()`
- ✅ No delays, callbacks, or event keys visible

## Comparison: Tickers vs Events

| Aspect | Tickers | Events (After Refactoring) |
|--------|---------|---------------------------|
| Field | `_hpRestoreTicker` | `Events` (single field) |
| Usage | `ticker.Step(elapsed)` | `this.ScheduleAutoRespawn()` |
| Check | (built into ticker) | `this.IsKnockedOut()` |
| Cleanup | (auto in Update) | `Events.CancelAll()` |
| Implementation | Hidden in ticker class | Hidden in extension methods |
| Readability | ✅ Clean | ✅ Clean |

**Both patterns achieve the same goal:**
- Entity class has no implementation details
- Just calls semantic methods
- Implementation is encapsulated elsewhere

## Benefits of This Approach

1. **Separation of Concerns**
   - Entity classes: business logic only
   - Extension methods: event scheduling details

2. **Maintainability**
   - Change delay? Only edit extension method
   - Change event key? Only edit extension method
   - Entity classes unchanged

3. **Readability**
   - `this.ScheduleAutoRespawn()` reads like natural language
   - `this.IsKnockedOut()` is self-documenting
   - No magic numbers or callbacks

4. **Consistency**
   - Same pattern as tickers
   - Same pattern across all entities
   - Predictable and easy to learn

5. **Testability**
   - Can mock extension methods
   - Can test entity logic without event details
   - Can test event scheduling separately

## Implementation Files

**Core:**
- `EventRegistry.cs` - Instance-based registry (single field)
- `EntityEventType.cs` - Event type enums
- `ItemEventType.cs` - Item event type enums

**Extensions (Hide Implementation):**
- `EntityEventExtensions.cs` - Entity event methods
- `ItemEventExtensions.cs` - Item event methods

**Usage in Entity Classes:**
```csharp
// Entity.cs
protected readonly EventRegistry Events = new();

public virtual int Damage(...)
{
    if (Health <= 0)
    {
        this.ScheduleKnockoutToDeath(); // Clean!
    }
}

// PlayerEntity.cs
public override void Die()
{
    this.ScheduleAutoRespawn(); // Clean!
}

// GroundItem.cs
public GroundItem(...)
{
    this.ScheduleOwnershipExpiry(); // Clean!
    this.ScheduleItemDisappear(); // Clean!
}
```

## Recommendation

**Use extension methods for all standard events:**
- ✅ Entity classes stay clean (like tickers)
- ✅ Implementation details hidden
- ✅ Semantic method names
- ✅ Easy to maintain
- ✅ Easy to test

**This achieves the same level of decoupling as the ticker pattern!**
