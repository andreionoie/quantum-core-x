using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumCore.API;
using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.Items;
using QuantumCore.API.Game.Types.Items;
using QuantumCore.Game.Extensions;

namespace QuantumCore.Game.ItemHandlers;

/// <summary>
/// Dispatches item usage to appropriate handlers based on [HandlesItemUse] attributes.
/// Uses last-wins registration (plugins override core).
/// Singleton service that stores handler Types and resolves instances per-request.
/// Pre-computes itemID-to-handler mapping at startup for O(1) dispatch.
/// </summary>
public sealed class ItemUseDispatcher(
    IItemManager itemManager,
    ILogger<ItemUseDispatcher> logger,
    IOptions<GameOptions> gameOptions,
    IServiceProvider serviceProvider)
    : IItemUseDispatcher, ILoadable
{
    /// <summary>
    /// Gets the pre-computed read-only mapping of itemIDs to handler types (resolved per-request).
    /// Handler types are pre-validated at startup.
    /// </summary>
    public IReadOnlyDictionary<uint, IItemUseHandler> Handlers => throw new NotSupportedException("Use GetHandlerForItem instead");

    private readonly Dictionary<uint, Type> _precomputedHandlerTypes = new();

    private readonly GameOptions _gameOptions = gameOptions.Value;

    public async Task LoadAsync(CancellationToken token = default)
    {
        // Ensure item manager has loaded first (idempotent call)
        await itemManager.LoadAsync(token);

        var context = new DispatcherRegistrationContext(logger);
        // TODO: Order by assembly (core first, plugins last) to ensure plugins override core handlers
        // See PacketManager for similar issue - both use alphabetical ordering which doesn't guarantee plugin precedence
        var handlers = AppDomain.CurrentDomain.GetAssemblies()
            .Where(x => x.GetName().Name?.StartsWith("DynamicProxyGenAssembly") == false) // ignore Castle.Core proxies
            .Where(x => !x.IsDynamic)
            .SelectMany(x => x.ExportedTypes)
            .Where(x => x is { IsAbstract: false, IsInterface: false } &&
                       typeof(IItemUseHandler).IsAssignableFrom(x))
            .OrderBy(x => x.FullName)
            .ToArray();

        foreach (var handlerType in handlers)
        {
            // Configurable handlers (for itemIDs not known at compile time but known at startup)
            if (typeof(IConfigurableItemIdHandler).IsAssignableFrom(handlerType))
            {
                RegisterConfigurableHandler(handlerType, _gameOptions, context);
            }
            else
            {
                // Attribute-based handlers specify itemIDs or types at compile time
                RegisterAttributeHandler(handlerType, context);
            }
        }

        // Pre-compute itemID -> handler-types with ShouldRegisterFor() validation at startup
        // Handlers are scoped and resolved per-request to access DbContext properly
        var allItems = itemManager.GetAllItems().ToArray();
        var notFoundCount = 0;

        // Create a temporary scope for validation
        using (var scope = serviceProvider.CreateScope())
        {
            foreach (var item in allItems)
            {
                var handlerType = context.FindHandlerType(item);
                if (handlerType != null)
                {
                    // Resolve handler instance from DI scope for validation
                    var handler = (IItemUseHandler)scope.ServiceProvider.GetRequiredService(handlerType);

                    // Validate ShouldRegisterFor() once at startup
                    if (PassesRegistrationCheck(handler, item))
                    {
                        _precomputedHandlerTypes[item.Id] = handlerType;
                    }
                    else
                    {
                        logger.LogDebug("Handler '{Handler}' rejected from {ValidationMethod}() the item <{Item}> ({ItemId}) with type {ItemType}, subtype {Subtype}, values {Values}.",
                             handlerType.Name, nameof(handler.ShouldRegisterFor), item.TranslatedName, item.Id, item.GetItemType(), item.GetSubtypeCast(), string.Join(", ", item.Values));
                    }
                }
                else
                {
                    notFoundCount++;
                }
            }
        }

        logger.LogWarning("{Count} items had no matching handler registration", notFoundCount);

        logger.LogInformation(
            "ItemUseDispatcher initialized: {ItemIds} exact itemIDs, {Ranges} itemID ranges, {Subtypes} type+subtype, {Types} type-only. " +
            "Precomputed handler dispatch for {HandledItems}/{TotalItems} items.",
            context.ItemIdHandlers.Count, context.RangeRules.Count, context.SubtypeHandlers.Count, context.TypeHandlers.Count,
            _precomputedHandlerTypes.Count, allItems.Length);
    }

    public IItemUseHandler? GetHandlerForItem(uint itemId, IServiceProvider scopedProvider)
    {
        if (_precomputedHandlerTypes.TryGetValue(itemId, out var handlerType))
        {
            return (IItemUseHandler)scopedProvider.GetRequiredService(handlerType);
        }
        return null;
    }

    // Validates ShouldRegisterFor() during startup pre-computation to avoid runtime validation overhead.
    // Uses the already-resolved singleton handler instance from DI.
    private bool PassesRegistrationCheck(IItemUseHandler handler, ItemData itemProto)
    {
        try
        {
            return handler.ShouldRegisterFor(itemProto);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Handler '{Handler}' threw during {ValidationMethod}() pre-computation for item {ItemId}. " +
                "Handler will not be registered for this item.",
                handler.GetType().Name, nameof(handler.ShouldRegisterFor), itemProto.Id);
            return false;
        }
    }
    
    private static void RegisterAttributeHandler(Type handlerType, DispatcherRegistrationContext context)
    {
        var attributes = handlerType.GetCustomAttributes<HandlesItemUseAttribute>().ToArray();

        if (attributes.Length == 0)
        {
            context.Logger.LogWarning("Handler '{Handler}' has no [HandlesItemUse] attribute, it will be skipped", handlerType);
            return;
        }

        foreach (var attr in attributes)
        {
            ValidateAttribute(attr, handlerType);
            RegisterAttribute(attr, handlerType, context);
        }
    }

    private static void RegisterConfigurableHandler(Type handlerType, GameOptions gameOptions, DispatcherRegistrationContext context)
    {
        var handler = (IConfigurableItemIdHandler)Activator.CreateInstance(handlerType)!;

        foreach (var itemId in handler.RegisterForItemIds(gameOptions))
        {
            context.RegisterItemId(itemId, handlerType);
        }
    }

    private static void ValidateAttribute(HandlesItemUseAttribute attr, Type handlerType)
    {
        if (attr is { HasItemId: false, HasItemIdRange: false, HasType: false })
        {
            throw new InvalidOperationException(
                $"Handler '{handlerType}' has [HandlesItemUse] with no filters. Specify at least one of: ItemId, ItemIdMin/ItemIdMax, or Type.");
        }

        if (attr is { HasItemId: true, HasItemIdRange: true })
        {
            throw new InvalidOperationException(
                $"Handler '{handlerType}' has both ItemId and ItemIdMin/ItemIdMax set. Use one or the other.");
        }

        if (attr is { HasType: false, HasSpecificSubtype: true })
        {
            throw new InvalidOperationException(
                $"Handler '{handlerType}' has a specific subtype without Type. When using subtypes, the parent Type must be specified.");
        }

        if (attr.HasItemIdRange && attr.ItemIdMin > attr.ItemIdMax)
        {
            throw new InvalidOperationException(
                $"Handler '{handlerType}' has invalid ItemIdRange: Min ({attr.ItemIdMin}) > Max ({attr.ItemIdMax}).");
        }
    }

    private static void RegisterAttribute(HandlesItemUseAttribute attr, Type handlerType, DispatcherRegistrationContext context)
    {
        if (attr.HasItemId)
        {
            context.RegisterItemId(attr.ItemId, handlerType);
        }
        else if (attr.HasItemIdRange)
        {
            context.RegisterItemIdRange(attr, handlerType);
        }
        else if (attr is { HasType: true, HasSpecificSubtype: true })
        {
            if (!attr.TryGetSubtypeUnion(out var subtype))
            {
                throw new InvalidOperationException($"Handler '{handlerType}' Type={attr.Type} did not specify a valid subtype.");
            }

            context.RegisterSubtype(subtype, handlerType);
        }
        else if (attr.HasType)
        {
            context.RegisterType(attr.Type, handlerType);
        }
    }

}

