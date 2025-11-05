using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.Items;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Items;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Affects;
using QuantumCore.Caching;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Constants;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.Persistence;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.ItemHandlers.Consumables;

[HandlesItemUse(Type = EItemType.Use, UseSubtype = EUseSubtype.UseAbilityUp)]
public class AbilityConsumableHandler(IItemRepository itemRepository, ICacheManager cacheManager) : IItemUseHandler
{
    private record ItemMetadata(ItemData ItemProto)
    {
        public EApplyType ApplyType => EnumUtils<EApplyType>.CheckedCast(ItemProto.Values[0]);
        public int DurationSeconds => ItemProto.Values[1];
        public int Amount => ItemProto.Values[2];

        public bool ShouldPreventReapply => ItemConstants.IsJuicePotion(ItemProto.Id);
    }

    public async Task<bool> HandleAsync(ItemUseContext context)
    {
        if (context.Player is not PlayerEntity player)
            return false;

        var meta = new ItemMetadata(context.ItemProto);
        var affectType = AffectType.From(meta.ApplyType.ToAffectType());

        if (meta.ShouldPreventReapply)
        {
            if (player.Affects.Active.Any(x =>
                    x.AffectType == affectType && x.ModifiedPointId == meta.ApplyType.ToPoint()))
            {
                player.SendChatInfo("This effect is already activated.");
                return true;
            }
        }

        player.Affects.Upsert(new EntityAffect
        {
            AffectType = affectType,
            AffectFlag = meta.ApplyType.ToAffect(),
            ModifiedPointId = meta.ApplyType.ToPoint(),
            ModifiedPointDelta = meta.Amount,
            RemainingDuration = TimeSpan.FromSeconds(meta.DurationSeconds)
        });

        await ItemConsumptionHelper.ConsumeItemAsync(player, context.Item, context.Window, context.Position,
            cacheManager, itemRepository);
        return true;
    }

    public bool ShouldRegisterFor(ItemData itemProto)
    {
        if (itemProto.Values.Count < 3) return false;

        var applyType = EnumUtils<EApplyType>.CheckedCast(itemProto.Values[0]);
        var amount = itemProto.Values[2];

        return amount != 0 &&
               applyType.ToPoint() != EPoint.None &&
               applyType.ToAffectType() != EAffectType.None;
    }
}
