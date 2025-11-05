using Microsoft.Extensions.Logging;
using QuantumCore.API;
using QuantumCore.API.PluginTypes;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.Packets;
using QuantumCore.Game.World.Entities;
using static QuantumCore.Game.Packets.CharacterMove;

namespace QuantumCore.Game.PacketHandlers.Game;

public class CharacterMoveHandler(ILogger<CharacterMoveHandler> logger) : IGamePacketHandler<CharacterMove>
{
    private const int MaskSkillMotion = (int)CharacterMovementType.SkillBitFlag - 1;

    public Task ExecuteAsync(GamePacketContext<CharacterMove> ctx, CancellationToken token = default)
    {
        if (!EnumUtils<CharacterMovementType>.IsDefined(ctx.Packet.MovementType) &&
            (ctx.Packet.MovementType & (byte)CharacterMovementType.SkillBitFlag) == 0)
        {
            logger.LogError("Received unknown movement type ({MovementType})", ctx.Packet.MovementType);
            ctx.Connection.Close();
            return Task.CompletedTask;
        }

        if (ctx.Connection.Player is null)
        {
            logger.LogCritical("Cannot move player that does not exist. This is a programmatic failure");
            ctx.Connection.Close();
            return Task.CompletedTask;
        }

        logger.LogDebug("Received movement packet with type {MovementType}",
            (CharacterMovementType)ctx.Packet.MovementType);
        switch ((CharacterMovementType)ctx.Packet.MovementType)
        {
            case CharacterMovementType.Move:
                ctx.Connection.Player.Rotation = ctx.Packet.Rotation * 5;
                ctx.Connection.Player.Goto(ctx.Packet.PositionX, ctx.Packet.PositionY);
                ctx.Connection.Player.Mobility.StartMoving(GameServer.Instance.ServerTime);
                break;
            case CharacterMovementType.Attack or CharacterMovementType.Combo:
                // todo: cancel mining if actually mining
                (ctx.Connection.Player as PlayerEntity)?.MarkAttackedNow();
                ctx.Connection.Player.Mobility.StopMoving(GameServer.Instance.ServerTime);
                break;
            default:
                if ((ctx.Packet.MovementType & (byte)CharacterMovementType.SkillBitFlag) != 0)
                {
                    var motion = ctx.Packet.MovementType & MaskSkillMotion;

                    if (!ctx.Connection.Player.IsUsableSkillMotion(motion))
                    {
                        logger.LogError("Player is not allowed to use skill motion {SkillMotion}", motion);
                        ctx.Connection.Close();
                        return Task.CompletedTask;
                    }

                    // todo: cancel mining if actually mining
                    ctx.Connection.Player.Mobility.StopMoving(GameServer.Instance.ServerTime);
                }

                break;
        }

        if ((CharacterMovementType)ctx.Packet.MovementType == CharacterMovementType.Wait)
        {
            ctx.Connection.Player.Wait(ctx.Packet.PositionX, ctx.Packet.PositionY);
            ctx.Connection.Player.Mobility.StopMoving(GameServer.Instance.ServerTime);
        }

        var movement = new CharacterMoveOut
        {
            MovementType = ctx.Packet.MovementType,
            Argument = ctx.Packet.Argument,
            Rotation = ctx.Packet.Rotation,
            Vid = ctx.Connection.Player.Vid,
            PositionX = ctx.Packet.PositionX,
            PositionY = ctx.Packet.PositionY,
            Time = ctx.Packet.Time,
            Duration = (CharacterMovementType)ctx.Packet.MovementType == CharacterMovementType.Move
                ? ctx.Connection.Player.MovementDuration
                : 0
        };

        foreach (var player in ctx.Connection.Player.GetNearbyPlayers())
        {
            player.Connection.Send(movement);
        }

        return Task.CompletedTask;
    }
}
