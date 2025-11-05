using System.Diagnostics;
using System.Text;
using QuantumCore.API.Game.Types;

namespace QuantumCore.API.Systems.Stats;

public sealed class ModifiableStat(EPoint point, ModifiableStat.BaseValueSupplier baseValueSupplier, Action<EPoint>? onChanged = null)
{
    public Action<EPoint>? OnChanged { get; set; } = onChanged;

    public delegate int BaseValueSupplier();

    private EPoint Point { get; } = point;
    private int? _cached;
    private readonly Dictionary<string, List<FlatStatModifier>> _modifiersBySource = new();

    #region Syntax sugar helpers
    
    public static implicit operator int(ModifiableStat stat)
    {
        return stat.ComputeValue();
    }
    
    public static explicit operator uint(ModifiableStat stat)
    {
        return (uint)stat.ComputeValue();
    } 
    
    public static ModifiableStat operator +(ModifiableStat stat, (int delta, IStatEngine.IModifierSource source) modifier)
    {
        stat.AddModifier(modifier.delta, modifier.source);
        return stat;
    } 
    
    #endregion
    
    public void AddModifier(int delta, IStatEngine.IModifierSource source)
    {
        Debug.Assert(delta != 0);

        if (!_modifiersBySource.TryGetValue(source.ModifierKey, out var modifiers))
        {
            modifiers = [];
            _modifiersBySource[source.ModifierKey] = modifiers;
        }

        modifiers.Add(new FlatStatModifier
        {
            SourceKey = source.ModifierKey,
            Target = Point,
            Delta = delta
        });
        Invalidate();
        OnChanged?.Invoke(Point);
    }

    public bool RemoveModifier(IStatEngine.IModifierSource source)
    {
        if (_modifiersBySource.Remove(source.ModifierKey))
        {
            Invalidate();
            OnChanged?.Invoke(Point);
            return true;
        }

        return false;
    }

    public void Invalidate()
    {
        _cached = null;
    }

    public int ComputeValue()
    {
        if (_cached.HasValue)
        {
            return _cached.Value;
        }

        var baseValue = baseValueSupplier();
        var combinedFlatModifiers = 0;
        foreach (var modifierList in _modifiersBySource.Values)
        {
            foreach (var m in modifierList)
            {
                combinedFlatModifiers += m.Delta;
            }
        }

        _cached = baseValue + combinedFlatModifiers;
        return _cached.Value;
    }

    public override string ToString()
    {
        var sb = new StringBuilder()
            .Append($"cached = {(_cached.HasValue ? _cached.Value : "no")}");

        foreach (var modifierList in _modifiersBySource.Values)
        {
            foreach (var modif in modifierList)
            {
                sb.Append(Environment.NewLine);
                sb.Append($"        * {modif}");
            }
        }

        return sb.ToString();
    }
}
