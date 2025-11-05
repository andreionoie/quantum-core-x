using System.Text;
using CommandLine;
using QuantumCore.API.Game;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Affects;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Packets;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.Commands;

[Command("affect_remove", "Lists active affects / Removes 1 active affect from the player")] 
public class AffectRemoveCommand(IWorld world) : ICommandHandler<AffectRemoveCommandOptions>
{
    public Task ExecuteAsync(CommandContext<AffectRemoveCommandOptions> context)
    {
        var player = context.Player;

        if (context.Arguments.ModifiedPoint is null)
        {
            var target = player;
            if (context.Arguments.Arg1 is not null)
            {
                var other = world.GetPlayer(context.Arguments.Arg1);
                if (other is null)
                {
                    player.SendChatInfo($"Player '{context.Arguments.Arg1}' not found.");
                    return Task.CompletedTask;
                }

                target = other;
            }
            
            if (target is PlayerEntity targetPlayer)
            {
                ListAffectsChat(player, targetPlayer);
            }
            
            return Task.CompletedTask;
        }

        if (player is not PlayerEntity playerEntity)
        {
            player.SendChatInfo("Not a player entity.");
            return Task.CompletedTask;
        }

        try
        {
            var typeRaw = uint.Parse(context.Arguments.Arg1!);
            if (!AffectType.TryFromRaw(typeRaw, out var affectType))
            {
                throw new ArgumentOutOfRangeException(nameof(context), typeRaw, "Not a defined Affect Type ID.");
            }
            
            var pointRaw = (byte)context.Arguments.ModifiedPoint;
            var point = EnumUtils<EPoint>.CheckedCast(pointRaw);

            if (!playerEntity.Affects.Remove(affectType, point))
            {
                player.SendChatInfo("Not affected by that type and point.");
                return Task.CompletedTask;
            }

            player.Connection.Send(new RemoveAffect { AffectType = typeRaw, ModifiedPointId = pointRaw });
            playerEntity.SendCharacterUpdate();
            playerEntity.SendPoints();
            player.SendChatInfo($"Affect (type={affectType}, apply={Enum.GetName(point)}) successfully removed.");
        }
        catch (Exception e)
        {
            player.SendChatInfo(e.Message);
        }

        return Task.CompletedTask;
    }

    private static void ListAffectsChat(IPlayerEntity viewer, PlayerEntity target)
    {
        if (target.Affects.Active.Count == 0)
        {
            viewer.SendChatInfo("No active affects.");
            return;
        }

        viewer.SendChatInfo($"-- Affect List of {target.Name} -------------------------------");

        const int TypeColWidth = 28;
        const int ApplyColWidth = 28;
        const int ValueColWidth = 8;
        const int DurationColWidth = 20;

        var header = new StringBuilder()
            .Append("Type (Name)" .PadRight(TypeColWidth))
            .Append("Point (Name)".PadRight(ApplyColWidth))
            .Append("Delta"       .PadRight(ValueColWidth))
            .Append("Duration"    .PadRight(DurationColWidth))
            .Append("Flag (Name)")
            .ToString();
        
        viewer.SendChatInfo(header);
        foreach (var affect in target.Affects.Active)
        {
            var affectRow = new StringBuilder()
                .Append($"{affect.AffectType.ToRaw()} ({affect.AffectType})".PadRight(TypeColWidth))
                .Append($"{(byte)affect.ModifiedPointId} ({affect.ModifiedPointId})".PadRight(ApplyColWidth))
                .Append($"{affect.ModifiedPointDelta}".PadRight(ValueColWidth))
                .Append($"{affect.RemainingDuration:g}".PadRight(DurationColWidth))
                .Append($"{(uint)affect.AffectFlag} ({affect.AffectFlag})");

            // TODO: append cooldown info
            // if (affect.AffectType.IsSkillAffect)
            // {
            //     var skill = (ESkill)affect.AffectType.ToRaw();
            //     if (target.Skills?[skill] != null)
            //     {
            //         var lastUsedServerTime = target.Skills[skill]?.LastUsedServerTime;
            //         if (lastUsedServerTime.HasValue)
            //         {
            //             var now = TimeSpan.FromMilliseconds(GameServer.Instance.ServerTime);
            //             var lastUsed = TimeSpan.FromMilliseconds(lastUsedServerTime.Value);
            //             var cd = skill.GetCooldown();
            //             if (now < lastUsed + cd)
            //             {
            //                 affectRow.Append($" | CD={(now - lastUsed - cd).TotalSeconds:F2}s");
            //             }
            //         }
            //     }
            // }
            
            viewer.SendChatInfo(affectRow.ToString());
        }
        
    }
}

public class AffectRemoveCommandOptions
{
    [Value(0, Required = false, HelpText = "Player Name OR Affect Type ID")] public string? Arg1 { get; set; }

    [Value(1, Required = false, HelpText = "Point ID")] public byte? ModifiedPoint { get; set; }
}
