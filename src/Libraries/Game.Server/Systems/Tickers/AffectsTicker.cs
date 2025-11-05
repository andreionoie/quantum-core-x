using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Affects;
using QuantumCore.API.Systems.Tickers;
using static QuantumCore.Game.Constants.SchedulingConstants;
using static QuantumCore.Game.Systems.Tickers.AffectsTickerHelpers;

namespace QuantumCore.Game.Systems.Tickers;

/// <summary>
/// Generic affects ticker that advances durations and consumes periodic costs.
/// </summary>
public class AffectsTicker<TAffectable>(TAffectable entity, TryConsumeSpDelegate? spConsumer = null, uint intervalMs = AffectsUpdateIntervalMs)
    : GatedTickerEngine<TAffectable>(entity, TimeSpan.FromMilliseconds(intervalMs))
    where TAffectable : IAffectable
{
    protected override bool UpdateState(TAffectable state, TimeSpan elapsed)
    {
        if (state.Affects.Active.Count <= 0)
        {
            return false;
        }

        var pointsChanged = false;
        var expiredAffects = new List<EntityAffect>();

        foreach (var affect in state.Affects.Active)
        {
            // 1. Update remaining duration
            if (affect.RemainingDuration < EntityAffect.PermanentAffectDurationThreshold)
            {
                if (affect.RemainingDuration <= elapsed)
                {
                    expiredAffects.Add(affect);
                }
                else
                {
                    affect.RemainingDuration -= elapsed;
                }
            }

            // 2. Decrease the periodic cost using the provided consumer
            if (spConsumer is not null && affect.SpCostPerSecond > 0)
            {
                var accumulated = affect.FractionalSpCostAccumulator + affect.SpCostPerSecond * elapsed.TotalSeconds;
                var toConsume = (uint)Math.Floor(accumulated);
                if (toConsume > 0)
                {
                    if (!spConsumer(toConsume))
                    {
                        expiredAffects.Add(affect);
                        continue;
                    }

                    pointsChanged = true;
                    accumulated -= toConsume;
                }

                affect.FractionalSpCostAccumulator = accumulated;
            }
        }

        state.Affects.Remove([..expiredAffects]);

        return pointsChanged;
    }
}

public static class AffectsTickerHelpers
{
    public delegate bool TryConsumeSpDelegate(uint amount);
    
}
