namespace QuantumCore.Game.World.Events;

/// <summary>
/// Standard event types for entities based on reference server patterns.
/// Each entity type gets a dedicated key to avoid conflicts.
/// </summary>
public enum EntityEventType
{
    /// <summary>
    /// Knockout-to-death transition delay (3 seconds default)
    /// </summary>
    KnockoutToDeath,

    /// <summary>
    /// Auto-respawn in town after death (default 10 seconds)
    /// </summary>
    AutoRespawnInTown,

    /// <summary>
    /// Logout/quit/character select countdown (3-10 seconds)
    /// </summary>
    LogoutCountdown,

    /// <summary>
    /// Warp transition delay
    /// </summary>
    WarpTransition,

    /// <summary>
    /// Stun effect duration
    /// </summary>
    StunDuration,

    /// <summary>
    /// Mining activity timeout
    /// </summary>
    MiningTimeout,

    /// <summary>
    /// Fishing activity timeout
    /// </summary>
    FishingTimeout,

    /// <summary>
    /// Party invite request timeout
    /// </summary>
    PartyInviteTimeout,
}

/// <summary>
/// Standard event types for ground items.
/// </summary>
public enum ItemEventType
{
    /// <summary>
    /// Item ownership expires and becomes public (default 30 seconds)
    /// </summary>
    OwnershipExpiry,

    /// <summary>
    /// Item disappears from ground (default 5 minutes)
    /// </summary>
    ItemDisappear,
}
