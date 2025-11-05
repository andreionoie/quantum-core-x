namespace QuantumCore.API.Game.Types;

[Flags]
public enum ESpawnStateFlags : byte
{
    None = 0,
    WithFallingAnimation = 1 << 1
}
