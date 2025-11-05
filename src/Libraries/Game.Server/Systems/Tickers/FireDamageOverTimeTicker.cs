using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Combat;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Tickers;
using static QuantumCore.Game.Constants.SchedulingConstants;

namespace QuantumCore.Game.Systems.Tickers;

public sealed class FireDamageOverTimeTicker<TAffectable>(TAffectable entity, uint intervalMs = PoisonTickIntervalMs)
    : GatedTickerEngine<TAffectable>(entity, TimeSpan.FromMilliseconds(intervalMs))
    where TAffectable : IEntity, IAffectable
{
    private const double ReferenceSecondsPerTick = 3.0; // skill_proto fire damage formula is specified per 3s interval

    private double _fractionalDamageAccum;
    private bool _wasOnFire;

    protected override bool UpdateState(TAffectable burningEntity, TimeSpan processed)
    {
        // select the fire affect with the highest damage
        var fireAffect = burningEntity.Affects.Active
            .Where(a => a is { AffectFlag: EAffect.Fire, ModifiedPointDelta: > 0 })
            .DefaultIfEmpty()
            .MaxBy(a => a?.ModifiedPointDelta);
        
        if (fireAffect is null)
        {
            _fractionalDamageAccum = 0;
            _wasOnFire = false;
            return false;
        }

        // when fire just started
        if (!_wasOnFire)
        {
            ArmInitialDelay(TimeSpan.FromSeconds(1));
            _wasOnFire = true;
        }

        var perTickDamage = fireAffect.ModifiedPointDelta;
        var perSecondDamage = perTickDamage / ReferenceSecondsPerTick;
        _fractionalDamageAccum += perSecondDamage * processed.TotalSeconds;

        var integralDamage = (int)Math.Floor(_fractionalDamageAccum);
        if (integralDamage <= 0)
        {
            return false;
        }

        IEntity? attacker = null;
        var sourceVid = fireAffect.SourceAttackerId;
        if (sourceVid != 0 && burningEntity.Map is not null)
        {
            attacker = burningEntity.Map.GetEntity(sourceVid);
        }

        burningEntity.Damage(attacker!, EDamageType.Fire, integralDamage);
        _fractionalDamageAccum -= integralDamage;
        
        return true;
    }
}

