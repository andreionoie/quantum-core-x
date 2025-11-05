using QuantumCore.Networking;

namespace QuantumCore.Game.Packets;

[Packet(0x35, EDirection.Incoming, Sequence = true)]
[PacketGenerator]
public partial class RangedAimAdditional
{
    [Field(0)] public uint TargetVid { get; set; }
    [Field(1)] public int X { get; set; }
    [Field(2)] public int Y { get; set; }
}
