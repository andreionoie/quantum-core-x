using QuantumCore.API;
using QuantumCore.API.Core.Models;
using QuantumCore.API.Extensions;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Monsters;
using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Stats;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.World.Entities;
using static QuantumCore.Game.Constants.PlayerConstants;

namespace QuantumCore.Game.Constants;

public static class ScalingFormulas
{
    private const uint DexLevelCap = 90; // no increases in computed rating when level/DEX over 90 points
    
    // attack rating / hit-rate calibration factors
    private const double AttackRatingMinFactor = 0.7; // 70%
    private const double AttackRatingMaxFactor = 1.0; // 100%

    public static StatEngine.BaseValueSupplierFactory BasePointsForPlayer(IPlayerEntity player, IJobManager jobManager)
    {
        var pl = player.Player;
        var job = jobManager.Get(pl.PlayerClass);
        
        return point => point switch
        {
            EPoint.AttackSpeed => static () => DEFAULT_ATTACK_SPEED,
            EPoint.MoveSpeed => static () => DEFAULT_MOVEMENT_SPEED,
            EPoint.CastingSpeed => static () => DEFAULT_CASTING_SPEED,
            
            // Primary stats - base values from PlayerData
            EPoint.St => () => pl.St,
            EPoint.Ht => () => pl.Ht,
            EPoint.Dx => () => pl.Dx,
            EPoint.Iq => () => pl.Iq,
            
            // Maxes computed from job constants
            EPoint.MaxHp => () =>
            {
                var startHp = (int)job.StartHp;
                var hpPerHt = (int)job.HpPerHt;
                var hpPerLevel = (int)job.HpPerLevel;

                var ht = (int)player.GetPoint(EPoint.Ht);
                var baseHp = startHp + hpPerHt * ht + hpPerLevel * pl.Level;

                var hpPercentageBonus = (int)player.GetPoint(EPoint.MaxHpPercentage);
                return baseHp + baseHp * hpPercentageBonus / 100;
            },
            EPoint.MaxSp => () =>
            {
                var startSp = (int)job.StartSp;
                var spPerIq = (int)job.SpPerIq;
                var spPerLevel = (int)job.SpPerLevel;

                var iq = (int)player.GetPoint(EPoint.Iq);
                var baseSp = startSp + spPerIq * iq + spPerLevel * pl.Level;

                var spPercentageBonus = (int)player.GetPoint(EPoint.MaxSpPercentage);
                return baseSp + baseSp * spPercentageBonus / 100;
            },
            EPoint.MaxStamina => () =>
            {
                var jobInfo = jobManager.Get(pl.PlayerClass);
                var maxStamina = (int)jobInfo.MaxStamina;
                var staminaPerHt = (int)jobInfo.StaminaPerHt;
                
                var ht = (int)player.GetPoint(EPoint.Ht);
                
                return maxStamina + staminaPerHt * ht;
            },
            
            // Derived stats
            // Expecting to receive computed values from GetPoint() with all modifiers applied
            EPoint.MinAttackDamage => () => (int)player.GetPoint(EPoint.MinWeaponDamage),
            EPoint.MaxAttackDamage => () => (int)player.GetPoint(EPoint.MaxWeaponDamage),
            EPoint.AttackGrade => () =>
            {
                var str = (int)player.GetPoint(EPoint.St);
                var dex = (int)player.GetPoint(EPoint.Dx);
                var iq = (int)player.GetPoint(EPoint.Iq);
                
                var classBonus = pl.PlayerClass.GetClass() switch
                {
                    EPlayerClass.Warrior => str * 2,
                    EPlayerClass.Ninja => (str * 4 + dex * 2) / 3,
                    EPlayerClass.Sura => str * 2,
                    EPlayerClass.Shaman => (str * 4 + iq * 2) / 3,
                    _ => throw new ArgumentOutOfRangeException(pl.PlayerClass.GetClass().ToString())
                };

                return pl.Level * 2 + classBonus;
            },
            EPoint.DefenceGrade => () =>
            {
                var con = (int)player.GetPoint(EPoint.Ht);
                return pl.Level + con * 8 / 10;
            },
            EPoint.MagicAttackGrade => () =>
            {
                var con = (int)player.GetPoint(EPoint.Iq);
                return pl.Level * 2 + con * 2;
            },
            EPoint.MagicDefenceGrade => () => 
            {
                var con = (int)player.GetPoint(EPoint.Ht);
                var iq = (int)player.GetPoint(EPoint.Iq);
                return pl.Level + (iq * 3 + con) / 3;
            },
            
            // Aggregated defence: grade adjusted by percentage bonus
            EPoint.Defence => () =>
            {
                var grade = (int)player.GetPoint(EPoint.DefenceGrade);
                var percentageBonus = (int)player.GetPoint(EPoint.DefenceBonus);
                return grade * (100 + percentageBonus) / 100;
            },

            // Hit rate derived from the same Dex/Level weighting used by attack rating
            EPoint.HitRate => () =>
            {
                var w = WeightedDexLvlAvg(player.GetPoint(EPoint.Dx), player.GetPoint(EPoint.Level));
                var t = w / (double)DexLevelCap; // 0..1
                var factor = Lerp(AttackRatingMinFactor, AttackRatingMaxFactor, t);
                return (int)(factor * 100);
            },
            
            _ => static () => 0
        };
    }

