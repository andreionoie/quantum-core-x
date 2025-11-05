using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;

namespace QuantumCore.API.Systems.Affects;

public interface IAffectsController
{
    event Action<EntityAffect> AffectAdded;
    event Action<EntityAffect> AffectRemoved;

    IReadOnlyList<EntityAffect> Active { get; }
    void Upsert(EntityAffect affect);
    bool Remove(params EntityAffect[] affects);
    bool Remove(AffectType type, EPoint applyOn);
    bool RemoveAllOfType(AffectType type);
    void Clear(bool preserveNoClearOnDeath = false);
    EAffectFlags GetActiveFlags();
}
