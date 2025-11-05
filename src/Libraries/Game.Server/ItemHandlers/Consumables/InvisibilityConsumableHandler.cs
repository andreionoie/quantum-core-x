using QuantumCore.API.Game.Items;
using QuantumCore.API.Game.Types.Items;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Affects;
using QuantumCore.Caching;
using QuantumCore.Game.Persistence;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.ItemHandlers.Consumables;

[HandlesItemUse(Type = EItemType.Use, UseSubtype = EUseSubtype.UseInvisibility)]
public class InvisibilityConsumableHandler(IItemRepository itemRepository, ICacheManager cacheManager)
    : IItemUseHandler
{
    private static readonly TimeSpan InvisibilityItemDuration = TimeSpan.FromMinutes(5);

    public async Task<bool> HandleAsync(ItemUseContext context)
    {
        if (context.Player is not PlayerEntity player)
        {
            return false;
        }

        player.Affects.Upsert(new EntityAffect
        {
            AffectType = AffectType.From(EAffectType.Invisibility),
            AffectFlag = EAffect.Invisibility,
            RemainingDuration = InvisibilityItemDuration
        });

        await ItemConsumptionHelper.ConsumeItemAsync(player, context.Item, context.Window, context.Position,
            cacheManager, itemRepository);
        return true;
    }
}
