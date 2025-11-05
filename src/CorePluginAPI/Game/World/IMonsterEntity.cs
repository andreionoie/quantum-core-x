using QuantumCore.API.Core.Models;

namespace QuantumCore.API.Game.World
{
    public interface IMonsterEntity : IEntity, IAffectable
    {
        MonsterData Proto { get; }
    }
}
