using QuantumCore.Networking;

namespace QuantumCore.Game.Packets;

[Packet(0x08, EDirection.Incoming, Sequence = true)]
[PacketGenerator]
public partial class SyncPositions
{
    [Field(0, PacketSize = true)] public ushort TotalSize { get; set; }

    [Field(1, VarLen = true)] public SyncPositionElement[] Positions { get; set; } = [];
}
