namespace QuantumCore.API.Game.Types.Combat;

[Flags]
public enum EImmunityFlags : uint
{
    None = 0,
    StunImmunity = 1 << 0,
    SlowImmunity = 1 << 1,
    FallImmunity = 1 << 2,
    CurseImmunity = 1 << 3,
    PoisonImmunity = 1 << 4,
    TerrorImmunity = 1 << 5,
    ReflectImmunity = 1 << 6
}
