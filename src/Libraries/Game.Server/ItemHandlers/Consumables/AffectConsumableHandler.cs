using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.Items;
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

[HandlesItemUse(Type = EItemType.Use, UseSubtype = EUseSubtype.UseAffect)]
public class AffectConsumableHandler(IItemRepository itemRepository, ICacheManager cacheManager) : IItemUseHandler
{
    private record ItemMetadata(ItemData ItemProto)
    {
        public EAffectType AffectType => EnumUtils<EAffectType>.CheckedCast(ItemProto.Values[0]);
        public EApplyType ApplyType => EnumUtils<EApplyType>.CheckedCast(ItemProto.Values[1]);
        public int Value => ItemProto.Values[2];
        public int DurationSeconds => ItemProto.Values[3];
        
        public bool ShouldUseBonusAffect => ItemConstants.IsJuicePotion(ItemProto.Id);
    }

    public async Task<bool> HandleAsync(ItemUseContext context)
    {
        if (context.Player is not PlayerEntity player)
            return false;

        var meta = new ItemMetadata(context.ItemProto);
        var affectTypeValue = meta.AffectType;

        if (meta.ShouldUseBonusAffect)
        {
            // allows stacking of the affect bonus by using a different affect
            affectTypeValue = EAffectType.Bonus;
        }

        var affectType = AffectType.From(affectTypeValue);
        if (player.Affects.Active.Any(x => x.AffectType == affectType && x.ModifiedPointId == meta.ApplyType.ToPoint()))
        {
            player.SendChatInfo("The buff is still currently active.");
            return true;
        }

        player.Affects.Upsert(new EntityAffect
        {
            AffectType = affectType,
            ModifiedPointId = meta.ApplyType.ToPoint(),
            ModifiedPointDelta = meta.Value,
            RemainingDuration = TimeSpan.FromSeconds(meta.DurationSeconds)
        });

        await ItemConsumptionHelper.ConsumeItemAsync(player, context.Item, context.Window, context.Position,
            cacheManager, itemRepository);
        return true;
    }

    public bool ShouldRegisterFor(ItemData itemProto)
    {
        if (itemProto.Values.Count < 4) return false;

        if (!EnumUtils<EAffectType>.IsDefined(itemProto.Values[0])) return false;
        if (!EnumUtils<EApplyType>.IsDefined(itemProto.Values[1])) return false;

        var affectType = EnumUtils<EAffectType>.CheckedCast(itemProto.Values[0]);
        var duration = itemProto.Values[3];

        // ApplyType (Values[1]) can be None (0) for affects that don't modify stats
        return affectType != EAffectType.None && duration > 0;
    }
}
