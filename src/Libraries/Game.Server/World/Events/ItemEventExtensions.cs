using QuantumCore.Core.Event;
using QuantumCore.Game.Packets;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.World.Events;

/// <summary>
/// Extension methods for ground item events.
/// Hides implementation details from GroundItem class.
/// </summary>
public static class ItemEventExtensions
{
    /// <summary>
    /// Schedule item ownership expiry.
    /// After delay, item becomes public and anyone can pick it up.
    /// Default: 30 seconds.
    /// </summary>
    public static void ScheduleOwnershipExpiry(this GroundItem item, int seconds = 30)
    {
        item.Events.Schedule(
            ItemEventType.OwnershipExpiry,
            () =>
            {
                // Broadcast that item is now public
                item.Map?.BroadcastNearby(item, new ItemOwnership
                {
                    Vid = item.Vid,
                    Player = "" // Empty = public
                });
            },
            seconds * 1000
        );
    }

    /// <summary>
    /// Schedule item disappear from ground.
    /// Default: 5 minutes (300 seconds).
    /// </summary>
    public static void ScheduleItemDisappear(this GroundItem item, int seconds = 300)
    {
        item.Events.Schedule(
            ItemEventType.ItemDisappear,
            () => item.Map?.DespawnEntity(item),
            seconds * 1000
        );
    }

    /// <summary>
    /// Check if item still has owner protection.
    /// </summary>
    public static bool HasOwnerProtection(this GroundItem item)
    {
        return item.Events.IsScheduled(ItemEventType.OwnershipExpiry);
    }
}
