using Microsoft.EntityFrameworkCore;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Affects;
using QuantumCore.Game.Persistence.Entities;
using QuantumCore.Game.Persistence.Extensions;

namespace QuantumCore.Game.Persistence;

public class DbPlayerAffectsRepository : IDbPlayerAffectsRepository
{
    private readonly GameDbContext _db;

    public DbPlayerAffectsRepository(GameDbContext db)
    {
        _db = db;
    }

    public async Task<ICollection<EntityAffect>> GetPlayerAffectsAsync(uint playerId)
    {
        return await _db.PlayerAffects
            .AsNoTracking()
            .Where(x => x.PlayerId == playerId)
            .SelectEntityAffect()
            .ToArrayAsync();
    }

    public async Task SavePlayerAffectsAsync(uint playerId, IEnumerable<EntityAffect> affects)
    {
        // Remove all existing affects for this player
        await DeletePlayerAffectsAsync(playerId);

        // Add new affects (excluding DoNotPersist affects)
        var persistableAffects = affects
            .Where(a => !a.DoNotPersist)
            .Select(a => ToPlayerAffect(playerId, a))
            .ToList();

        if (persistableAffects.Count > 0)
        {
            _db.PlayerAffects.AddRange(persistableAffects);
            await _db.SaveChangesAsync();
        }
    }

    public async Task DeletePlayerAffectsAsync(uint playerId)
    {
        await _db.PlayerAffects
            .Where(x => x.PlayerId == playerId)
            .ExecuteDeleteAsync();
    }

    private static PlayerAffect ToPlayerAffect(uint playerId, EntityAffect affect)
    {
        var (affectTypeValue, isSkill) = affect.AffectType.Match(
            affectType => ((int)affectType, false),
            skill => ((int)skill, true)
        );

        return new PlayerAffect
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            AffectTypeValue = affectTypeValue,
            IsSkill = isSkill,
            AffectFlag = (byte)affect.AffectFlag,
            ModifiedPointId = (byte)affect.ModifiedPointId,
            ModifiedPointDelta = affect.ModifiedPointDelta,
            SpCostPerSecond = affect.SpCostPerSecond,
            DoNotPersist = affect.DoNotPersist,
            DoNotClearOnDeath = affect.DoNotClearOnDeath,
            RemainingDurationMs = (long)affect.RemainingDuration.TotalMilliseconds,
            FractionalSpCostAccumulator = affect.FractionalSpCostAccumulator,
            SourceAttackerId = affect.SourceAttackerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
