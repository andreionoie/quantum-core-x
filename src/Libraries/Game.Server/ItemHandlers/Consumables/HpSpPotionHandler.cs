using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.Items;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Items;
using QuantumCore.Caching;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.Persistence;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.ItemHandlers.Consumables;

[HandlesItemUse(Type = EItemType.Use, UseSubtype = EUseSubtype.UsePotion)]
[HandlesItemUse(Type = EItemType.Use, UseSubtype = EUseSubtype.UsePotionNodelay)]
public class HpSpPotionHandler(IItemRepository itemRepository, ICacheManager cacheManager) : IItemUseHandler
{
    private record ItemMetadata(ItemData ItemProto)
    {
        public int HpAbsolute   => ItemProto.Values.ElementAtOrDefault(0);
        public int SpAbsolute   => ItemProto.Values.ElementAtOrDefault(1);
        public int HpPercentage => ItemProto.Values.ElementAtOrDefault(3);
        public int SpPercentage => ItemProto.Values.ElementAtOrDefault(4);

        public bool IsDelayedPotion => ItemProto.IsSubtype(EUseSubtype.UsePotion);
    }

    public async Task<bool> HandleAsync(ItemUseContext context)
    {
        if (context.Player is not PlayerEntity player)
            return false;

        var meta = new ItemMetadata(context.ItemProto);
        // NOTE: it is possible to have a potion that restores both HP and SP at the same time
        (int itemValue, int itemPercentage, EPoint point, EPoint maxPoint, EPoint bucket, ECharacterFx effect)[] potionsContext =
        [
            (meta.HpAbsolute, meta.HpPercentage, EPoint.Hp, EPoint.MaxHp, bucket: EPoint.HpRecovery, ECharacterFx.HpUpRed),
            (meta.SpAbsolute, meta.SpPercentage, EPoint.Sp, EPoint.MaxSp, bucket: EPoint.SpRecovery, ECharacterFx.SpUpBlue)
        ];

        var potionBonusPercentage = 100 + player.GetPoint(EPoint.PotionBonus);
        if (meta.IsDelayedPotion)
        {
            potionBonusPercentage = Math.Clamp(potionBonusPercentage, 0, 200);
        }
        
        var applied = false;
        foreach (var ctx in potionsContext)
        {
            if (ctx.itemValue > 0 || ctx.itemPercentage > 0)
            {
                var current = player.GetPoint(ctx.point);
                if (meta.IsDelayedPotion)
                {
                    current += player.GetPoint(ctx.bucket);
                }
                var max = player.GetPoint(ctx.maxPoint);

                // consume another potion only if not already full or pending to full
                if (current < max)
                {
                    var toRecoverFlat = ctx.itemValue * potionBonusPercentage / 100;
                    if (toRecoverFlat > 0)
                    {
                        var targetPoint = meta.IsDelayedPotion ? ctx.bucket : ctx.point;
                        player.AddPoint(targetPoint, (int)toRecoverFlat);
                        player.BroadcastCharacterFx(ctx.effect);
                        applied = true;
                    }

                }

                current = player.GetPoint(ctx.point);
                if (current < max)
                {
                    var toRecoverPercentage = ctx.itemPercentage * max / 100;
                    if (toRecoverPercentage > 0)
                    {
                        player.AddPoint(ctx.point, (int)toRecoverPercentage);
                        player.BroadcastCharacterFx(ctx.effect);
                        applied = true;
                    }
                }
            }
        }

        if (applied)
        {
            player.SendPoints();
            await ItemConsumptionHelper.ConsumeItemAsync(player, context.Item, context.Window, context.Position,
                cacheManager, itemRepository);
        }

        return true;
    }
    
    public bool ShouldRegisterFor(ItemData itemProto)
    {
        var hpAbsolute   = itemProto.Values.ElementAtOrDefault(0);
        var spAbsolute   = itemProto.Values.ElementAtOrDefault(1);
        var hpPercentage = itemProto.Values.ElementAtOrDefault(3);
        var spPercentage = itemProto.Values.ElementAtOrDefault(4);

        return hpAbsolute > 0 || spAbsolute > 0 || hpPercentage > 0 || spPercentage > 0;
    }

}
