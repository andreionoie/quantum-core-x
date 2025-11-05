using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.World;

namespace QuantumCore.API.Game.Items;

/// <summary>
/// Use [HandlesItemUse] attribute to specify what items this handler processes.
/// If multiple handlers match, last registered wins (plugins override core).
/// </summary>
public interface IItemUseHandler
{
    /// <summary>
    /// Optional validation before handling. 
    /// </summary>
    bool ShouldRegisterFor(ItemData itemProto) => true;

    /// <summary>
    /// Return true if handled successfully, false otherwise.
    /// </summary>
    Task<bool> HandleAsync(ItemUseContext context);
}

public record ItemUseContext(
    IGameConnection Connection,
    ItemInstance Item,
    ItemData ItemProto,
    byte Window,
    ushort Position)
{
    public IPlayerEntity Player => Connection.Player!;
}
