using QuantumCore.API.Game.Items;
using QuantumCore.Game.Extensions;

namespace QuantumCore.Game.ItemHandlers.Consumables;

public class SoulStoneHandler : IConfigurableItemIdHandler
{
    public IEnumerable<uint> RegisterForItemIds(GameOptions options)
    {
        yield return (uint)options.Skills.SoulStoneId;
    }

    public Task<bool> HandleAsync(ItemUseContext context)
    {
        var player = context.Player;
        var item = context.Item;

        // Simply consume the soul stone
        player.RemoveItem(item);
        player.SendRemoveItem(context.Window, context.Position);

        return Task.FromResult(true);
    }
}
