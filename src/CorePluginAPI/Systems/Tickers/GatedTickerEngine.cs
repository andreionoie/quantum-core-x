namespace QuantumCore.API.Systems.Tickers;

// A custom interval allows setting a higher tick frequency for the tickers of VIP/premium players, for example.
// (obviously with a lower bound of 100Hz by the parent ticker GameServer._targetElapsedTime)
// This can also be extended to permit dynamically changing the interval at runtime, e.g. to scale based on current load.
public abstract class GatedTickerEngine<TState>(TState state, TimeSpan interval)
{
    private TimeSpan _accum = TimeSpan.Zero;

    // Optional custom initial delay before the first tick
    private TimeSpan _initialDelay = TimeSpan.Zero;
    private TimeSpan _initialAccum = TimeSpan.Zero;
    private bool _initialPending = false;

    protected GatedTickerEngine(TState state, TimeSpan interval, TimeSpan initialDelay)
        : this(state, interval)
    {
        if (initialDelay > TimeSpan.Zero)
        {
            ArmInitialDelay(initialDelay);
        }
    }

    /// <summary>
    /// Arm an initial delay before the next tick. Useful when an affect just became active,
    /// and we want to trigger its processing quicker than the periodic interval.
    /// </summary>
    protected void ArmInitialDelay(TimeSpan initialDelay)
    {
        _initialDelay = initialDelay;
        _initialAccum = TimeSpan.Zero;
        _initialPending = initialDelay > TimeSpan.Zero;
    }

    /// <summary>
    /// Reset the regular interval accumulator. Useful when (re)arming an effect so we don't immediately
    /// trigger a tick due to previously accumulated elapsed time while the effect was inactive.
    /// </summary>
    protected void ResetIntervalAccum()
    {
        _accum = TimeSpan.Zero;
    }

    /// <summary>
    /// Advance internal state by elapsed. Returns true if any work was performed.
    /// Handles an optional initial delay gate followed by the regular interval cadence.
    /// </summary>
    public bool Step(TimeSpan elapsed)
    {
        var changedState = false;

        // 1. Handle initial delay gate if armed
        if (_initialPending)
        {
            _initialAccum += elapsed;
            if (_initialAccum >= _initialDelay)
            {
                changedState |= UpdateState(state, _initialDelay);

                var overshoot = _initialAccum - _initialDelay;
                if (overshoot > TimeSpan.Zero)
                {
                    _accum += overshoot;
                }

                _initialAccum = TimeSpan.Zero;
                _initialPending = false;
            }
            else
            {
                return false;
            }
        }

        // 2. Handle steady-state interval gating
        _accum += elapsed;
        if (_accum < interval)
        {
            return changedState;
        }

        var processedIntervals = _accum.Ticks / interval.Ticks;
        var processed = TimeSpan.FromTicks(processedIntervals * interval.Ticks);
        _accum -= processed;

        changedState |= UpdateState(state, processed);
        return changedState;
    }

    /// <summary>
    /// Advance TState state; return true if update produced an actual change.
    /// Implementations should scale linearly with processed to batch work for multiple intervals.
    /// </summary>
    protected abstract bool UpdateState(TState state, TimeSpan processed);
}
