using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;

namespace QuantumCore.Game.Persistence.Entities;

public class PlayerAffect
{
    public required Guid Id { get; set; } = Guid.NewGuid();
    public required DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public required DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public required uint PlayerId { get; init; }

    // AffectType (either EAffectType or ESkill)
    public required int AffectTypeValue { get; set; }
    [DefaultValue(false)] public required bool IsSkill { get; set; }

    // Properties from EntityAffect
    [DefaultValue(EAffect.None)] public required byte AffectFlag { get; set; }
    [DefaultValue(EPoint.None)] public required byte ModifiedPointId { get; set; }
    [DefaultValue(0)] public required int ModifiedPointDelta { get; set; }
    [DefaultValue(0)] public required int SpCostPerSecond { get; set; }
    [DefaultValue(false)] public required bool DoNotPersist { get; set; }
    [DefaultValue(false)] public required bool DoNotClearOnDeath { get; set; }

    // Duration stored as total milliseconds
    public required long RemainingDurationMs { get; set; }
    [DefaultValue(0.0)] public required double FractionalSpCostAccumulator { get; set; }

    [DefaultValue(0)] public required uint SourceAttackerId { get; set; }

    public static void Configure(EntityTypeBuilder<PlayerAffect> builder, DatabaseFacade database)
    {
        builder.HasKey(x => x.Id);

        // Index on PlayerId for fast lookups
        builder.HasIndex(x => x.PlayerId);

        if (database.IsSqlite() || database.IsNpgsql())
        {
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("current_timestamp");
            builder.Property(x => x.UpdatedAt).HasDefaultValueSql("current_timestamp");
        }
        else if (database.IsMySql())
        {
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("(CAST(CURRENT_TIMESTAMP AS DATETIME(6)))");
            builder.Property(x => x.UpdatedAt).HasDefaultValueSql("(CAST(CURRENT_TIMESTAMP AS DATETIME(6)))");
        }
    }
}
