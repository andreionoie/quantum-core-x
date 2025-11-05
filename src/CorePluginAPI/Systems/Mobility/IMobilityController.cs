namespace QuantumCore.API.Systems.Mobility;

public interface IMobilityController
{
    EMobilityMode PreferredMode { get; }
    EMobilityOverride OverrideMode { get; }
    EMobilityMode ActiveMode { get; }

    long LastWalkStartAt { get; }
    long LastStopAt { get; }
    long LastRunStartAt { get; }
    long LastMovementAt { get; }

    bool IsCurrentlyWalking { get; }
    bool IsForcedToWalk { get; }

    event Action<EMobilityMode, EMobilityMode>? ActiveModeChanged;

    void SetPreferredMode(EMobilityMode mode, long timestamp);
    void StartMoving(long timestamp);
    void StopMoving(long timestamp);
    void ForceWalk(long timestamp);
    void ClearForceWalk(long timestamp);
}
