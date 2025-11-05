using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using BinarySerialization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using QuantumCore.API;
using QuantumCore.API.Core.Models;
using QuantumCore.Core.Types;

namespace QuantumCore.Game
{
    /// <summary>
    /// Manage all static data related to items
    /// </summary>
    public class ItemManager : IItemManager, ILoadable
    {
        private readonly ILogger<ItemManager> _logger;
        private readonly IFileProvider _fileProvider;
        private ImmutableDictionary<uint, ItemData> _itemsById = ImmutableDictionary<uint, ItemData>.Empty;
        
        private readonly Lazy<Task> _loader;

        static ItemManager()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // register korean locale
        }

        public ItemManager(ILogger<ItemManager> logger, IFileProvider fileProvider)
        {
            _logger = logger;
            _fileProvider = fileProvider;
            _loader = new Lazy<Task>(LoadItemProtoAsync, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Query for a specific item definition by it's id (O(1) lookup)
        /// </summary>
        /// <param name="id">Item ID</param>
        /// <returns>The item definition or null if the item is not known</returns>
        public ItemData? GetItem(uint id)
        {
            _itemsById.TryGetValue(id, out var item);
            return item;
        }

        public bool TryGetItem(uint id, [NotNullWhen(true)] out ItemData? data)
        {
            return _itemsById.TryGetValue(id, out data);
        }

        public ItemData? GetItemByName(ReadOnlySpan<char> name)
        {
            foreach (var item in _itemsById.Values)
            {
                if (name.Equals(item.Name, StringComparison.InvariantCulture))
                {
                    return item;
                }
            }

            return null;
        }

        public IEnumerable<ItemData> GetAllItems()
        {
            return _itemsById.Values;
        }

        /// <summary>
        /// Try to load the item_proto file - idempotent and thread-safe due to Lazy<> usage
        /// </summary>
        public async Task LoadAsync(CancellationToken token = default)
        {
            await _loader.Value.WaitAsync(token); 
        }
        
        private async Task LoadItemProtoAsync()
        {
            var file = _fileProvider.GetFileInfo("item_proto");
            if (!file.Exists)
            {
                _logger.LogWarning("{Path} does not exist, items not loaded", file.PhysicalPath);
                _itemsById = ImmutableDictionary<uint, ItemData>.Empty;
                return;
            }

            _logger.LogInformation("Loading item_proto");

            await using var fs = file.CreateReadStream();
            var bs = new BinarySerializer {Options = SerializationOptions.ThrowOnEndOfStream};
            var result = await bs.DeserializeAsync<ItemDataContainer>(fs);
            var items = new LzoXtea(result.Payload.RealSize, result.Payload.EncryptedSize, 0x2A4A1, 0x45415AA,
                0x185A8BE7,
                0x1AAD6AB);
            var itemsRaw = items.Decode(result.Payload.EncryptedPayload);
            var data = bs.Deserialize<ItemData[]>(itemsRaw);

            _itemsById = data.Select(proto => new ItemData
            {
                Applies = proto.Applies.Select(x => new ItemApplyData {Type = x.Type, Value = x.Value}).ToList(),
                Flags = proto.Flags,
                Id = proto.Id,
                Limits = proto.Limits.Select(x => new ItemLimitData {Type = x.Type, Value = x.Value}).ToList(),
                Name = proto.Name,
                Size = proto.Size,
                Sockets = proto.Sockets,
                Specular = proto.Specular,
                Subtype = proto.Subtype,
                Type = proto.Type,
                Unknown = proto.Unknown,
                Unknown2 = proto.Unknown2,
                Values = proto.Values,
                AntiFlags = proto.AntiFlags,
                BuyPrice = proto.BuyPrice,
                ImmuneFlags = proto.ImmuneFlags,
                SellPrice = proto.SellPrice,
                SocketPercentage = proto.SocketPercentage,
                TranslatedName = proto.TranslatedName,
                UpgradeId = proto.UpgradeId,
                UpgradeSet = proto.UpgradeSet,
                WearFlags = proto.WearFlags,
                MagicItemPercentage = proto.MagicItemPercentage
            }).ToImmutableDictionary(x => x.Id);
        }

        /// <summary>
        /// Create an instance for the given item definition.
        /// The owner, window, position will left empty.
        /// Also the object won't get stored without calling Item.Persist()!
        /// </summary>
        /// <param name="proto">Item definition to create</param>
        /// <param name="count">Number of items on this stack</param>
        /// <returns>Item instance</returns>
        public ItemInstance CreateItem(ItemData proto, byte count = 1)
        {
            return new ItemInstance {ItemId = proto.Id, Count = count};
        }
    }
}
