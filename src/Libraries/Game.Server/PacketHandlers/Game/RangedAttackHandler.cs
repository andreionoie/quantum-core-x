using Microsoft.Extensions.Logging;
using QuantumCore.API;
using QuantumCore.API.PluginTypes;
using QuantumCore.Game.Packets;
using QuantumCore.API.Game.Types.Combat;

namespace QuantumCore.Game.PacketHandlers.Game;

public class RangedAttackHandler(ILogger<RangedAttackHandler> logger) : IGamePacketHandler<RangedAttack>
{
    public Task ExecuteAsync(GamePacketContext<RangedAttack> ctx, CancellationToken token = default)
    {
        // Intentionally no-op; ranged skill damage is handled when the skill packet is processed.
        // Still decode the type for telemetry and future logic.
        if (RangedAttackType.TryFromRaw(ctx.Packet.Type, out var kind))
        {
            logger.LogDebug("Received ranged attack packet Kind={Kind}", kind);
        }
        else
        {
            logger.LogDebug("Received ranged attack packet UnknownType={Type}", ctx.Packet.Type);
        }
        return Task.CompletedTask;
    }
}
