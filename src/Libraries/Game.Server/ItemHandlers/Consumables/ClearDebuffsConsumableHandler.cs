using QuantumCore.API.Game.Items;
using QuantumCore.API.Game.Types.Items;
using QuantumCore.Caching;
using QuantumCore.Game.Constants;
using QuantumCore.Game.Persistence;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.ItemHandlers.Consumables;

[HandlesItemUse(Type = EItemType.Use, UseSubtype = EUseSubtype.UseClear)]
public class ClearDebuffsConsumableHandler(IItemRepository itemRepository, ICacheManager cacheManager)
    : IItemUseHandler
{
    public async Task<bool> HandleAsync(ItemUseContext context)
    {
        if (context.Player is not PlayerEntity player)
        {
            return false;
        }

        var anyRemoved = false;
        foreach (var debuff in PlayerConstants.DebuffAffects)
        {
            anyRemoved |= player.Affects.RemoveAllOfType(debuff);
        }

        if (anyRemoved)
        {
            await ItemConsumptionHelper.ConsumeItemAsync(player, context.Item, context.Window, context.Position,
                cacheManager, itemRepository);
        }
        else
        {
            player.SendChatInfo("You don't have any active debuffs.");
        }
        
        return true;
    }
}
