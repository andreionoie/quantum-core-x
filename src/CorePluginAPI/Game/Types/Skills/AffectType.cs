using OneOf;

namespace QuantumCore.API.Game.Types.Skills;

public readonly struct AffectType : IEquatable<AffectType>
{
    private readonly OneOf<EAffectType, ESkill> _value;

    public static AffectType From(EAffectType normal) => new(normal);
    public static AffectType FromSkill(ESkill skill) => new(skill);

    public static bool TryFromRaw(uint raw, out AffectType affectType)
    {
        affectType = default;
        if (Enum.IsDefined(typeof(EAffectType), raw))
        {
            affectType = From((EAffectType)raw);
            return true;
        }

        if (Enum.IsDefined(typeof(ESkill), raw))
        {
            affectType = FromSkill((ESkill)raw);
            return true;
        }

        return false;
    }

    public bool IsNormalAffect => _value.IsT0;
    public bool IsSkillAffect => _value.IsT1;

    public uint ToRaw() => _value.Match(
        normal => (uint)normal,
        skill => (uint)skill);

    public override string ToString() => _value.Match(
        normal => Enum.GetName(normal) ?? ((uint)normal).ToString(),
        skill => Enum.GetName(skill) ?? ((uint)skill).ToString());

    public bool Equals(AffectType other) => ToRaw() == other.ToRaw();

    public override bool Equals(object? obj) => obj is AffectType other && Equals(other);

    public override int GetHashCode() => ToRaw().GetHashCode();

    public static bool operator ==(AffectType left, AffectType right) => left.Equals(right);

    public static bool operator !=(AffectType left, AffectType right) => !left.Equals(right);
    
    private AffectType(EAffectType type) => _value = type;
    private AffectType(ESkill skill) => _value = skill;
}
