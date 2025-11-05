using Microsoft.Extensions.Logging;
using QuantumCore.API;
using QuantumCore.API.Game.Items;
using QuantumCore.API.PluginTypes;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.Packets;

namespace QuantumCore.Game.PacketHandlers.Game;

public class ItemUseHandler(
    IItemManager itemManager,
    ILogger<ItemUseHandler> logger,
    IItemUseDispatcher dispatcher,
    IServiceProvider serviceProvider)
    : IGamePacketHandler<ItemUse>
{
    public async Task ExecuteAsync(GamePacketContext<ItemUse> ctx, CancellationToken token = default)
    {
        var player = ctx.Connection.Player;
        if (player == null)
        {
            ctx.Connection.Close();
            return;
        }


        var item = player.GetItem(ctx.Packet.Window, ctx.Packet.Position);
        if (item == null)
        {
            logger.LogDebug("Used item not found!");
            return;
        }

        var itemProto = itemManager.GetItem(item.ItemId);
        if (itemProto == null)
        {
            logger.LogDebug("Cannot find item proto {ItemId}", item.ItemId);
            return;
        }

        var handler = dispatcher.GetHandlerForItem(itemProto.Id, serviceProvider);
        if (handler == null)
        {
            player.SendChatInfo($"The item <{itemProto.TranslatedName}> ({itemProto.Id}) is not implemented.");

            logger.LogWarning("No handler registered for item <{Item}> ({ItemId}) with type {ItemType}, subtype {Subtype}, values {Values}.",
                itemProto.TranslatedName, itemProto.Id, itemProto.GetItemType(), itemProto.GetSubtypeCast(), string.Join(", ", itemProto.Values));
            return;
        }

        logger.LogDebug("Use item {Window},{Position} with handler {Handler}", ctx.Packet.Window, ctx.Packet.Position, handler.GetType().Name);

        var context = new ItemUseContext(ctx.Connection, item, itemProto, ctx.Packet.Window, ctx.Packet.Position);
        var wasHandled = await handler.HandleAsync(context);
        if (!wasHandled)
        {
            logger.LogWarning("Handler {Handler} failed for item type {ItemType}, subtype {Subtype}, itemID {ItemId}",
                handler.GetType().Name, itemProto.Type, itemProto.Subtype, itemProto.Id);
        }
    }
}
