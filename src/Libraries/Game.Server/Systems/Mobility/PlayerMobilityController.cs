using QuantumCore.API.Systems.Mobility;

namespace QuantumCore.Game.Systems.Mobility;

public sealed class PlayerMobilityController : IMobilityController
{
    private bool _isMoving;

    public PlayerMobilityController()
    {
        var now = GameServer.Instance.ServerTime;
        LastWalkStartAt = now;
        LastRunStartAt = now;
        LastStopAt = now;
        LastMovementAt = now;
        _isMoving = false;
    }

    public EMobilityMode PreferredMode { get; private set; } = EMobilityMode.Run;

    public EMobilityOverride OverrideMode { get; private set; } = EMobilityOverride.None;

    public EMobilityMode ActiveMode => OverrideMode == EMobilityOverride.ForceWalk
        ? EMobilityMode.Walk
        : PreferredMode;

    public long LastWalkStartAt { get; private set; }

    public long LastStopAt { get; private set; }

    public long LastRunStartAt { get; private set; }

    // last mobility-related input (any movement/idle/skill command)
    public long LastMovementAt { get; private set; }

    public bool IsCurrentlyWalking => ActiveMode == EMobilityMode.Walk;
    public bool IsForcedToWalk => OverrideMode == EMobilityOverride.ForceWalk;

    public event Action<EMobilityMode, EMobilityMode>? ActiveModeChanged;

    public void SetPreferredMode(EMobilityMode mode, long timestamp)
    {
        if (PreferredMode == mode)
        {
            return;
        }

        var oldActive = ActiveMode;
        PreferredMode = mode;

        if (_isMoving && oldActive != ActiveMode)
        {
            if (ActiveMode == EMobilityMode.Walk)
            {
                LastWalkStartAt = timestamp;
            }
            else
            {
                LastRunStartAt = timestamp;
            }
        }

        LastMovementAt = timestamp;
        NotifyIfActiveModeChanged(oldActive);
    }

    public void StartMoving(long timestamp)
    {
        var wasMoving = _isMoving;
        var oldActive = ActiveMode;

        _isMoving = true;
        LastMovementAt = timestamp; // refresh activity even during continuous movement

        if (!wasMoving)
        {
            if (ActiveMode == EMobilityMode.Walk)
            {
                LastWalkStartAt = timestamp;
            }
            else
            {
                LastRunStartAt = timestamp;
            }
        }
        else if (oldActive != ActiveMode)
        {
            // mode changed while moving (force-walk or command toggle)
            if (ActiveMode == EMobilityMode.Walk)
            {
                LastWalkStartAt = timestamp;
            }
            else
            {
                LastRunStartAt = timestamp;
            }
        }
        NotifyIfActiveModeChanged(oldActive);
    }

    public void StopMoving(long timestamp)
    {
        if (!_isMoving)
        {
            return;
        }

        _isMoving = false;
        LastStopAt = timestamp;
        LastMovementAt = timestamp;
    }

    public void ForceWalk(long timestamp)
    {
        if (OverrideMode == EMobilityOverride.ForceWalk)
        {
            return;
        }

        var oldActive = ActiveMode;
        OverrideMode = EMobilityOverride.ForceWalk;

        if (_isMoving && oldActive != ActiveMode)
        {
            LastWalkStartAt = timestamp;
        }

        NotifyIfActiveModeChanged(oldActive);
    }

    public void ClearForceWalk(long timestamp)
    {
        if (OverrideMode == EMobilityOverride.None)
        {
            return;
        }

        var oldActive = ActiveMode;
        OverrideMode = EMobilityOverride.None;

        if (_isMoving && oldActive != ActiveMode)
        {
            LastRunStartAt = timestamp;
        }

        NotifyIfActiveModeChanged(oldActive);
    }

    private void NotifyIfActiveModeChanged(EMobilityMode before)
    {
        var after = ActiveMode;
        if (after != before)
        {
            ActiveModeChanged?.Invoke(before, after);
        }
    }
}
