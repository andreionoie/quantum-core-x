using System.Runtime.Serialization;

namespace QuantumCore.API.Game.Types.Skills;

[Flags]
public enum ESkillFlags
{
    [EnumMember(Value = "NONE")] None = 0,
    [EnumMember(Value = "ATTACK")] Attack = 1 << 0,
    [EnumMember(Value = "USE_MELEE_DAMAGE")] UseMeleeDamage = 1 << 1,
    [EnumMember(Value = "COMPUTE_ATT_GRADE")] ComputeAttGrade = 1 << 2,
    [EnumMember(Value = "SELFONLY")] SelfOnly = 1 << 3,
    [EnumMember(Value = "USE_MAGIC_DAMAGE")] UseMagicDamage = 1 << 4,
    [EnumMember(Value = "USE_HP_AS_COST")] UseHpAsCost = 1 << 5,
    [EnumMember(Value = "COMPUTE_MAGIC_DAMAGE")] ComputeMagicDamage = 1 << 6,
    [EnumMember(Value = "SPLASH")] Splash = 1 << 7,
    [EnumMember(Value = "GIVE_PENALTY")] GivePenalty = 1 << 8,
    [EnumMember(Value = "USE_ARROW_DAMAGE")] UseArrowDamage = 1 << 9,
    [EnumMember(Value = "PENETRATE")] Penetrate = 1 << 10,
    [EnumMember(Value = "IGNORE_TARGET_RATING")] IgnoreTargetRating = 1 << 11,
    [EnumMember(Value = "ATTACK_SLOW")] AttackSlow = 1 << 12,
    [EnumMember(Value = "ATTACK_STUN")] AttackStun = 1 << 13,
    [EnumMember(Value = "HP_ABSORB")] HpAbsorb = 1 << 14,
    [EnumMember(Value = "SP_ABSORB")] SpAbsorb = 1 << 15,
    [EnumMember(Value = "ATTACK_FIRE_CONT")] AttackFireContinuous = 1 << 16,
    [EnumMember(Value = "REMOVE_BAD_AFFECT")] RemoveBadAffect = 1 << 17,
    [EnumMember(Value = "REMOVE_GOOD_AFFECT")] RemoveGoodAffect  = 1 << 18,
    [EnumMember(Value = "CRUSH")] Crush = 1 << 19,
    [EnumMember(Value = "ATTACK_POISON")] AttackPoison = 1 << 20,
    [EnumMember(Value = "TOGGLE")] Toggle = 1 << 21,
    [EnumMember(Value = "DISABLE_BY_POINT_UP")] DisableByPointUp = 1 << 22,
    [EnumMember(Value = "CRUSH_LONG")] CrushLong = 1 << 23
}
