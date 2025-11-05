namespace QuantumCore.API.Game.Types.Skills;

[Flags]
public enum EAffectFlags : ulong
{
    None = 0,
    
    GameMaster = 1 << (EAffect.GameMaster - 1),
    Invisibility = 1 << (EAffect.Invisibility - 1),
    SpawnWithAppearFx = 1 << (EAffect.SpawnWithAppearFx - 1),
    
    Poison = 1 << (EAffect.Poison - 1),
    Slow = 1 << (EAffect.Slow - 1),
    Stun = 1 << (EAffect.Stun - 1),
    
    BuildingConstructionSmall = 1 << (EAffect.BuildingConstructionSmall - 1),
    BuildingConstructionLarge = 1 << (EAffect.BuildingConstructionLarge - 1),
    BuildingUpgrade = 1 << (EAffect.BuildingUpgrade - 1),
    MovementSpeedPotion = 1 << (EAffect.MovementSpeedPotion - 1),
    AttackSpeedPotion = 1 << (EAffect.AttackSpeedPotion - 1),
    FishMind = 1 << (EAffect.FishMind - 1),
    Berserk = 1 << (EAffect.Berserk - 1),
    AuraOfTheSword = 1 << (EAffect.AuraOfTheSword - 1),
    StrongBody = 1 << (EAffect.StrongBody - 1),
    FeatherWalk = 1 << (EAffect.FeatherWalk - 1),
    Stealth = 1 << (EAffect.Stealth - 1),
    EnchantedBlade = 1 << (EAffect.EnchantedBlade - 1),
    Terror = 1 << (EAffect.Terror - 1),
    EnchantedArmor = 1 << (EAffect.EnchantedArmor - 1),
    Blessing = 1 << (EAffect.Blessing - 1),
    Reflect = 1 << (EAffect.Reflect - 1),
    Swiftness = 1 << (EAffect.Swiftness - 1),
    Manashield = 1 << (EAffect.Manashield - 1),
    FlameSpirit = 1 << (EAffect.FlameSpirit - 1),
    InvisibleRespawn = 1 << (EAffect.InvisibleRespawn - 1),
    Fire = 1 << (EAffect.Fire - 1),
    DragonAid = 1 << (EAffect.DragonAid - 1),
    AttackUp = 1 << (EAffect.AttackUp - 1),
    
    // into 64-bit zone from here
    Dash = 1UL << (EAffect.Dash - 1),
    Dispel = 1UL << (EAffect.Dispel - 1),
    StrongBodyKnockback = 1UL << (EAffect.StrongBodyKnockback - 1),
    Polymorph = 1UL << (EAffect.Polymorph - 1),
    WarFlag1 = 1UL << (EAffect.WarFlag1 - 1),
    WarFlag2 = 1UL << (EAffect.WarFlag2 - 1),
    WarFlag3 = 1UL << (EAffect.WarFlag3 - 1),
    
    ChinaFirework = 1UL << (EAffect.ChinaFirework - 1),
    Hair = 1UL << (EAffect.Hair - 1),
    Germany = 1UL << (EAffect.Germany - 1),
}

public static class AffectFlagExtensions
{
    public static EAffectFlags ToFlag(this EAffect bit)
    {
        if (bit == EAffect.None) return EAffectFlags.None;
        var shift = (int)bit - 1;
        return (EAffectFlags)(1UL << shift);
    }

    public static bool Has(this EAffectFlags flags, EAffect bit)
        => flags.HasFlag(bit.ToFlag());

    public static EAffectFlags CombineFlags(this EAffectFlags flags, EAffect bit)
        => flags | bit.ToFlag();

    public static EAffectFlags RemoveFlag(this EAffectFlags flags, EAffect bit)
        => flags & ~bit.ToFlag();
}
