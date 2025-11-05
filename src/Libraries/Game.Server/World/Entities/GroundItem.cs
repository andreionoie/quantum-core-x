using QuantumCore.API;
using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Combat;
using QuantumCore.API.Game.World;
using QuantumCore.Game.Packets;
using QuantumCore.Game.Constants;

namespace QuantumCore.Game.World.Entities;

public class GroundItem : Entity, IGroundItem
{
    private readonly ItemInstance _item;
    private readonly uint _amount;
    private string? _ownerName;
    private long _droppedAtMs;

    public ItemInstance Item => _item;
    public uint Amount => _amount;
    public string? OwnerName => _ownerName;

    public GroundItem(IAnimationManager animationManager, uint vid, ItemInstance item, uint amount,
        string? ownerName = null) : base(animationManager, vid)
    {
        _item = item;
        _amount = amount;
        _ownerName = ownerName;

        _droppedAtMs = GameServer.Instance.ServerTime;
    }

    public override EEntityType Type { get; }

    public override byte HealthPercentage { get; } = 0;

    protected override void OnNewNearbyEntity(IEntity entity)
    {
    }

    protected override void OnRemoveNearbyEntity(IEntity entity)
    {
    }

    public override void OnDespawn()
    {
    }

    public override void ShowEntity(IConnection connection)
    {
        connection.Send(new GroundItemAdd
        {
            PositionX = PositionX, PositionY = PositionY, Vid = Vid, ItemId = _item.ItemId
        });
        connection.Send(new ItemOwnership {Vid = Vid, Player = OwnerName ?? ""});
    }

    public override void HideEntity(IConnection connection)
    {
        connection.Send(new GroundItemRemove {Vid = Vid});
    }

    public override uint GetPoint(EPoint point)
    {
        throw new NotImplementedException();
    }

    public override EBattleType GetBattleType()
    {
        throw new NotImplementedException();
    }

    public override int GetMinDamage()
    {
        throw new NotImplementedException();
    }

    public override int GetMaxDamage()
    {
        throw new NotImplementedException();
    }

    public override int GetBonusDamage()
    {
        throw new NotImplementedException();
    }

    public override void AddPoint(EPoint point, int value)
    {
    }

    public override void SetPoint(EPoint point, uint value)
    {
    }

    public override void Update(double elapsedTime)
    {
        base.Update(elapsedTime);

        var ageMs = GameServer.Instance.ServerTime - _droppedAtMs;
        
        // Release owner-only pickup window
        if (_ownerName != null && ageMs >= SchedulingConstants.GroundItemOwnershipLockSeconds * 1000L)
        {
            _ownerName = null;

            var packet = new ItemOwnership { Vid = Vid, Player = "" };
            foreach (var e in NearbyEntities)
            {
                if (e is PlayerEntity p)
                {
                    p.Connection.Send(packet);
                }
            }
        }

        // Despawn when lifetime expires
        if (ageMs >= SchedulingConstants.GroundItemLifetimeSeconds * 1000L)
        {
            Map?.DespawnEntity(this);
        }
    }
}
