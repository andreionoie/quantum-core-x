using QuantumCore.API.Systems.Affects;

namespace QuantumCore.API.Game.World;

/// <summary>
/// Marker interface for living entities that can have timed buffs/debuffs (affects)
/// </summary>
public interface IAffectable
{
    IAffectsController Affects { get; }
}

