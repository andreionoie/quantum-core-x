using System.Text;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;

namespace QuantumCore.API.Systems.Affects;

public sealed class AffectsController : IAffectsController
{
    private readonly object _lock = new();
    private readonly List<EntityAffect> _active = [];
    private EAffectFlags _cachedFlags = EAffectFlags.None;
    private bool _flagsCacheValid = false;

    public event Action<EntityAffect>? AffectAdded;
    public event Action<EntityAffect>? AffectRemoved;

    public IReadOnlyList<EntityAffect> Active
    {
        get
        {
            lock (_lock)
            {
                return _active.ToList();
            }
        }
    }

    public void Upsert(EntityAffect affect)
    {
        lock (_lock)
        {
            var existing = _active.FirstOrDefault(a => a.AffectType == affect.AffectType && a.ModifiedPointId == affect.ModifiedPointId);
            if (existing is not null)
            {
                // Check if only the mutable properties changed (duration/SP accumulator)
                // If the immutable properties are the same, just update in place to avoid client flicker
                bool onlyDurationChanged = existing.AffectFlag == affect.AffectFlag &&
                                         existing.ModifiedPointDelta == affect.ModifiedPointDelta &&
                                         existing.SpCostPerSecond == affect.SpCostPerSecond &&
                                         existing.DoNotPersist == affect.DoNotPersist &&
                                         existing.DoNotClearOnDeath == affect.DoNotClearOnDeath &&
                                         existing.SourceAttackerId == affect.SourceAttackerId;

                if (onlyDurationChanged)
                {
                    // Update mutable properties in-place without firing remove/add events
                    existing.RemainingDuration = affect.RemainingDuration;
                    existing.FractionalSpCostAccumulator = affect.FractionalSpCostAccumulator;
                    return;
                }

                // Properties changed, need to replace the affect
                if (_active.Remove(existing))
                {
                    _flagsCacheValid = false;
                    AffectRemoved?.Invoke(existing);
                }
            }

            _active.Add(affect);
            _flagsCacheValid = false;
            AffectAdded?.Invoke(affect);
        }
    }

    public bool Remove(AffectType type, EPoint applyOn)
    {
        lock (_lock)
        {
            var found = _active.FirstOrDefault(a => a.AffectType == type && a.ModifiedPointId == applyOn);
            if (found is null)
            {
                return false;
            }

            if (_active.Remove(found))
            {
                _flagsCacheValid = false;
                AffectRemoved?.Invoke(found);
                return true;
            }

            return false;
        }
    }

    public bool RemoveAllOfType(AffectType type)
    {
        lock (_lock)
        {
            var toRemove = _active.Where(a => a.AffectType == type).ToList();

            var anyRemoved = false;
            foreach (var affect in toRemove)
            {
                if (_active.Remove(affect))
                {
                    anyRemoved = true;
                    AffectRemoved?.Invoke(affect);
                }
            }

            if (anyRemoved)
            {
                _flagsCacheValid = false;
            }

            return anyRemoved;
        }
    }

    public void Clear(bool preserveNoClearOnDeath = false)
    {
        lock (_lock)
        {
            var toRemove = preserveNoClearOnDeath
                ? _active.Where(a => !a.DoNotClearOnDeath).ToList()
                : _active.ToList();

            var anyRemoved = false;
            foreach (var affect in toRemove)
            {
                if (_active.Remove(affect))
                {
                    anyRemoved = true;
                    AffectRemoved?.Invoke(affect);
                }
            }

            if (anyRemoved)
            {
                _flagsCacheValid = false;
            }
        }
    }

    public bool Remove(params EntityAffect[] affects)
    {
        lock (_lock)
        {
            var anyRemoved = false;

            foreach (var affect in affects)
            {
                if (_active.Remove(affect))
                {
                    anyRemoved = true;
                    AffectRemoved?.Invoke(affect);
                }
            }

            if (anyRemoved)
            {
                _flagsCacheValid = false;
            }

            return anyRemoved;
        }
    }

    public EAffectFlags GetActiveFlags()
    {
        lock (_lock)
        {
            if (_flagsCacheValid)
            {
                return _cachedFlags;
            }

            _cachedFlags = _active.Aggregate(EAffectFlags.None,
                (flagsAccum, activeAffect) => flagsAccum | activeAffect.AffectFlag.ToFlag());
            _flagsCacheValid = true;
            return _cachedFlags;
        }
    }

    public override string ToString()
    {
        lock (_lock)
        {
            if (_active.Count == 0)
            {
                return "AffectsController: No active affects";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"AffectsController: {_active.Count} active affect(s)");
            sb.AppendLine("=".PadRight(110, '='));

            const int TypeColWidth = 30;
            const int PointColWidth = 30;
            const int DeltaColWidth = 10;
            const int DurationColWidth = 22;

            // Header row
            sb.Append("Type".PadRight(TypeColWidth));
            sb.Append("Point".PadRight(PointColWidth));
            sb.Append("Delta".PadRight(DeltaColWidth));
            sb.Append("Duration".PadRight(DurationColWidth));
            sb.AppendLine("Flag");

            sb.AppendLine("-".PadRight(110, '-'));

            // Data rows
            foreach (var affect in _active)
            {
                var typeStr = $"{affect.AffectType.ToRaw()}:{affect.AffectType}";
                var pointStr = $"{(byte)affect.ModifiedPointId}:{affect.ModifiedPointId}";
                var deltaStr = $"{affect.ModifiedPointDelta:+#;-#;none}";
                var durationStr = affect.RemainingDuration.ToString("g");
                var flagStr = $"{(uint)affect.AffectFlag}:{affect.AffectFlag}";

                sb.Append(typeStr.PadRight(TypeColWidth));
                sb.Append(pointStr.PadRight(PointColWidth));
                sb.Append(deltaStr.PadRight(DeltaColWidth));
                sb.Append(durationStr.PadRight(DurationColWidth));
                sb.AppendLine(flagStr);
            }

            sb.Append("=".PadRight(110, '='));

            return sb.ToString();
        }
    }
}
