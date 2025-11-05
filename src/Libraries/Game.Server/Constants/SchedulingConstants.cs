namespace QuantumCore.Game.Constants;

public static class SchedulingConstants
{
    public const int PersistInterval = 30 * 1000;
    public const int PassiveRegenIntervalMs = 3000;
    public const int AffectsUpdateIntervalMs = 1000;
    public const int FlameSpiritHitRateMs = 3000;
    public const int PoisonTickIntervalMs = 3000;

    public const int ImmunitySuccessPercentage = 90;
    
    public const int DefaultPoisonDurationSeconds = 30;
    public const int DefaultSlowDurationSeconds = 30;
    public const int DefaultStunDurationSeconds = 2;
    public const int PvmStunDurationSeconds = 4;

    public const int DefaultMovementDebuffValue = -30;

    public const int GroundItemOwnershipLockSeconds = 30;
    public const int GroundItemLifetimeSeconds = 300;   

    public const int PlayerAutoRespawnDelaySeconds = 180;

    // Death flow
    public const int KnockoutToDeathDelaySeconds = 3; // time between knockdown and death broadcast
}
