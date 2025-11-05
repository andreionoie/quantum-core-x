using QuantumCore.API.Systems.Affects;

namespace QuantumCore.API;

public interface IPlayerAffectsRepository
{
    Task<ICollection<EntityAffect>> GetPlayerAffectsAsync(uint playerId);
}
