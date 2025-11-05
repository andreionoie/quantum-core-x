using QuantumCore.Networking;

namespace QuantumCore.Game.Packets;

[Packet(0x36, EDirection.Incoming, Sequence = true)]
[PacketGenerator]
public partial class RangedAttack
{
    /// <summary>
    /// The type of ranged attack: <see cref="QuantumCore.API.Game.Types.Combat.RangedAttackType"/>
    /// </summary>
    [Field(0)] public byte Type { get; set; }
}

