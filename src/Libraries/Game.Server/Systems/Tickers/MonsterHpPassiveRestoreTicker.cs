using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Tickers;

namespace QuantumCore.Game.Systems.Tickers;

// We can set a sub-1Hz restore frequency in the GatedTickerEngine constructor,
// as the proto only defines it in integral seconds as a `byte` type.
public sealed class MonsterHpPassiveRestoreTicker(IMonsterEntity monster)
    : GatedTickerEngine<IMonsterEntity>(monster, TimeSpan.FromSeconds(Math.Max((byte)1, monster.Proto.RegenDelay)))
{
    private readonly TimeSpan _protoCycleSpan = TimeSpan.FromSeconds(Math.Max((byte)1, monster.Proto.RegenDelay));
    
    private double _fractionalHpAccum;

    protected override bool UpdateState(IMonsterEntity m, TimeSpan processed)
    {
        if (m.Type is not (EEntityType.Monster or EEntityType.MetinStone))
        {
            return false;
        }

        if (m.Dead || m.Health <= 0)
        {
            _fractionalHpAccum = 0;
            return false;
        }
        
        if (m.Affects.GetActiveFlags().Has(EAffect.Poison))
        {
            _fractionalHpAccum = 0;
            return false;
        }

        var maxHp = (long)m.Proto.Hp;
        if (m.Health >= maxHp)
        {
            return false;
        }

        var perCycle = Math.Max(1.0, m.Proto.RegenPercentage / 100.0 * maxHp);
        var perSecond = perCycle / _protoCycleSpan.TotalSeconds;

        _fractionalHpAccum += perSecond * processed.TotalSeconds;
        var toAdd = (int)Math.Floor(_fractionalHpAccum);
        if (toAdd <= 0)
        {
            return false;
        }

        var prev = m.Health;
        m.Health = Math.Clamp(m.Health + toAdd, 0, maxHp);
        _fractionalHpAccum -= toAdd;

        var changed = m.Health != prev;
        return changed;
    }
}
