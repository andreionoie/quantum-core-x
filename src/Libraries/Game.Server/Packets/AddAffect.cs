using QuantumCore.Networking;

namespace QuantumCore.Game.Packets;

[Packet(0x7E, EDirection.Outgoing)]
[PacketGenerator]
public partial class AddAffect
{
    /// <summary>
    /// The Affect type: <see cref="QuantumCore.API.Game.Types.Skills.AffectType"/>.
    /// One of <see cref="QuantumCore.API.Game.Types.Skills.EAffectType"/>, <see cref="QuantumCore.API.Game.Types.Skills.ESkill"/>
    /// </summary>
    [Field(0)] public uint AffectType { get; set; }

    /// <summary>
    /// The point to apply the modification to: <see cref="QuantumCore.API.Game.Types.EPoint"/>.
    /// </summary>
    [Field(1)] public byte ModifiedPointId { get; set; }

    /// <summary>
    /// The delta to add to the point value.
    /// </summary>
    [Field(2)] public int ModifiedPointDelta { get; set; }

    /// <summary>
    /// The bit index of the affect flag: <see cref="QuantumCore.API.Game.Types.Skills.EAffect"/>.
    /// <br/>
    /// (OR the item id of auto recovery HP/SP: <see cref="QuantumCore.Game.Persistence.Entities.Item.Id"/>)
    /// </summary>
    [Field(3)] public uint AffectFlag { get; set; }
    
    /// <summary>
    /// Duration of the affect. Extracted from item_proto value1: <see cref="QuantumCore.API.Core.Models.ItemData.Values[1]"/>
    /// OR computed from skill_proto formula:<see cref="QuantumCore.API.Core.Models.SkillData.DurationFormula"/> .
    /// </summary>
    [Field(4)] public int TotalDurationSec { get; set; }

    /// <summary>
    /// For padding only; should be the per-second duration SP cost but does not actually affect the client.
    /// </summary>
    [Field(5)] private int _spCostUnused { get; init; }
}
