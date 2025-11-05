using QuantumCore.Networking;

namespace QuantumCore.Game.Packets;

[Packet(0x72, EDirection.Outgoing)]
[PacketGenerator]
public partial class CharacterFx
{
    /// <summary>
    /// The special effect type to display: <see cref="QuantumCore.API.Game.Types.ECharacterFx"/>.
    /// </summary>
    [Field(0)] public byte Type { get; set; }
    
    /// <summary>
    /// The entity id to apply the effect to.
    /// </summary> 
    [Field(1)] public uint Vid { get; set; }
}
