using OneOf;
using QuantumCore.API.Game.Types.Skills;

namespace QuantumCore.API.Game.Types.Combat;

public enum ERangedAttackBasic : byte
{
    Archer = 0,
    Magic = 1
}

public readonly struct RangedAttackType : IEquatable<RangedAttackType>
{
    private readonly OneOf<ERangedAttackBasic, ESkill> _value;

    public static RangedAttackType FromBasic(ERangedAttackBasic basic) => new(basic);
    public static RangedAttackType FromSkill(ESkill skill) => new(skill);

    public static bool TryFromRaw(byte raw, out RangedAttackType type)
    {
        type = default;

        if (Enum.IsDefined(typeof(ERangedAttackBasic), raw))
        {
            type = FromBasic((ERangedAttackBasic)raw);
            return true;
        }

        if (Enum.IsDefined(typeof(ESkill), (uint)raw))
        {
            type = FromSkill((ESkill)raw);
            return true;
        }

        return false;
    }

    public bool IsBasic => _value.IsT0;
    public bool IsSkill => _value.IsT1;

    public byte ToRaw() => _value.Match(
        basic => (byte)basic,
        skill => (byte)skill);

    public override string ToString() => _value.Match(
        basic => Enum.GetName(basic) ?? ((byte)basic).ToString(),
        skill => Enum.GetName(skill) ?? ((byte)skill).ToString());

    public bool Equals(RangedAttackType other) => ToRaw() == other.ToRaw();
    public override bool Equals(object? obj) => obj is RangedAttackType other && Equals(other);
    public override int GetHashCode() => ToRaw().GetHashCode();
    public static bool operator ==(RangedAttackType left, RangedAttackType right) => left.Equals(right);
    public static bool operator !=(RangedAttackType left, RangedAttackType right) => !left.Equals(right);
    
    private RangedAttackType(ERangedAttackBasic basic) => _value = basic;
    private RangedAttackType(ESkill skill) => _value = skill;

}

