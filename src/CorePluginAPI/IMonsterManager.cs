using System.Collections.Immutable;
using QuantumCore.API.Core.Models;
using QuantumCore.API.Systems.Stats;

namespace QuantumCore.API;

public interface IMonsterManager
{
    /// <summary>
    /// Try to load mob_proto file
    /// </summary>
    Task LoadAsync(CancellationToken token = default);

    MonsterData? GetMonster(uint id);
    ImmutableArray<MonsterData> GetMonsters();
    
    StatEngine.BaseValueSupplierFactory BasePointsForMonster(uint id);
}
