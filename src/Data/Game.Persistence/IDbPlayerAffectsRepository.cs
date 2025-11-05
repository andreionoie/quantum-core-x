using QuantumCore.API;
using QuantumCore.API.Systems.Affects;

namespace QuantumCore.Game.Persistence;

public interface IDbPlayerAffectsRepository : IPlayerAffectsRepository
{
    Task SavePlayerAffectsAsync(uint playerId, IEnumerable<EntityAffect> affects);
    Task DeletePlayerAffectsAsync(uint playerId);
}
