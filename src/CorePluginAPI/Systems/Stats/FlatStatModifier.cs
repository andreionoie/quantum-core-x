using QuantumCore.API.Game.Types;

namespace QuantumCore.API.Systems.Stats;

// NOTE: we can define a generic IStatModifier interface and implement other types of modifiers. Some examples:
// * PercentAdditiveStatModifier: +10% from source A, +15% from source B => +25% on final value
// * OverrideStatModifier: ignores all other modifiers, forces a constant value
// * ClampedStatModifier: final value is clamped, cannot exceed a min or max bound
// Currently this is the only type of modifier needed for basic game functionality.
public sealed class FlatStatModifier
{
    public required string SourceKey { get; init; }
    public required EPoint Target { get; init; }
    public int Delta { get; init; }

    public override string ToString()
    {
        return $"'{SourceKey}': delta({Target}) = {Delta:+#;-#;none}";
    }
}
