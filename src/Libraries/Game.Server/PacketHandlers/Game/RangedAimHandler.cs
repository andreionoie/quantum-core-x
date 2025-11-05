using Microsoft.Extensions.Logging;
using QuantumCore.API;
using QuantumCore.API.PluginTypes;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.Packets;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.PacketHandlers.Game;

public class RangedAimHandler(ILogger<RangedAimHandler> logger) : IGamePacketHandler<RangedAim>
{
    public Task ExecuteAsync(GamePacketContext<RangedAim> ctx, CancellationToken token = default)
    {
        if (ctx.Connection.Player is not PlayerEntity player)
        {
            logger.LogWarning("Received RangedAim packet for connection without player");
            return Task.CompletedTask;
        }
        
        // TODO: implement ranged targeting logic

        var broadcast = new ProjectilePacket
        {
            Shooter = player.Vid,
            Target = ctx.Packet.TargetVid,
            TargetX = ctx.Packet.X,
            TargetY = ctx.Packet.Y
        };

        player.Connection.Send(broadcast);
        foreach (var nearbyPlayer in player.GetNearbyPlayers())
        {
            nearbyPlayer.Connection.Send(broadcast);
        }

        return Task.CompletedTask;
    }
}
