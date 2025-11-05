namespace QuantumCore.API.Game.Items;

/// <summary>
/// Dispatches item usage to appropriate handlers based on item attributes.
/// Pre-computes itemID-to-handler mapping at startup for O(1) dispatch.
/// </summary>
public interface IItemUseDispatcher
{
    /// <summary>
    /// Gets a handler for the specified item ID, resolved from the provided service provider.
    /// Handlers are scoped and resolved per-request to properly access DbContext.
    /// </summary>
    /// <param name="itemId">The item ID to get a handler for</param>
    /// <param name="scopedProvider">The scoped service provider to resolve the handler from</param>
    /// <returns>The handler instance, or null if no handler is registered for this item</returns>
    IItemUseHandler? GetHandlerForItem(uint itemId, IServiceProvider scopedProvider);
}
