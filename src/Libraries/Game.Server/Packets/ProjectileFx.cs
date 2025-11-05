using QuantumCore.Networking;

namespace QuantumCore.Game.Packets;

[Packet(0x46, EDirection.Outgoing)]
[PacketGenerator]
public partial class ProjectileFx
{
    /// <summary>
    /// The projectile type to display: <see cref="QuantumCore.API.Game.Types.EProjectileFx"/>.
    /// </summary>
    [Field(0)] public byte Type { get; set; }
    
    /// <summary>
    /// The entity id to display as the source of the projectile.
    /// </summary> 
    [Field(1)] public uint Source { get; set; }
    
    /// <summary>
    /// The entity id to display as the destination of the projectile.
    /// </summary> 
    [Field(2)] public uint Destination { get; set; }
}
