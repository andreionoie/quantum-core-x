using QuantumCore.API;
using QuantumCore.API.Game.World;
using QuantumCore.API.PluginTypes;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.Packets;
using QuantumCore.Game.World;

namespace QuantumCore.Game.PacketHandlers.Game;

public class SyncPositionsHandler : IGamePacketHandler<SyncPositions>
{
    public Task ExecuteAsync(GamePacketContext<SyncPositions> ctx, CancellationToken token = default)
    {
        if (ctx.Connection.Player is not IPlayerEntity player || player.Map is not Map map)
        {
            return Task.CompletedTask;
        }

        // Filter only known entities and forward positions
        var positions = new List<SyncPositionElement>(ctx.Packet.Positions.Length);
        foreach (var position in ctx.Packet.Positions)
        {
            var entity = map.GetEntity(position.Vid);
            if (entity is null || entity.Type is EEntityType.Npc or EEntityType.Warp or EEntityType.Goto)
            {
                continue;
            }

            positions.Add(position);
        }

        if (positions.Count == 0)
        {
            return Task.CompletedTask;
        }

        var syncPacket = new SyncPositionsOut { Positions = positions.ToArray() };
        foreach (var nearby in player.GetNearbyPlayers())
        {
            nearby.Connection.Send(syncPacket);
        }

        return Task.CompletedTask;
    }
}
