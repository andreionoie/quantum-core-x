namespace QuantumCore.API.Game.Types.Skills;

public enum EAffectType : uint
{
    None = 0,

    MoveSpeed = 200,
    AttackSpeed = 201,
    AttackGrade = 202,
    Invisibility = 203,
    Strength = 204,
    Dexterity = 205,
    Constitution = 206,
    Intelligence = 207,
    FishMindPill = 208,

    Poison = 209,
    Stun = 210,
    Slow = 211,

    Building = 214,
    InvisibleRespawn = 215,
    Fire = 216,
    CastSpeed = 217,
    HpRecoverContinue = 218,
    SpRecoverContinue = 219,

    Polymorph = 220,
    Mount = 221,
    
    WarFlag = 222,        // guild flag

    BlockChat = 223,
    ChinaFirework = 224,
    
    BowDistance = 225,
    DefenseGrade = 226,

    // Premium Affects
    ExpBonus = 500,  // experience ring
    ItemBonus = 501,       // thief's glove
    Safebox = 502,         // expanded storage
    AutoLoot = 503,        // third hand
    FishMind = 504,        // fishing marble
    MarriageFast = 505,    // feather of lovers
    GoldBonus = 506,       // lucky gold coin
    ItemShop = 510,                  
    NoDeathPenalty = 511,
    SkillbookReadingBonus = 512,
    SkillbookNoDelay = 513,
    Hair = 514,
    Collect = 515,
    Bonus = 516,
    Bonus2 = 517,
    UniqueAbility = 518,

    Cube1 = 519,
    Cube2 = 520,
    Cube3 = 521,
    Cube4 = 522,
    Cube5 = 523,
    Cube6 = 524,
    Cube7 = 525,
    Cube8 = 526,
    Cube9 = 527,
    Cube10 = 528,
    Cube11 = 529,
    Cube12 = 530,

    Blend = 531,
    HorseName = 532,
    MountBonus = 533,

    AutoHpRecovery = 534,
    AutoSpRecovery = 535,

    DragonSoulQualified = 540,
    DragonSoulDeck0 = 541,
    DragonSoulDeck1 = 542,
    
    QuestStart = 1000,
}