    // Invalidate cached values of dependents when their bases change - mirrored from the BasePointsForPlayer() formulas above
    public static void RegisterDependenciesForPlayer(IStatEngine stats, EPlayerClass playerClass)
    {
        EPoint[] classUniqueAtkGradeDeps = playerClass switch
        {
            EPlayerClass.Warrior => [EPoint.St],
            EPlayerClass.Ninja => [EPoint.St, EPoint.Dx],
            EPlayerClass.Sura => [EPoint.St],
            EPlayerClass.Shaman => [EPoint.St, EPoint.Iq],
            _ => throw new ArgumentOutOfRangeException(playerClass.ToString())
        };
        stats.RegisterDependency(EPoint.AttackGrade, [..classUniqueAtkGradeDeps, EPoint.Level]);
        
        stats.RegisterDependency(EPoint.DefenceGrade, EPoint.Level, EPoint.Ht);
        stats.RegisterDependency(EPoint.MagicAttackGrade, EPoint.Level, EPoint.Iq);
        stats.RegisterDependency(EPoint.MagicDefenceGrade, EPoint.Level, EPoint.Iq, EPoint.Ht);
        
        stats.RegisterDependency(EPoint.MaxHp, EPoint.Ht, EPoint.Level, EPoint.MaxHpPercentage);
        stats.RegisterDependency(EPoint.MaxSp, EPoint.Iq, EPoint.Level, EPoint.MaxSpPercentage);
        stats.RegisterDependency(EPoint.MaxStamina, EPoint.Ht);
        
        stats.RegisterDependency(EPoint.MinAttackDamage, EPoint.MinWeaponDamage);
        stats.RegisterDependency(EPoint.MaxAttackDamage, EPoint.MaxWeaponDamage);
        
        stats.RegisterDependency(EPoint.Defence, EPoint.DefenceGrade, EPoint.DefenceBonus);
        stats.RegisterDependency(EPoint.HitRate, EPoint.Dx, EPoint.Level);
    }

    // Since we only rely on the proto data for the base points, we reuse a shared instance for all mobs of the same proto
    public static StatEngine.BaseValueSupplierFactory BasePointsForMonsters(MonsterData proto)
    {
        return point => point switch
        {
            EPoint.MoveSpeed => () => proto.MoveSpeed,
            EPoint.AttackSpeed or EPoint.CastingSpeed => () => proto.AttackSpeed,
            EPoint.AttackGrade or EPoint.MagicAttackGrade => () => (proto.Level + proto.St) * 2,
            EPoint.DefenceGrade or EPoint.Defence  => () => proto.Level + proto.Ht + proto.Defence,

            _ => () =>
            {
                Environment.FailFast($"The point {point} should not be handled through {nameof(StatEngine)} for monsters." +
                                     $"It should be added explicitly in {nameof(IMonsterEntity.GetPoint)}'s implementation.");
                return 0;
            }
        };
    }

