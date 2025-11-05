using System.Text;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;

namespace QuantumCore.API.Systems.Affects;

public sealed class AffectsController : IAffectsController
{
    private readonly List<EntityAffect> _active = [];

    public event Action<EntityAffect>? AffectAdded;
    public event Action<EntityAffect>? AffectRemoved;

    public IReadOnlyList<EntityAffect> Active => _active;

    public void Upsert(EntityAffect affect)
    {
        var existing = _active.FirstOrDefault(a => a.AffectType == affect.AffectType && a.ModifiedPointId == affect.ModifiedPointId);
        if (existing is not null)
        {
            Remove(existing);
        }

        _active.Add(affect);
        AffectAdded?.Invoke(affect);
    }

    public bool Remove(AffectType type, EPoint applyOn)
    {
        var found = _active.FirstOrDefault(a => a.AffectType == type && a.ModifiedPointId == applyOn);
        if (found is null)
        {
            return false;
        }

        return Remove(found);
    }

    public bool RemoveAllOfType(AffectType type)
    {
        var toRemove = _active.Where(a => a.AffectType == type).ToList();

        return Remove([..toRemove]);
    }

    public void Clear()
    {
        Remove([.._active]);
    }

    public bool Remove(params EntityAffect[] affects)
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

        return anyRemoved;
    }

    public override string ToString()
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