/// <summary>ItemID range rule for large ranges (not expanded into exact itemID table)</summary>
internal readonly struct ItemIdRangeRule(uint min, uint max, HandlesItemUseAttribute attr, Type handlerType)
{
    public readonly uint ItemIdMin = min;
    public readonly uint ItemIdMax = max;
    public readonly bool HasType = attr.HasType;
    public readonly EItemType Type = attr.Type;
    public readonly bool HasSubtypeUnion = attr.HasSpecificSubtype && attr.TryGetSubtypeUnion(out _);
    public readonly ItemSubtype SubtypeUnion = attr.TryGetSubtypeUnion(out var st) ? st : default;
    public readonly Type HandlerType = handlerType;

    public bool Matches(uint itemId, EItemType itemType, byte rawSubtype)
    {
        if (itemId < ItemIdMin || itemId > ItemIdMax)
        {
            return false;
        }

        if (HasType && itemType != Type)
        {
            return false;
        }

        if (!HasSubtypeUnion)
        {
            return true;
        }

        if (!ItemSubtype.TryFrom(itemType, rawSubtype, out var st))
        {
            return false;
        }

        return st.Equals(SubtypeUnion);
    }
}

/// <summary>Encapsulates handler registration state and logic during initialization</summary>
internal sealed class DispatcherRegistrationContext(ILogger logger)
{
    private const int RangeExpansionThreshold = 100;