    public static double ComputeAttackRating(IEntity attacker, IEntity target)
    {
        // Tunable parameters 
        const uint NumeratorSlope = 2;   // target's defense scaling - higher means faster growth ramp
        const uint NumeratorOffset = 5;  // target's defense base offset

        var atkStat = WeightedDexLvlAvg(attacker.GetPoint(EPoint.Dx), attacker.GetPoint(EPoint.Level));
        var t = atkStat / (double)DexLevelCap;
        var attackerRating = Lerp(AttackRatingMinFactor, AttackRatingMaxFactor, t);

        const double TargetDefenseMax = AttackRatingMaxFactor - AttackRatingMinFactor;
        
        var defStat = WeightedDexLvlAvg(target.GetPoint(EPoint.Dx), attacker.GetPoint(EPoint.Level));

        // diminishing returns as defStat increases
        var fraction = (defStat * NumeratorSlope + NumeratorOffset) / (DexLevelCap + defStat + NumeratorOffset);
        var defensePenalty = Lerp(0, TargetDefenseMax, fraction);

        return attackerRating - defensePenalty;
    }

    public static int ComputeMeleeDamage(IEntity attacker, IEntity target)
    {
        var baseDmg = 2 * CoreRandom.GenerateInt32(attacker.GetMinDamage(), attacker.GetMaxDamage() + 1);
        
        var attack = (int)(attacker.GetPoint(EPoint.AttackGrade) + baseDmg - 2 * attacker.GetPoint(EPoint.Level));
        attack = (int)Math.Floor(attack * ComputeAttackRating(attacker, target));
        attack += 2 * (int)attacker.GetPoint(EPoint.Level);
        attack += 2 * attacker.GetBonusDamage();

        attack = (int)Math.Floor(attack * AttackBonusBaseScaling(attacker));
        
        attack = (int)Math.Floor(attack * AttackBonusVsMonsterScaling(attacker, target));
        attack = (int)Math.Floor(attack * AttackBonusVsPlayerScaling(attacker, target));
        
        attack = (int)Math.Floor(attack / ClassResistScaling(attacker, target));
        attack = (int)Math.Floor(attack / PvmResistScaling(attacker, target));

        if (attacker is MonsterEntity monster)
        {
            attack = (int)Math.Floor(attack * monster.Proto.DamageMultiply);
        }

        var defence = (int)target.GetPoint(EPoint.Defence);
        baseDmg = Math.Max(0, attack - defence);
        if (baseDmg < 3)
        {
            baseDmg = CoreRandom.GenerateInt32(1, 6);
        }

        // todo reduce damage by weapon type resist
        
        return baseDmg;
    }

    public static double AttackBonusBaseScaling(IEntity attacker)
    {
        var scale = 1.0;
        ScaleByPercentage(ref scale, attacker.GetPoint(EPoint.AttackBonus) + attacker.GetPoint(EPoint.MeleeMagicAttackBonusPer));
        return scale;
    }

    public static double AttackBonusVsMonsterScaling(IEntity attacker, IEntity target)
    {
        if (target is IMonsterEntity monster)
        {
            var scale = 1.0;
            var raceBonusPoint = GameExtensions.GetRaceBonusPoint((ERaceFlag)monster.Proto.RaceFlag);
            if (raceBonusPoint.HasValue)
            {
                ScaleByPercentage(ref scale, attacker.GetPoint(raceBonusPoint.Value));
            }

            ScaleByPercentage(ref scale, attacker.GetPoint(EPoint.AttackBonusMonster));
            return scale;
        }

        return 1.0;
    }

    public static double AttackBonusVsPlayerScaling(IEntity attacker, IEntity target)
    {
        if (target is not IPlayerEntity playerTarget)
        {
            return 1.0;
        }

        var scale = 1.0;
        var classBonusPoint = GameExtensions.GetClassBonusPoint(playerTarget.Player.PlayerClass.GetClass());

        ScaleByPercentage(ref scale, attacker.GetPoint(EPoint.AttackBonusHuman));
        ScaleByPercentage(ref scale, attacker.GetPoint(classBonusPoint));
        return scale;
    }

