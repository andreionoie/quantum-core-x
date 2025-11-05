using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.World;
using QuantumCore.Game.Packets;
using QuantumCore.Networking;

namespace QuantumCore.Game.Extensions;

public static class PaketExtensions
{
    public static Character ToCharacter(this PlayerData player)
    {
        return new Character
        {
            Id = player.Id,
            Name = player.Name,
            Class = player.PlayerClass,
            Level = player.Level,
            Playtime = (uint) TimeSpan.FromMilliseconds(player.PlayTime).TotalMinutes,
            St = player.St,
            Ht = player.Ht,
            Dx = player.Dx,
            Iq = player.Iq,
            BodyPart = (ushort) player.BodyPart,
            NameChange = 0,
            HairPort = (ushort) player.HairPart,
            PositionX = player.PositionX,
            PositionY = player.PositionY,
            SkillGroup = player.SkillGroup
        };
    }

    public static void BroadcastCharacterFx(this IEntity entity, ECharacterFx effect)
    {
        entity.BroadcastNearby(new CharacterFx { Vid = entity.Vid, Type = (byte)effect });
    }
    
    public static void BroadcastNearby<T>(this IEntity entity, T packet) where T : IPacketSerializable
    {
        if (entity is IPlayerEntity player)
        {
            player.Connection.Send(packet);
        }
        
        foreach (var nearbyPlayer in entity.NearbyEntities.Where(x => x is IPlayerEntity).Cast<IPlayerEntity>())
        {
            nearbyPlayer.Connection.Send(packet);
        }
    }
}
