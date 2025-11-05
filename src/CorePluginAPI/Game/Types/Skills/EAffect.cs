namespace QuantumCore.API.Game.Types.Skills;

public enum EAffect
{
    None = 0, 
    
    GameMaster = 1,
    Invisibility = 2,
    SpawnWithAppearFx = 3,
    
    Poison = 4,
    Slow = 5,
    Stun = 6,

    DungeonReady = 7,
    DungeonUnique = 8,

    BuildingConstructionSmall = 9,
    BuildingConstructionLarge = 10,
    BuildingUpgrade = 11,
    
    MovementSpeedPotion = 12,
    AttackSpeedPotion = 13,
    FishMind = 14,
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.BerserkerFury"/>
    /// </summary>
    Berserk = 15,            // AKA "Jeongwihon"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.AuraOfTheSword"/>
    /// </summary>
    AuraOfTheSword = 16,     // AKA "Geomgyeong"
    /// <summary>
    /// Affect for skill <see cref="ESkill.StrongBody"/>
    /// </summary>
    StrongBody = 17,         // AKA "Cheongeun"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.FeatherWalk"/>
    /// </summary>
    FeatherWalk = 18,        // AKA "Gyeonggong"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.Stealth"/>
    /// </summary>
    Stealth = 19,            // AKA "Eunhyung"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.EnchantedBlade"/>
    /// </summary>
    EnchantedBlade = 20,     // AKA "Gwigum"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.Fear"/>
    /// </summary>
    Terror = 21,
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.EnchantedArmor"/>
    /// </summary>
    EnchantedArmor = 22,     // AKA "Jumagap"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.Blessing"/>
    /// </summary>
    Blessing = 23,           // AKA "Hosin"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.Reflect"/>
    /// </summary>
    Reflect = 24,            // AKA "Boho"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.Swiftness"/>
    /// </summary>
    Swiftness = 25,          // AKA "Kwaesok"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.DarkProtection"/>
    /// </summary>
    Manashield = 26,
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.FlameSpirit"/>
    /// </summary>
    FlameSpirit = 27,        // AKA "Muyeong"
    
    InvisibleRespawn = 28,
    Fire = 29,
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.DragonAid"/>
    /// </summary>
    DragonAid = 30,          // AKA "Gicheon"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.AttackUp"/>
    /// </summary>
    AttackUp = 31,           // AKA "Jeungryeok"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.Dash"/>
    /// </summary>
    Dash = 32,               // AKA "Tanhwan"
    
    /// <summary>
    /// Affect for skill <see cref="ESkill.Dispel"/>
    /// </summary>
    Dispel = 33,             // AKA "Pabeop"
    
    StrongBodyKnockback = 34,    // AKA "Cheongeun"
    Polymorph = 35,
    WarFlag1 = 36,
    WarFlag2 = 37,
    WarFlag3 = 38,
    ChinaFirework = 39,
    Hair = 40,
    Germany = 41
}
