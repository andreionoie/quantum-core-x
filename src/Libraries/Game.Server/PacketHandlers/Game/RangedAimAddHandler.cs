using Microsoft.Extensions.Logging;
using QuantumCore.API;
using QuantumCore.API.PluginTypes;
using QuantumCore.Game.Packets;

namespace QuantumCore.Game.PacketHandlers.Game;

public class RangedAimAddHandler(ILogger<RangedAimAddHandler> logger) : IGamePacketHandler<RangedAimAdditional>
{
    public Task ExecuteAsync(GamePacketContext<RangedAimAdditional> ctx, CancellationToken token = default)
    {
        // TODO: implement
        logger.LogDebug("Received additional ranged aim TargetVid={Vid} X={X} Y={Y}",
            ctx.Packet.TargetVid, ctx.Packet.X, ctx.Packet.Y);
        return Task.CompletedTask;
    }
}