    public ILogger Logger { get; } = logger;

    public Dictionary<uint, Type> ItemIdHandlers { get; } = new();
    public List<ItemIdRangeRule> RangeRules { get; } = [];
    public Dictionary<ItemSubtype, Type> SubtypeHandlers { get; } = new();
    public Dictionary<EItemType, Type> TypeHandlers { get; } = new();

    /// <summary>
    /// Finds handler Type for an item using natural specificity ordering.
    /// Used during pre-computation to build itemID-to-handler map.
    /// </summary>
    public Type? FindHandlerType(ItemData itemProto)
    {
        var itemId = itemProto.Id;
        var itemType = (EItemType)itemProto.Type;
        var rawSubtype = itemProto.Subtype;

        // 1. Exact itemID match
        if (ItemIdHandlers.TryGetValue(itemId, out var handlerType))
        {
            return handlerType;
        }

        // 2. itemID range match
        foreach (var rule in RangeRules)
        {
            if (rule.Matches(itemId, itemType, rawSubtype))
            {
                return rule.HandlerType;
            }
        }

        // 3. Type+Subtype match
        if (ItemSubtype.TryFrom(itemType, rawSubtype, out var st) &&
            SubtypeHandlers.TryGetValue(st, out handlerType))
        {
            return handlerType;
        }

        // 4. Type-only match
        if (TypeHandlers.TryGetValue(itemType, out handlerType))
        {
            return handlerType;
        }

        return null;
    }

    public void RegisterItemId(uint itemId, Type handlerType)
    {
        if (ItemIdHandlers.TryGetValue(itemId, out var existing))
        {
            Logger.LogWarning("ItemID {ItemId}: '{Handler}' replaces '{Existing}' (last registration wins)",
                itemId, handlerType.Name, existing.Name);
        }

        ItemIdHandlers[itemId] = handlerType;

        Logger.LogDebug("Registered item handler for itemID {ItemId} -> {Handler}", itemId, handlerType);
    }