    public static double ClassResistScaling(IEntity attacker, IEntity target)
    {
        if (attacker is IPlayerEntity playerAttacker)
        {
            var scale = 1.0;
            var classResistPoint = GameExtensions.GetClassResistPoint(playerAttacker.Player.PlayerClass.GetClass());

            ScaleByPercentage(ref scale, target.GetPoint(classResistPoint));
            return scale;
        }

        return 1.0;
    }

    public static double PvmResistScaling(IEntity attacker, IEntity target)
    {
        if (attacker is IMonsterEntity monsterAttacker && target is IPlayerEntity playerTarget)
        {
            var scale = 1.0;
            var resistPoint = GameExtensions.GetElementsResistPoint((ERaceFlag)monsterAttacker.Proto.RaceFlag);
            if (resistPoint.HasValue)
            {
                ScaleByPercentage(ref scale, 0.3 * playerTarget.GetPoint(resistPoint.Value));
            }
            return scale;
        }

        return 1.0;
    }

    public static double GetScaledSkillLevel(byte skillLevel, short skillLevelMax)
    {
        // piecewise linear formula
        var scParams = skillLevel switch
        {
            // Normal 1 - 19: 5% - 40%
            1                   => (basePower: 5, extraPowerPerLevel: 0, levelBase: 1),
            2                   => (basePower: 6, extraPowerPerLevel: 0, levelBase: 2),
            >= 3 and <= 19      => (basePower: 8, extraPowerPerLevel: 2, levelBase: 3),

            // Master 20 - 29: 50% - 72%
            >= 20 and <= 25     => (basePower: 50, extraPowerPerLevel: 2, levelBase: 20),
            >= 26 and <= 29     => (basePower: 63, extraPowerPerLevel: 3, levelBase: 26),

            // Grand Master 30 - 39: 82% - 115%
            >= 30 and <= 33     => (basePower: 82,  extraPowerPerLevel: 3, levelBase: 30),
            >= 34 and <= 37     => (basePower: 94,  extraPowerPerLevel: 4, levelBase: 34),
            >= 38 and <= 39     => (basePower: 110, extraPowerPerLevel: 5, levelBase: 38),

            // Perfect Master: 125%
            40                  => (basePower: 125, extraPowerPerLevel: 0, levelBase: 40),

            _ => throw new ArgumentOutOfRangeException(nameof(skillLevel), skillLevel, "Only 1-40 supported")
        };

        var percentage = scParams.basePower + (skillLevel - scParams.levelBase) * scParams.extraPowerPerLevel;
        
        return percentage / 100.0 * skillLevelMax;
    }

    public static uint GetScaledCooldownMs(uint baseCooldownSec, uint castingSpeed)
    {
        const uint MaxCooldownPenaltyPercentage = 200;
        
        var speedPercentage = castingSpeed switch
        {
            // linearly decrease factor by CastingSpeed point value
            // 0: 200% longer cooldown
            // 50: 150% longer cooldown
            // 99: basically unmodified cooldown
            < DEFAULT_CASTING_SPEED => MaxCooldownPenaltyPercentage - castingSpeed,
            
            // inversely proportional - diminishing returns the higher CastingSpeed point gets
            // 125: 20% shorter cooldown
            // 166: 40% shorter cooldown
            // 200: 50% shorter cooldown
            // 250: 60% shorter cooldown
            >= DEFAULT_CASTING_SPEED => DEFAULT_CASTING_SPEED * DEFAULT_CASTING_SPEED / castingSpeed
        };

        var cooldownMs = baseCooldownSec * 1000 * speedPercentage / 100;

        return cooldownMs;
    }

    private static uint WeightedDexLvlAvg(uint dex, uint level)
    {
        const uint DexWeight = 2;
        const uint LevelWeight = 1;

        var weighted = (dex * DexWeight + level * LevelWeight) / (DexWeight + LevelWeight);
        return Math.Min(DexLevelCap, weighted);
    }
    
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    
    private static void ScaleByPercentage(ref double scale, double percent) => scale *= (100.0 + percent) / 100.0; 
}
