using System.Diagnostics;
using QuantumCore.API.Core.Models;
using QuantumCore.Caching;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.Persistence;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.ItemHandlers;

public static class ItemConsumptionHelper
{
    public static async Task ConsumeItemAsync(PlayerEntity player, ItemInstance item, byte window, ushort position,
        ICacheManager cacheManager, IItemRepository itemRepository)
    {
        Debug.Assert(item.Count > 0);

        if (item.Count == 1)
        {
            player.RemoveItem(item);
            player.SendRemoveItem(window, position);
            await item.Destroy(cacheManager);
            await itemRepository.DeletePlayerItemAsync(player.Player.Id, item.ItemId);
        }
        else
        {
            item.Count--;
            await item.Persist(itemRepository);
            player.SendItem(item);
        }
    }
}
