using OneOf;

namespace QuantumCore.API.Game.Types.Skills;

public enum EWarriorSkillGroup : byte
{
    None = 0,
    WarriorBody = 1,
    WarriorMental = 2
}

public enum ENinjaSkillGroup : byte
{
    None = 0,
    NinjaBlade = 1,
    NinjaArchery = 2
}

public enum ESuraSkillGroup : byte
{
    None = 0,
    SuraWeapon = 1,
    SuraBlackMagic = 2
}

public enum EShamanSkillGroup : byte
{
    None = 0,
    ShamanDragon = 1,
    ShamanHealing = 2
}

public readonly struct SkillGroup : IEquatable<SkillGroup>
{
    private readonly OneOf<EWarriorSkillGroup, ENinjaSkillGroup, ESuraSkillGroup, EShamanSkillGroup> _value;
    
    public static SkillGroup FromWarrior(EWarriorSkillGroup warrior) => new(warrior);
    public static SkillGroup FromNinja(ENinjaSkillGroup ninja) => new(ninja);
    public static SkillGroup FromSura(ESuraSkillGroup sura) => new(sura);
    public static SkillGroup FromShaman(EShamanSkillGroup shaman) => new(shaman);

    public static bool TryFrom(EPlayerClass playerClass, byte rawSkillGroup, out SkillGroup skillGroup)
    {
        skillGroup = default;

        switch (playerClass)
        {
            case EPlayerClass.Warrior:
                if (Enum.IsDefined(typeof(EWarriorSkillGroup), rawSkillGroup))      // TODO: switch to the faster EnumUtils.IsDefined ?
                {
                    skillGroup = FromWarrior((EWarriorSkillGroup)rawSkillGroup);
                    return true;
                }
                return false;
            case EPlayerClass.Ninja:
                if (Enum.IsDefined(typeof(ENinjaSkillGroup), rawSkillGroup))
                {
                    skillGroup = FromNinja((ENinjaSkillGroup)rawSkillGroup);
                    return true;
                }
                return false;
            case EPlayerClass.Sura:
                if (Enum.IsDefined(typeof(ESuraSkillGroup), rawSkillGroup))
                {
                    skillGroup = FromSura((ESuraSkillGroup)rawSkillGroup);
                    return true;
                }
                return false;
            case EPlayerClass.Shaman:
                if (Enum.IsDefined(typeof(EShamanSkillGroup), rawSkillGroup))
                {
                    skillGroup = FromShaman((EShamanSkillGroup)rawSkillGroup);
                    return true;
                }
                return false;
            
            default:
                throw new ArgumentOutOfRangeException(nameof(playerClass));
        }
    }
    
    public bool IsWarrior => _value.IsT0;
    public bool IsNinja => _value.IsT1;
    public bool IsSura => _value.IsT2;
    public bool IsShaman => _value.IsT3;

    public byte ToRaw() => _value.Match(
        warrior => (byte)warrior,
        ninja => (byte)ninja,
        sura => (byte)sura,
        shaman => (byte)shaman
    );

    public override string ToString() => _value.Match(
        warrior => Enum.GetName(warrior) ?? ((byte)warrior).ToString(),
        ninja => Enum.GetName(ninja) ?? ((byte)ninja).ToString(),
        sura => Enum.GetName(sura) ?? ((byte)sura).ToString(),
        shaman => Enum.GetName(shaman) ?? ((byte)shaman).ToString()
    );
    
    private int Discriminant => _value.IsT0 ? 0 : _value.IsT1 ? 1 : _value.IsT2 ? 2 : _value.IsT3 ? 3 : -1;
    public bool Equals(SkillGroup other) => Discriminant == other.Discriminant && ToRaw() == other.ToRaw();
    public override bool Equals(object? obj) => obj is SkillGroup other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Discriminant, ToRaw());
    public static bool operator ==(SkillGroup left, SkillGroup right) => left.Equals(right);
    public static bool operator !=(SkillGroup left, SkillGroup right) => !left.Equals(right);

    
    private SkillGroup(EWarriorSkillGroup skillGroup) => _value = skillGroup;
    private SkillGroup(ENinjaSkillGroup skillGroup)   => _value = skillGroup;
    private SkillGroup(ESuraSkillGroup skillGroup)    => _value = skillGroup;
    private SkillGroup(EShamanSkillGroup skillGroup)  => _value = skillGroup;
}
