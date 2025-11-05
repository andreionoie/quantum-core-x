using System.Diagnostics;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Stats;

namespace QuantumCore.API.Systems.Affects;

public sealed class EntityAffect : IStatEngine.IModifierSource
{
    public AffectType AffectType { get; init; } = AffectType.From(EAffectType.None);
    public EAffect AffectFlag { get; init; }
    public EPoint ModifiedPointId { get; init; }
    public int ModifiedPointDelta { get; init; }
    public int SpCostPerSecond
    {
        get => _spCostPerSecond;
        init
        {
            Debug.Assert(value >= 0);
            _spCostPerSecond = value;
        }
    }
    private readonly int _spCostPerSecond; // used to help with non-negative validation on init
    
    public bool DoNotPersist { get; init; }

    public TimeSpan RemainingDuration { get; set; }
    public double FractionalSpCostAccumulator { get; set; }
    
    // Optional source attacker VID used by DoT affects for damage attribution
    public uint SourceAttackerId { get; init; }

    public string ModifierKey => $"affect:{AffectType}";
    
    // Duration is sent as int in seconds; int.MaxValue seconds is ~68.1 years; leaving some buffer to 65 years.
    public static readonly TimeSpan PermanentAffectDurationThreshold = TimeSpan.FromDays(365 * 65);
    
    
    public static EntityAffect InvisibleRespawn5Sec => new()
    {
        AffectType = AffectType.From(EAffectType.InvisibleRespawn),
        AffectFlag = EAffect.InvisibleRespawn,
        RemainingDuration = TimeSpan.FromSeconds(5),
        DoNotPersist = true
    };
    
    public static EntityAffect GameMasterHaloEffect => new()
    {
        AffectFlag = EAffect.GameMaster,
        RemainingDuration = PermanentAffectDurationThreshold,
        DoNotPersist = true
    };
    
}
