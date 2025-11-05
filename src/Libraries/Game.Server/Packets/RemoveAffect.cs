using QuantumCore.Networking;

namespace QuantumCore.Game.Packets;

[Packet(0x7F, EDirection.Outgoing)]
[PacketGenerator]
public partial class RemoveAffect
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
}
