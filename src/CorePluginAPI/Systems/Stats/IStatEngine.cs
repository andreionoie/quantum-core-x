using QuantumCore.API.Game.Types;

namespace QuantumCore.API.Systems.Stats;

public interface IStatEngine
{
    /// <summary>
    /// Represents an object that can be the source of stat modifiers.
    /// </summary>
    public interface IModifierSource
    {
        /// <summary>
        /// Gets the unique string key identifying this modifier source.
        /// Recommended format: "{category}:{instance}"
        /// </summary>
        string ModifierKey { get; }
    }
    
    ModifiableStat this[EPoint p] { get; set; }
    void RegisterDependency(EPoint dependent, params EPoint[] bases);
    void NotifyBaseChanged(EPoint basePoint);
    void RemoveAllModifiersWithSource(IModifierSource source);
}
