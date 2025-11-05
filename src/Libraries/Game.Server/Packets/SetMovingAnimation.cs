using QuantumCore.Networking;

namespace QuantumCore.Game.Packets;

[Packet(0x6F, EDirection.Outgoing)]
[PacketGenerator]
public partial class SetMovingAnimation
{
    public enum MovingMode : byte
    {
        Running = 0,
        Walking = 1
    }
    
    [Field(0)] public uint Vid { get; set; }
    [Field(1)] public byte Mode { get; set; }
}

