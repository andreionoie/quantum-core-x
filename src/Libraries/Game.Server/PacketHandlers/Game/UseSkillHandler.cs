using System.Diagnostics;
using EnumsNET;
using Microsoft.Extensions.Logging;
using QuantumCore.API;
using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.Skills;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Combat;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Game.World;
using QuantumCore.API.PluginTypes;
using QuantumCore.API.Systems.Affects;
using QuantumCore.API.Systems.Formulas;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Constants;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.Packets;
using QuantumCore.Game.Persistence.Entities;
using QuantumCore.Game.World.Entities;
using static QuantumCore.API.Systems.Formulas.EFormulaVariable;
using static QuantumCore.Game.Constants.SchedulingConstants;

namespace QuantumCore.Game.PacketHandlers.Game;

public class UseSkillHandler(ILogger<UseSkillHandler> logger, ISkillManager skillManager, IItemManager itemManager)
    : IGamePacketHandler<PlayerUseSkill>
{
    // Note: Attack flags are handled minimally by interpreting negative HP deltas as damage
    private const ESkillFlags SupportedFlags =
        ESkillFlags.None |
        ESkillFlags.SelfOnly |
        ESkillFlags.Toggle |
        ESkillFlags.RemoveBadAffect |
        ESkillFlags.UseHpAsCost |
        ESkillFlags.AttackPoison |
        ESkillFlags.AttackStun |
        ESkillFlags.AttackSlow;

    public Task ExecuteAsync(GamePacketContext<PlayerUseSkill> ctx, CancellationToken token = default)
    {
        #region Validations

        if (ctx.Connection.Player is null)
        {
            logger.LogError("Cannot handle skill {SkillId} for null player", ctx.Packet.SkillId);
            return Task.CompletedTask;
        }
        var player = ctx.Connection.Player;
        
        if (!EnumUtils<ESkill>.TryCast(ctx.Packet.SkillId, out var skill))
        {
            logger.LogWarning("Unknown skill id {SkillId}", ctx.Packet.SkillId);
            return Task.CompletedTask;
        }

        var skillData = skillManager.GetSkill(skill);
        if (skillData is null)
        {
            logger.LogWarning("Skill proto missing for id {SkillId}", skill);
            return Task.CompletedTask;
        }

        var playerSkill = player.Skills[skill];
        if (playerSkill is null || playerSkill.Level <= 0)
        {
            logger.LogDebug("Player {Player} attempted to use skill {Skill} without learning it", player.Name, skill);
            return Task.CompletedTask;
        }

        if ((skillData.Flags & ~SupportedFlags) != 0)
        {
            player.SendChatInfo($"Skill {skillData.Id} with flags {skillData.Flags} not fully supported.");
        }
        logger.LogDebug("Skill cast: Player={Player} Skill={Skill} Flags={Flags} TargetVid={TargetVid} Affect=({Aff1}|{Aff2})",
            player.Name, skill, skillData.Flags, ctx.Packet.TargetVid, skillData.AffectFlag, skillData.AffectFlag2);
        
        #endregion
        
        #region Find target for skill
        
        IAffectable? target;
        if (skillData.Flags.HasFlag(ESkillFlags.SelfOnly) || skill == ESkill.FlameSpirit && ctx.Packet.TargetVid == 0)
        {
            target = player;
        }
        else
        {
            var targetEntity = player.Map?.GetEntity((uint)ctx.Packet.TargetVid);
            if (targetEntity is IAffectable affectable)
            {
                target = affectable;
            }
            else
            {
                logger.LogDebug("Skill {Skill} target {TargetVid} not found or not affectable", skill, ctx.Packet.TargetVid);
                return Task.CompletedTask;
            }
        }
        
        #endregion
        
        if (skillData.Flags.HasFlag(ESkillFlags.Toggle))
        {
            if (player.Affects.RemoveAllOfType(AffectType.FromSkill(skillData.Id)))
            {
                logger.LogDebug("Player {Player} toggled off skill {Skill}", player.Name, skill);
                return Task.CompletedTask;
            }
        }
        
        var parameters = PopulateFormulaParameters(player, (IEntity)target, skillData, playerSkill, itemManager);

        #region Cooldown Validation
        
        var baseCooldownSec = checked((uint)skillData.CooldownFormula.Evaluate(parameters));
        var cooldown = TimeSpan.FromMilliseconds(
            ScalingFormulas.GetScaledCooldownMs(baseCooldownSec, player.GetPoint(EPoint.CastingSpeed))
        );
        
        if (playerSkill.LastUsedServerTime.HasValue)
        {
            var errorMargin = TimeSpan.FromMilliseconds(100);
            var nextAvailable = playerSkill.LastUsedServerTime.Value + cooldown.TotalMilliseconds;
            
            if (GameServer.Instance.ServerTime + errorMargin.TotalMilliseconds < nextAvailable)
            {
                var remaining = TimeSpan.FromMilliseconds(nextAvailable - GameServer.Instance.ServerTime);
                player.SendChatInfo($"Skillhack used? {remaining.TotalSeconds:F2}s remaining for {skill}, canceled skill use.");
                logger.LogDebug("Skill {Skill} is on cooldown for player {Player}: {Remaining:F2}s remaining",
                    skill, player.Name, remaining.TotalSeconds);
                return Task.CompletedTask;
            }
        }

        #endregion
        
        if (!TryConsumeSkillCost(player, skillData, parameters))
        {
            logger.LogDebug("Skillhack used? {Skill} cannot be cast due to cost for player {Player}", skillData.Id, player.Name);
            return Task.CompletedTask;
        }

        try
        {
            player.Affects.RemoveAllOfType(AffectType.From(EAffectType.InvisibleRespawn));

            var primaryDelta = (int)skillData.PointFormula.Evaluate(parameters);
            var primaryDurationSec = (int)skillData.DurationFormula.Evaluate(parameters);
            var spPerSec = (int)skillData.DurationSpCostFormula.Evaluate(parameters);

            var secondaryDelta = (int)skillData.PointFormula2.Evaluate(parameters);
            var secondaryDurationSec = (int)skillData.DurationFormula2.Evaluate(parameters);

            logger.LogDebug("Skill eval for Player={Player} Skill={Skill}: P1={P1} dP1={d1} dur1={t1}s | P2={P2} dP2={d2} dur2={t2}s | SP/sec={sp}",
                player.Name, skill, skillData.PointOn, primaryDelta, primaryDurationSec,
                skillData.PointOn2, secondaryDelta, secondaryDurationSec, spPerSec);

            #region Cleanse debuffs

            if (skillData.Flags.HasFlag(ESkillFlags.RemoveBadAffect))
            {
                var cleanseTarget = target as IPlayerEntity ?? player;
                if (CoreRandom.PercentageCheck(secondaryDelta))
                {
                    foreach (var debuff in PlayerConstants.DebuffAffects)
                    {
                        cleanseTarget.Affects.RemoveAllOfType(debuff);
                    }
                }
            }

            #endregion

            if (IsAttackSkill(skillData) && skillData.PointOn == EPoint.Hp && primaryDelta < 0)
            {
                var absoluteDamage = -primaryDelta;
                var damageType = ResolveDamageType(skillData);

                if (skillData.Flags.HasFlag(ESkillFlags.Splash))
                {
                    // AoE around center (self for self-only skills, otherwise target)
                    var center = skillData.Flags.HasFlag(ESkillFlags.SelfOnly) || target == player
                        ? player
                        : target as IEntity ?? player;

                    var range = skillData.SplashRange > 0 ? skillData.SplashRange : 500u;
                    var maxHit = skillData.MaxHit > 0 ? skillData.MaxHit : int.MaxValue;
                    var hitCount = 0;

                    var processed = new HashSet<uint>();

                    // always apply to the center victim first (but not damage the caster in self-centered AoE)
                    if (center != player)
                    {
                        AttackTargetAreaOfEffect(center);
                    }

                    foreach (var e in player.NearbyEntities.ToList())
                    {
                        if (hitCount >= maxHit) break;
                        if (processed.Contains(e.Vid)) continue;
                        if (MathUtils.Distance(center.PositionX, center.PositionY, e.PositionX, e.PositionY) > range)
                            continue;

                        AttackTargetAreaOfEffect(e);
                    }

                    void AttackTargetAreaOfEffect(IEntity e)
                    {
                        if (hitCount >= maxHit) return;
                        if (e is not IAffectable victim || e.Dead) return;
                        processed.Add(e.Vid);
                        logger.LogDebug("AoE hit: Skill={Skill} Victim={VictimVid} Range={Range}", skillData.Id, e.Vid, range);

                        e.Damage(player, damageType, absoluteDamage);
                        hitCount++;
                        if (e.Dead)
                        {
                            return;
                        }

                        // post-hit status and dispel per victim
                        if (skillData.Flags.HasFlag(ESkillFlags.RemoveGoodAffect))
                        {
                            DispelBuffs(victim, skillData, secondaryDelta, secondaryDurationSec);
                        }

                        if (!victim.Affects.GetActiveFlags().Has(EAffect.Stun))
                        {
                            if (skillData.Flags.HasFlag(ESkillFlags.AttackSlow))
                                ApplySlow(e, victim, secondaryDelta, secondaryDurationSec);
                            else if (skillData.Flags.HasFlag(ESkillFlags.AttackStun))
                                ApplyStun(e, victim, secondaryDelta, secondaryDurationSec);
                            else if (skillData.Flags.HasFlag(ESkillFlags.AttackFireContinuous))
                                ApplyFire(victim, player, secondaryDelta, secondaryDurationSec);
                            else if (skillData.Flags.HasFlag(ESkillFlags.AttackPoison))
                                ApplyPoison(victim, player, secondaryDelta);
                        }
                    }
                }
                else
                {
                    if (target is IEntity entTarget && target != player && !entTarget.Dead)
                    {
                        entTarget.Damage(player, damageType, absoluteDamage);

                        if (entTarget.Dead)
                        {
                            return Task.CompletedTask;
                        }

                        if (skillData.Flags.HasFlag(ESkillFlags.RemoveGoodAffect))
                        {
                            DispelBuffs(target, skillData, secondaryDelta, secondaryDurationSec);
                        }

                        if (!target.Affects.GetActiveFlags().Has(EAffect.Stun))
                        {
                            if (skillData.Flags.HasFlag(ESkillFlags.AttackSlow))
                                ApplySlow(entTarget, target, secondaryDelta, secondaryDurationSec);
                            else if (skillData.Flags.HasFlag(ESkillFlags.AttackStun))
                                ApplyStun(entTarget, target, secondaryDelta, secondaryDurationSec);
                            else if (skillData.Flags.HasFlag(ESkillFlags.AttackFireContinuous))
                                ApplyFire(target, player, secondaryDelta, secondaryDurationSec);
                            else if (skillData.Flags.HasFlag(ESkillFlags.AttackPoison))
                                ApplyPoison(target, player, secondaryDelta);
                        }
                    }
                }

                return Task.CompletedTask;
            }

            if (IsSkillBlockedByDispel(target, skillData))
            {
                return Task.CompletedTask;
            }

            if (!IsAttackSkill(skillData) && target is not PlayerEntity)
            {
                // fallback to self if mob was selected as target
                target = player;
            }

            if (skillData.PointOn != EPoint.None)
            {
                if (primaryDurationSec == 0)
                {
                    ApplyImmediatePoint(target, skillData.PointOn, primaryDelta);
                }
                else
                {
                    var isFlameSpirit = skillData is { Id: ESkill.FlameSpirit, AffectFlag: EAffect.None };
                    var modifiedPointId = skillData.PointOn;
                    var modifiedPointDelta = primaryDelta;
                    if (isFlameSpirit)
                    {
                        // Flame Spirit is a toggle with periodic SP cost; it should not modify points directly
                        modifiedPointId = EPoint.None;
                        modifiedPointDelta = 0;
                    }

                    target.Affects.Upsert(new EntityAffect
                    {
                        AffectType = AffectType.FromSkill(skillData.Id),
                        AffectFlag = isFlameSpirit ? EAffect.FlameSpirit : skillData.AffectFlag,
                        ModifiedPointId = modifiedPointId,
                        ModifiedPointDelta = modifiedPointDelta,
                        RemainingDuration = TimeSpan.FromSeconds(primaryDurationSec),
                        SpCostPerSecond = spPerSec
                    });
                }
            }

            if (skillData.PointOn2 != EPoint.None)
            {
                if (secondaryDurationSec == 0)
                {
                    ApplyImmediatePoint(target, skillData.PointOn2, secondaryDelta);
                }
                else
                {
                    target.Affects.Upsert(new EntityAffect
                    {
                        AffectType = AffectType.FromSkill(skillData.Id),
                        AffectFlag = skillData.AffectFlag2,
                        ModifiedPointId = skillData.PointOn2,
                        ModifiedPointDelta = secondaryDelta,
                        RemainingDuration = TimeSpan.FromSeconds(secondaryDurationSec)
                    });
                }
            }
        }
        finally
        {
            // Always set timestamp before returning
            playerSkill.LastUsedServerTime = GameServer.Instance.ServerTime;
        }

        return Task.CompletedTask;
    }

    private static bool IsAttackSkill(SkillData skillData)
    {
        // treat Flame Spirit as a self-buff toggle, not as an attack
        if (skillData.Id == ESkill.FlameSpirit)
        {
            return false;
        }

        // Self-only toggles are generally buffs, not attacks
        if (skillData.Flags.HasFlag(ESkillFlags.Toggle) && skillData.Flags.HasFlag(ESkillFlags.SelfOnly))
        {
            return false;
        }

        return skillData.Flags.HasFlag(ESkillFlags.Attack) ||
               skillData.Flags.HasFlag(ESkillFlags.UseMeleeDamage) ||
               skillData.Flags.HasFlag(ESkillFlags.UseMagicDamage) ||
               skillData.Flags.HasFlag(ESkillFlags.UseArrowDamage);
    }

    private static EDamageType ResolveDamageType(SkillData skillData)
    {
        if (skillData.Flags.HasFlag(ESkillFlags.UseArrowDamage))
        {
            return EDamageType.NormalRange;
        }

        return EDamageType.Normal;
    }

    private static void DispelBuffs(IAffectable target, SkillData skillData, int successPercentage, int durationSec)
    {
        if (!skillData.Flags.HasFlag(ESkillFlags.RemoveGoodAffect))
        {
            return;
        }

        if (CoreRandom.PercentageCheck(successPercentage))
        {
            foreach (var buff in PlayerConstants.BuffAffects)
            {
                target.Affects.RemoveAllOfType(buff);
            }

            target.Affects.Upsert(new EntityAffect
            {
                AffectType = AffectType.FromSkill(skillData.Id),
                AffectFlag = EAffect.Dispel,
                RemainingDuration = TimeSpan.FromSeconds(durationSec),
                DoNotPersist = true
            });
        }
    }

    private static bool IsSkillBlockedByDispel(IAffectable target, SkillData skillData)
    {
        if (target.Affects.GetActiveFlags().Has(EAffect.Dispel))
        {
            if (PlayerConstants.BuffAffects.Contains(AffectType.FromSkill(skillData.Id)))
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyStun(IEntity target, IAffectable targetAff, int successPercentage, int duration)
    {
        if (!target.TryImmunityCheck(EImmunityFlags.StunImmunity) && CoreRandom.PercentageCheck(successPercentage))
        {
            targetAff.Affects.Upsert(new EntityAffect
            {
                AffectType = AffectType.From(EAffectType.Stun),
                AffectFlag = EAffect.Stun,
                RemainingDuration = TimeSpan.FromSeconds(duration),
                DoNotPersist = true
            });
        }
    }
    
    private static void ApplySlow(IEntity target, IAffectable targetAff, int successPercentage, int duration)
    {
        if (!target.TryImmunityCheck(EImmunityFlags.SlowImmunity) && CoreRandom.PercentageCheck(successPercentage))
        {
            targetAff.Affects.Upsert(new EntityAffect
            {
                AffectType = AffectType.From(EAffectType.Slow),
                AffectFlag = EAffect.Slow,
                ModifiedPointId = EPoint.MoveSpeed,
                ModifiedPointDelta = DefaultMovementDebuffValue,
                RemainingDuration = TimeSpan.FromSeconds(duration),
                DoNotPersist = true
            });
        }
    }
    
    private static void ApplyFire(IAffectable target, IPlayerEntity attacker, int damagePerTick, int successPercentage)
    {
        const int SecondsPerTick = 3;
        const int DefaultFireTicksCount = 5;
        
        if (CoreRandom.PercentageCheck(successPercentage))
        {
            target.Affects.Upsert(new EntityAffect
            {
                AffectType = AffectType.From(EAffectType.Fire),
                AffectFlag = EAffect.Fire,
                ModifiedPointDelta = damagePerTick,
                RemainingDuration = TimeSpan.FromSeconds(DefaultFireTicksCount * SecondsPerTick),
                DoNotPersist = true,
                SourceAttackerId = attacker.Vid
            });
        }
    }
    
    private static void ApplyPoison(IAffectable target, IPlayerEntity attacker, int successPercentage)
    {
        if (CoreRandom.PercentageCheck(successPercentage))
        {
            target.Affects.Upsert(new EntityAffect
            {
                AffectType = AffectType.From(EAffectType.Poison),
                AffectFlag = EAffect.Poison,
                RemainingDuration = TimeSpan.FromSeconds(DefaultPoisonDurationSeconds),
                DoNotPersist = true,
                SourceAttackerId = attacker.Vid
            });
        }
    }
    
    private static void ApplyImmediatePoint(IAffectable target, EPoint point, int delta)
    {
        if (point == EPoint.None || delta == 0)
        {
            return;
        }

        if (target is IPlayerEntity player)
        {
            player.AddPoint(point, delta);
            player.SendPoints();
        }
        // TODO: handle other types of target e.g. monster?
    }

    private static bool TryConsumeSkillCost(IPlayerEntity player, SkillData skillData, Dictionary<EFormulaVariable, double> parameters)
    {
        var costPoint = skillData.Flags.HasFlag(ESkillFlags.UseHpAsCost) ? EPoint.Hp : EPoint.Sp;

        var toConsume = (int)skillData.SpCostFormula.Evaluate(parameters);
        
        Debug.Assert(toConsume >= 0, "Check your skill formula, SP cost should never be negative.");

        var current = player.GetPoint(costPoint);
        if (toConsume > current)
        {
            return false;
        }

        player.AddPoint(costPoint, -toConsume);
        return true;
    }
    
    private static Dictionary<EFormulaVariable, double> PopulateFormulaParameters(IPlayerEntity player,
        IEntity target, SkillData skill, ISkill playerSkill, IItemManager itemManager)
    {
        var parameters = new Dictionary<EFormulaVariable, double>(skill.AllRequiredVariables.Length);
        foreach (var variable in skill.AllRequiredVariables)
        {
            parameters[variable] = variable switch
            {
                SkillLevel => ScalingFormulas.GetScaledSkillLevel(playerSkill.Level, skill.MaxLevel),
                AttackRating => ScalingFormulas.ComputeAttackRating(player, target),
                AttackValue =>  // TODO: compute properly based on target as well as skill type (melee/ranged/magic etc)
                    ScalingFormulas.ComputeMeleeDamage(player, target),
                Level => player.GetPoint(EPoint.Level),
                Strength => player.GetPoint(EPoint.St),
                Constitution => player.GetPoint(EPoint.Ht),
                Dexterity => player.GetPoint(EPoint.Dx),
                Intelligence => player.GetPoint(EPoint.Iq),
                MaxHp => player.GetPoint(EPoint.MaxHp),
                MaxSp => player.GetPoint(EPoint.MaxSp),
                Defence => player.GetPoint(EPoint.DefenceGrade),
                OriginalDefence => player.GetPoint(EPoint.DefenceGrade) - player.GetPoint(EPoint.DefenceGradeBonus),
                WeaponAttack => 
                    (itemManager.GetItem(player.Inventory.GetWeaponId()) ?? null).RollWeaponDamage(),
                MagicWeaponAttack or MagicAttack => 
                    (itemManager.GetItem(player.Inventory.GetWeaponId()) ?? null).RollWeaponMagicDamage(),
                // TODO
                ChainCount => 0,
                HorseLevel => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(skill), $"Skill formula used unsupported variable '{variable}'.")
            };
        }

        return parameters;
    }
}