    public void RegisterItemIdRange(HandlesItemUseAttribute attr, Type handlerType)
    {
        var (min, max) = (attr.ItemIdMin, attr.ItemIdMax);
        var size = max - min + 1;

        if (size > 100_000)
        {
            Logger.LogWarning("Handler '{Handler}' has very large range ({Min}-{Max} = {Size:N0} items)",
                handlerType, min, max, size);
        }

        // Check for overlaps with existing large ranges
        foreach (var existing in RangeRules)
        {
            if (RangesOverlap(attr, existing))
            {
                Logger.LogWarning("Overlapping ranges: '{Handler}' {Range1} overlaps '{Existing}' {Range2}",
                    handlerType.Name, FormatRange(attr), existing.HandlerType.Name, FormatRange(existing));
            }
        }

        // optimization: expand small range into exact itemID mapping
        if (size <= RangeExpansionThreshold)
        {
            for (var itemId = min; itemId <= max; itemId++)
            {
                if (ItemIdHandlers.TryGetValue(itemId, out var existing))
                {
                    Logger.LogWarning("ItemID {ItemId} (in range {Min}-{Max}): '{Handler}' replaces '{Existing}'",
                        itemId, min, max, handlerType.Name, existing.Name);
                }

                ItemIdHandlers[itemId] = handlerType;
            }
            Logger.LogDebug("Expanded range {Min}-{Max} ({Size} items) -> {Handler}", min, max, size, handlerType);
        }
        else
        {
            // Keep large ranges for linear search fallback
            RangeRules.Add(new ItemIdRangeRule(min, max, attr, handlerType));
            Logger.LogDebug("Registered item handler for itemID range {Min}-{Max} ({Size:N0} items) -> {Handler}", min, max, size, handlerType);
        }
    }

    public void RegisterSubtype(ItemSubtype subtype, Type handlerType)
    {
        if (SubtypeHandlers.TryGetValue(subtype, out var existing))
        {
            Logger.LogWarning("Type+Subtype ({Subtype}): '{Handler}' replaces '{Existing}'",
                subtype, handlerType.Name, existing.Name);
        }

        SubtypeHandlers[subtype] = handlerType;
        Logger.LogDebug("Registered item handler for Subtype={Subtype} -> {Handler}", subtype, handlerType);
    }

    public void RegisterType(EItemType type, Type handlerType)
    {
        if (TypeHandlers.TryGetValue(type, out var existing))
        {
            Logger.LogWarning("Type {Type}: '{Handler}' replaces '{Existing}'",
                type, handlerType.Name, existing.Name);
        }

        TypeHandlers[type] = handlerType;
        Logger.LogDebug("Registered item handler for Type={Type} -> {Handler}", type, handlerType);
    }

    private static bool RangesOverlap(HandlesItemUseAttribute attr, ItemIdRangeRule rule)
    {
        // Check itemID overlap
        if (attr.ItemIdMax < rule.ItemIdMin || rule.ItemIdMax < attr.ItemIdMin)
            return false;

        // ItemIDs overlap - check if constraints are compatible (both having same Type/Subtype)
        if (attr.HasType && rule.HasType && attr.Type != rule.Type)
            return false;

        if (attr.HasSpecificSubtype && rule.HasSubtypeUnion)
        {
            attr.TryGetSubtypeUnion(out var attrSubtype);
            if (!attrSubtype.Equals(rule.SubtypeUnion))
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatRange(HandlesItemUseAttribute attr)
    {
        var result = $"[{attr.ItemIdMin}-{attr.ItemIdMax}]";
        if (attr.HasType)
        {
            result += $" Type={attr.Type}";
        }

        if (attr.HasSpecificSubtype)
        {
            if (attr.TryGetSubtypeUnion(out var subtype))
            {
                result += $" Subtype={subtype}";
            }
        }
        
        return result;
    }

    private static string FormatRange(ItemIdRangeRule rule)
    {
        var result = $"[{rule.ItemIdMin}-{rule.ItemIdMax}]";
        if (rule.HasType)
        {
            result += $" Type={rule.Type}";
        }

        if (rule.HasSubtypeUnion)
        {
            result += $" Subtype={rule.SubtypeUnion}";
        }

        return result;
    }
}

/// <summary>
/// Marker interface for handlers that need itemIDs from runtime configuration.
/// Implement this to register handlers at startup with dynamically-resolved itemIDs.
/// </summary>
public interface IConfigurableItemIdHandler : IItemUseHandler
{
    /// <summary>
    /// Called only once at startup by ItemUseDispatcher.
    /// Receives GameOptions to resolve itemIDs from configuration.
    /// </summary>
    IEnumerable<uint> RegisterForItemIds(GameOptions options);
}
