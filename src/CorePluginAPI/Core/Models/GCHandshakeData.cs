using QuantumCore.API.Core.Time;

namespace QuantumCore.API.Core.Models;

public class GcHandshakeData
{
    public ulong Handshake { get; set; }
    public ServerTimestamp Time { get; set; }
    public TimeSpan Delta { get; set; }
}
