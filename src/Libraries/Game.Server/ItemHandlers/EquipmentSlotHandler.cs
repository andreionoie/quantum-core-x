using QuantumCore.API;
using QuantumCore.API.Game.Items;
using QuantumCore.API.Game.Types.Items;

namespace QuantumCore.Game.ItemHandlers;

[HandlesItemUse(Type = EItemType.Weapon)]
[HandlesItemUse(Type = EItemType.FishingRod)]
[HandlesItemUse(Type = EItemType.Pickaxe)]
[HandlesItemUse(Type = EItemType.Armor)]
[HandlesItemUse(Type = EItemType.Costume)]
public class EquipmentSlotHandler(IItemManager itemManager) : IItemUseHandler
{
    public async Task<bool> HandleAsync(ItemUseContext context)
    {
        // Unequip from equipment slot
        if (context.Window == (byte)WindowType.Inventory && context.Position >= context.Player.Inventory.Size)
        {
            context.Player.RemoveItem(context.Item);
            if (await context.Player.Inventory.PlaceItem(context.Item))
            {
                context.Player.SendRemoveItem(context.Window, context.Position);
                context.Player.SendItem(context.Item);
                context.Player.SendCharacterUpdate();
            }
            else
            {
                context.Player.SetItem(context.Item, context.Window, context.Position);
                context.Player.SendChatInfo("Cannot unequip item if the inventory is full");
            }
            return true;
        }

        // Equip or swap equipment
        if (context.Player.IsEquippable(context.Item))
        {
            var wearSlot = context.Player.Inventory.EquipmentWindow.GetWearPosition(itemManager, context.Item.ItemId);

            if (wearSlot <= ushort.MaxValue)
            {
                context.Player.RemoveItem(context.Item);
                
                var item2 = context.Player.Inventory.EquipmentWindow.GetItem((ushort)wearSlot);
                if (item2 == null)
                {
                    // Equip item
                    context.Player.SetItem(context.Item, (byte)WindowType.Inventory, (ushort)wearSlot);
                    context.Player.SendRemoveItem(context.Window, context.Position);
                    context.Player.SendItem(context.Item);
                }
                else
                {
                    // Swap items
                    context.Player.RemoveItem(item2);
                    if (await context.Player.Inventory.PlaceItem(item2))
                    {
                        context.Player.SendRemoveItem(context.Window, (ushort)wearSlot);
                        context.Player.SendRemoveItem(context.Window, context.Position);
                        context.Player.SetItem(context.Item, context.Window, (ushort)wearSlot);
                        context.Player.SetItem(item2, context.Window, context.Position);
                        context.Player.SendItem(context.Item);
                        context.Player.SendItem(item2);
                    }
                    else
                    {
                        context.Player.SetItem(context.Item, context.Window, context.Position);
                        context.Player.SetItem(item2, context.Window, (ushort)wearSlot);
                        context.Player.SendChatInfo("Cannot swap item if the inventory is full");
                    }
                }
            }
            return true;
        }

        return false;
    }
}
