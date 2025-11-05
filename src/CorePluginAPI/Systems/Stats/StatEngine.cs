using System.Text;
using QuantumCore.API.Game.Types;

namespace QuantumCore.API.Systems.Stats;

public sealed class StatEngine(StatEngine.BaseValueSupplierFactory baseValueSupplierFactory)
    : IStatEngine
{
    public delegate ModifiableStat.BaseValueSupplier BaseValueSupplierFactory(EPoint point);
    
    private readonly Dictionary<EPoint, ModifiableStat> _statContainers = new();
    private readonly Dictionary<EPoint, HashSet<EPoint>> _dependencies = new();

    private ModifiableStat GetOrInitContainer(EPoint point)
    {
        if (_statContainers.TryGetValue(point, out var statContainer))
        {
            return statContainer;
        }
        
        // providing a callback so adding modifiers to a stat automatically invalidates its dependents
        statContainer = new ModifiableStat(point, baseValueSupplierFactory(point), NotifyBaseChanged);
        _statContainers[point] = statContainer;
        return statContainer;
    }

    public ModifiableStat this[EPoint p]
    {
        get => GetOrInitContainer(p);
        set => _statContainers[p] = value;
    }

    public void Invalidate(EPoint target)
    {
        if (_statContainers.TryGetValue(target, out var v))
        {
            v.Invalidate();
        }
    }

    public void RegisterDependency(EPoint dependent, params EPoint[] bases)
    {
        foreach (var basePoint in bases)
        {
            if (!_dependencies.TryGetValue(basePoint, out var baseDependencies))
            {
                baseDependencies = [];
                _dependencies[basePoint] = baseDependencies;
            }
            baseDependencies.Add(dependent);
        }
    }

    public void NotifyBaseChanged(EPoint basePoint)
    {
        Invalidate(basePoint);

        if (_dependencies.TryGetValue(basePoint, out var deps))
        {
            foreach (var dep in deps)
            {
                if (_statContainers.TryGetValue(dep, out var depContainer))
                {
                    depContainer.Invalidate();
                    // fire OnChanged for dependents since their computed value has changed
                    depContainer.OnChanged?.Invoke(dep);
                }
            }
        }
    }

    public void RemoveAllModifiersWithSource(IStatEngine.IModifierSource source)
    {
        foreach (var container in _statContainers.Values)
        {
            container.RemoveModifier(source);
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder()
            .Append($"{nameof(StatEngine)}: {_statContainers.Count} points tracked");

        var idx = 1;
        foreach (var (point, container) in _statContainers)
        {
            if (container.ToString().Equals("cached = 0")) continue;
            
            sb.Append(Environment.NewLine);
            sb.Append($"{idx++}. Point {point}: {container}");
        }

        return sb.ToString();
    }
}
