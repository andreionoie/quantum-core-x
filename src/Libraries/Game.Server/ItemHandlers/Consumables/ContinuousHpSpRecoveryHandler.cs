using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.Items;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Items;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Affects;
using QuantumCore.Caching;
using QuantumCore.Game.Persistence;
using QuantumCore.Game.World.Entities;

namespace QuantumCore.Game.ItemHandlers.Consumables;

[HandlesItemUse(Type = EItemType.Use, UseSubtype = EUseSubtype.UsePotionContinue)]
public class ContinuousHpSpRecoveryHandler(IItemRepository itemRepository, ICacheManager cacheManager)
    : IItemUseHandler
{
    private record ItemMetadata(ItemData ItemProto)
    {
        public int HpPerSecond => ItemProto.Values[0];
        public int SpPerSecond => ItemProto.Values[1];
        public int DurationSeconds => ItemProto.Values[2];
    }

    public async Task<bool> HandleAsync(ItemUseContext context)
    {
        if (context.Player is not PlayerEntity player)
            return false;

        var meta = new ItemMetadata(context.ItemProto);
        (int regenPerSecond, EAffectType affectType, EPoint modifiedPoint)[] recoveryContext =
        [
            (meta.HpPerSecond, EAffectType.HpRecoverContinue, EPoint.HpRecoverContinue),
            (meta.SpPerSecond, EAffectType.SpRecoverContinue, EPoint.SpRecoverContinue)
        ];

        foreach (var ctx in recoveryContext)
        {
            if (ctx.regenPerSecond > 0)
            {
                player.Affects.Upsert(new EntityAffect
                {
                    AffectType = AffectType.From(ctx.affectType),
                    ModifiedPointId = ctx.modifiedPoint,
                    ModifiedPointDelta = ctx.regenPerSecond,
                    RemainingDuration = TimeSpan.FromSeconds(meta.DurationSeconds)
                });
            }
        }

        await ItemConsumptionHelper.ConsumeItemAsync(player, context.Item, context.Window, context.Position,
            cacheManager, itemRepository);
        return true;
    }
    
    public bool ShouldRegisterFor(ItemData itemProto)
    {
        if (itemProto.Values.Count < 3) return false;

        var hpPerSecond = itemProto.Values[0];
        var spPerSecond = itemProto.Values[1];

        return hpPerSecond > 0 || spPerSecond > 0;
    }

}
