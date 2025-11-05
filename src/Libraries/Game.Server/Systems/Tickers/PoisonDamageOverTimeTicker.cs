using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Combat;
using QuantumCore.API.Game.Types.Monsters;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Tickers;
using QuantumCore.Game.World.Entities;
using static QuantumCore.Game.Constants.SchedulingConstants;

namespace QuantumCore.Game.Systems.Tickers;

public sealed class PoisonDamageOverTimeTicker<TAffectable>(TAffectable entity, uint intervalMs = PoisonTickIntervalMs)
    : GatedTickerEngine<TAffectable>(entity, TimeSpan.FromMilliseconds(intervalMs))
    where TAffectable : IEntity, IAffectable
{
    private const double DefaultPoisonDamagePercentagePerSecond = 1.666; // ~1.666% of MaxHP per second
    private static readonly Dictionary<EMonsterLevel, double> DamageScalingFactorByRank = new()
        {
            { EMonsterLevel.Pawn,    1.6 },
            { EMonsterLevel.SPawn,   1.0 },
            { EMonsterLevel.Knight,  0.8 },
            { EMonsterLevel.SKnight, 0.6 },
            { EMonsterLevel.Boss,    0.5 },
            { EMonsterLevel.King,    0.02 },
        };
    
    private double _fractionalDamageAccum;
    private bool _wasPoisoned;

    protected override bool UpdateState(TAffectable poisonedEntity, TimeSpan processed)
    {
        var isPoisoned = poisonedEntity.Affects.GetActiveFlags().Has(EAffect.Poison);
        if (!isPoisoned)
        {
            _fractionalDamageAccum = 0;
            _wasPoisoned = false;
            return false;
        }

        // Arm initial 1s delay when poison just became active
        if (!_wasPoisoned)
        {
            ArmInitialDelay(TimeSpan.FromSeconds(1));
            _wasPoisoned = true;
        }

        var anyDamage = false;

        var maxHp = poisonedEntity.GetPoint(EPoint.MaxHp);
        var ratePerSecond = GetPoisonDamageFractionPerSecond(poisonedEntity); // fraction of MaxHP per second
        _fractionalDamageAccum += maxHp * ratePerSecond * processed.TotalSeconds;

        var integralDamage = (int)Math.Floor(_fractionalDamageAccum);
        if (integralDamage <= 0)
        {
            return false;
        }

        var currentHp = (int)poisonedEntity.GetPoint(EPoint.Hp);
        var toApply = Math.Clamp(integralDamage, 0, Math.Max(0, currentHp - 1));   // never kill
        if (toApply > 0)
        {
            IEntity? attacker = null;
            var sourceVid = poisonedEntity.Affects.Active.FirstOrDefault(a => a.AffectFlag == EAffect.Poison)?.SourceAttackerId ?? 0u;
            if (sourceVid != 0 && poisonedEntity.Map is not null)
            {
                attacker = poisonedEntity.Map.GetEntity(sourceVid);
            }

            poisonedEntity.Damage(attacker!, EDamageType.Poison, toApply);
            anyDamage = true;
            _fractionalDamageAccum -= toApply;
        }

        return anyDamage;
    }

    private static double GetPoisonDamageFractionPerSecond(IEntity victim)
    {
        var basePercentage = DefaultPoisonDamagePercentagePerSecond;
        if (victim is MonsterEntity mob)
        {
            basePercentage *= DamageScalingFactorByRank.TryGetValue(mob.Rank, out var s) ? s : 1.0;
        }

        const double PerMilleScale = 1000.0;
        const double ReferenceTickSeconds = 3.0;
 
        // reduce point is defined in 1000s of MaxHP per 3 seconds
        var reduceFraction = victim.GetPoint(EPoint.PoisonReduce) / (PerMilleScale * ReferenceTickSeconds);
        return Math.Max(0, basePercentage / 100 - reduceFraction);
    }
}
