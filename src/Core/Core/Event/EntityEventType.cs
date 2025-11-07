namespace QuantumCore.Core.Event;

/// <summary>
/// Standard event types for entities based on reference server patterns.
/// Used as keys in EventRegistry to avoid field bloat in entity classes.
/// </summary>
public enum EntityEventType
{
    /// <summary>
    /// Knockout-to-death transition delay (3 seconds default)
    /// Reference: char_battle.cpp StunEvent pattern
    /// </summary>
    KnockoutToDeath,

    /// <summary>
    /// Auto-respawn in town after death (10 seconds default)
    /// Reference: char.cpp respawn logic
    /// </summary>
    AutoRespawnInTown,

    /// <summary>
    /// Logout/quit/character select countdown (3-10 seconds)
    /// Reference: cmd_general.cpp timed_event
    /// </summary>
    LogoutCountdown,

    /// <summary>
    /// Warp transition delay
    /// Reference: char.cpp m_pkWarpEvent
    /// </summary>
    WarpTransition,

    /// <summary>
    /// Stun effect duration
    /// Reference: char_battle.cpp m_pkStunEvent
    /// </summary>
    StunDuration,

    /// <summary>
    /// Mining activity timeout
    /// Reference: char.cpp m_pkMiningEvent
    /// </summary>
    MiningTimeout,

    /// <summary>
    /// Fishing activity timeout
    /// Reference: char.cpp m_pkFishingEvent
    /// </summary>
    FishingTimeout,

    /// <summary>
    /// Party invite request timeout
    /// Reference: char.cpp m_pkPartyRequestEvent
    /// </summary>
    PartyInviteTimeout,

    /// <summary>
    /// Periodic save event
    /// Reference: char.cpp m_pkSaveEvent
    /// </summary>
    PeriodicSave,

    /// <summary>
    /// Recovery event (HP/SP regen)
    /// Reference: char.cpp m_pkRecoveryEvent
    /// </summary>
    Recovery,

    /// <summary>
    /// Check speed hack event
    /// Reference: char.cpp m_pkCheckSpeedHackEvent
    /// </summary>
    CheckSpeedHack,

    /// <summary>
    /// Destroy entity when idle (for mobs)
    /// Reference: char.cpp m_pkDestroyWhenIdleEvent
    /// </summary>
    DestroyWhenIdle,
}

/// <summary>
/// Standard event types for ground items.
/// </summary>
public enum ItemEventType
{
    /// <summary>
    /// Item ownership expires and becomes public (30 seconds default)
    /// </summary>
    OwnershipExpiry,

    /// <summary>
    /// Item disappears from ground (5 minutes default)
    /// </summary>
    ItemDisappear,
}
