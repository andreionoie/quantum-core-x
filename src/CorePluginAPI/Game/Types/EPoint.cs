using System.Runtime.Serialization;

namespace QuantumCore.API.Game.Types;

public enum EPoint
{
    [EnumMember(Value = "NONE")] None = 0,
    Level = 1,
    Voice = 2,
    Experience = 3,
    NeededExperience = 4,
    [EnumMember(Value = "HP")] Hp = 5,
    [EnumMember(Value = "MAX_HP")] MaxHp = 6,
    [EnumMember(Value = "SP")] Sp = 7,
    [EnumMember(Value = "MAX_SP")] MaxSp = 8,
    Stamina = 9,
    MaxStamina = 10,
    Gold = 11,
    St = 12,
    Ht = 13,
    Dx = 14,
    Iq = 15,
    DefenceGrade = 16,
    [EnumMember(Value = "ATT_SPEED")] AttackSpeed = 17,
    AttackGrade = 18,
    [EnumMember(Value = "MOV_SPEED")] MoveSpeed = 19,
    Defence = 20,
    [EnumMember(Value = "CASTING_SPEED")] CastingSpeed = 21,
    MagicAttackGrade = 22,
    MagicDefenceGrade = 23,
    EmpirePoint = 24,
    LevelStep = 25,
    StatusPoints = 26,
    SubSkill = 27,
    Skill = 28,
    MinAttackDamage = 29,
    MaxAttackDamage = 30,
    PlayTime = 31,
    [EnumMember(Value = "HP_REGEN")] HpRegen = 32,
    [EnumMember(Value = "SP_REGEN")] SpRegen = 33,
        
    [EnumMember(Value = "BOW_DISTANCE")] BowDistance = 34,
        
    HpRecovery = 35,
    SpRecovery = 36,
        
    [EnumMember(Value = "POISON_PCT")] PoisonPercentage = 37,
    StunPercentage = 38,
    SlowPercentage = 39,
    [EnumMember(Value = "CRITICAL")] CriticalPercentage = 40,
    PenetratePercentage = 41,
    CursePercentage = 42,
        
    AttackBonusHuman = 43,
    AttackBonusAnimal = 44,
    AttackBonusOrc = 45,
    AttackBonusEsoterics = 46,
    AttackBonusUndead = 47,
    AttackBonusDevil = 48,
    AttackBonusInsect = 49,
    AttackBonusFire = 50,
    AttackBonusIce = 51,
    AttackBonusDesert = 52,
    AttackBonusMonster = 53,
    AttackBonusWarrior = 54,
    AttackBonusAssassin = 55,
    AttackBonusSura = 56,
    AttackBonusShaman = 57,
    AttackBonusTree = 58,
        
    ResistWarrior = 59,
    ResistAssassin = 60,
    ResistSura = 61,
    ResistShaman = 62,
        
    StealHp = 63,
    StealSp = 64,
        
    ManaBurnPercentage = 65,
        
    DamageSpRecover = 66,
        
    [EnumMember(Value = "BLOCK")] Block = 67,
    [EnumMember(Value = "DODGE")] Dodge = 68,
        
    ResistSword = 69,
    ResistTwoHanded = 70,
    ResistDagger = 71,
    ResistBell = 72,
    ResistFan = 73,
    [EnumMember(Value = "RESIST_RANGE")] ResistBow = 74,
    ResistFire = 75,
    ResistElectric = 76,
    ResistMagic = 77,
    ResistWind = 78,
        
    [EnumMember(Value = "REFLECT_MELEE")] ReflectMelee = 79,
    ReflectCurse = 80,
    PoisonReduce = 81,
    [EnumMember(Value = "KILL_SP_RECOVER")] KillSpRecover = 82,
        
    ExpDoubleBonus = 83,
    GoldDoubleBonus = 84,
    ItemDropBonus = 85,
        
    PotionBonus = 86,
    [EnumMember(Value = "KILL_HP_RECOVER")] KillHpRecover = 87,
        
    ImmuneStun = 88,
    ImmuneSlow = 89,
    ImmuneFall = 90,
        
    PartyAttackerBonus = 91,
    PartyTankerBonus = 92,

    [EnumMember(Value = "ATT_BONUS")] AttackBonus = 93,
    [EnumMember(Value = "DEF_BONUS")] DefenceBonus = 94,
    [EnumMember(Value = "ATT_GRADE")] AttackGradeBonus = 95,
    [EnumMember(Value = "DEF_GRADE")] DefenceGradeBonus = 96,
    [EnumMember(Value = "MAGIC_ATT_GRADE")] MagicAttackGradeBonus = 97,
    [EnumMember(Value = "MAGIC_DEF_GRADE")] MagicDefenceGradeBonus = 98,
        
    [EnumMember(Value = "RESIST_NORMAL")] ResistNormalDamage = 99,
        
    [EnumMember(Value = "HIT_HP_RECOVER")] HitHpRecovery = 100,
    [EnumMember(Value = "HIT_SP_RECOVER")] HitSpRecovery = 101,
        
    [EnumMember(Value = "MANASHIELD")] Manashield = 102,
        
    PartyBufferBonus = 103,
    PartySkillMasterBonus = 104,
        
    HpRecoverContinue = 105,
    SpRecoverContinue = 106,
        
    StealGold = 107,
    Polymorph = 108,
    Mount = 109,
    PartyHasteBonus = 110,
    PartyDefenderBonus = 111,
        
    StatResetCount = 112,

    HorseSkill = 113,

    MallAttBonus = 114,
    MallDefBonus = 115,
    MallExpBonus = 116,
    MallItemBonus = 117,
    MallGoldBonus = 118,
    MaxHpPercentage = 119,
    MaxSpPercentage = 120,
    [EnumMember(Value = "SKILL_DAMAGE_BONUS")] SkillDamageBonus = 121,
    [EnumMember(Value = "NORMAL_HIT_DAMAGE_BONUS")] NormalHitDamageBonus = 122,
    SkillDefendBonus = 123,
    NormalHitDefendBonus = 124,
        
    Energy = 128,
    EnergyEndTime = 129,
    CostumeAttrBonus = 130,
    MagicAttackBonusPer = 131,
    MeleeMagicAttackBonusPer = 132,
    ResistIce = 133,
    ResistEarth = 134,
    ResistDark = 135,
    ResistCritical = 136,
    ResistPenetrate = 137,
        
    // Client Special Points
    MinWeaponDamage = 200,
    MaxWeaponDamage = 201,
    MinMagicWeaponDamage = 202,
    MaxMagicWeaponDamage = 203,
    HitRate = 204
}
