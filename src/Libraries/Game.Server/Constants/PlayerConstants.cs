using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Affects;
using static QuantumCore.API.Game.Types.Skills.EAffectType;
using static QuantumCore.API.Game.Types.Skills.ESkill;

namespace QuantumCore.Game.Constants;

public static class PlayerConstants
{
    public const int MAX_PLAYERS_PER_ACCOUNT = 4;
    public const int PLAYER_NAME_MAX_LENGTH = 25;

    public const byte DEFAULT_ATTACK_SPEED = 100;
    public const byte DEFAULT_MOVEMENT_SPEED = 150;
    public const byte DEFAULT_CASTING_SPEED = 100;
    public const uint RESPAWN_HEALTH = 50;
    public const uint RESPAWN_MANA = 50;
    
    public static readonly AffectType[] DebuffAffects =
    [
        AffectType.FromSkill(SpiritStrike),
        AffectType.From(Poison),
        AffectType.From(Stun),
        AffectType.From(Fire),
        AffectType.From(Slow)
    ];

    public static readonly AffectType[] BuffAffects =
    [
        AffectType.FromSkill(AuraOfTheSword),
        AffectType.FromSkill(BerserkerFury),
        AffectType.FromSkill(StrongBody),
        
        AffectType.FromSkill(Stealth),
        AffectType.FromSkill(FeatherWalk),
        
        AffectType.FromSkill(EnchantedBlade),
        AffectType.FromSkill(EnchantedArmor),
        AffectType.FromSkill(Fear),
        AffectType.FromSkill(DarkProtection),
        
        AffectType.FromSkill(Blessing),
        AffectType.FromSkill(Reflect),
        AffectType.FromSkill(Swiftness),
        AffectType.FromSkill(DragonAid),
        AffectType.FromSkill(AttackUp),
        
        AffectType.From(AttackSpeed),
        AffectType.From(MoveSpeed),
        AffectType.From(Strength),
        AffectType.From(Dexterity),
        AffectType.From(Constitution),
        AffectType.From(Intelligence)
    ];
}
