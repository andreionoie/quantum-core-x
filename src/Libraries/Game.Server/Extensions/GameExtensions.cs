using QuantumCore.API;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Combat;
using QuantumCore.API.Game.Types.Monsters;
using QuantumCore.API.Game.World;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Constants;

namespace QuantumCore.Game.Extensions;

public static class GameExtensions
{
    public static IEnumerable<IPlayerEntity> GetNearbyPlayers(this IEntity entity)
    {
        foreach (var nearbyEntity in entity.NearbyEntities.ToArray())
        {
            if (nearbyEntity is IPlayerEntity p)
            {
                yield return p;
            }
        }
    }

    // TODO: rework this into a special immunity system instead of using points as a proxy?
    public static bool TryImmunityCheck(this IEntity entity, EImmunityFlags immunity)
    {
        var immunityPoint = immunity switch
        {
            EImmunityFlags.StunImmunity => EPoint.ImmuneStun,
            EImmunityFlags.SlowImmunity => EPoint.ImmuneSlow,
            EImmunityFlags.FallImmunity => EPoint.ImmuneFall,
            _ => throw new ArgumentOutOfRangeException(nameof(immunity), immunity, "Unsupported immunity type")
        };

        return entity.GetPoint(immunityPoint) > 0 &&
               CoreRandom.PercentageCheck(SchedulingConstants.ImmunitySuccessPercentage);
    }
    
    public static EPoint? GetRaceBonusPoint(ERaceFlag race)
    {
        if (race.HasFlag(ERaceFlag.Animal))   return EPoint.AttackBonusAnimal;
        if (race.HasFlag(ERaceFlag.Undead))   return EPoint.AttackBonusUndead;
        if (race.HasFlag(ERaceFlag.Devil))    return EPoint.AttackBonusDevil;
        if (race.HasFlag(ERaceFlag.Human))    return EPoint.AttackBonusHuman;
        if (race.HasFlag(ERaceFlag.Orc))      return EPoint.AttackBonusOrc;
        if (race.HasFlag(ERaceFlag.Esoteric)) return EPoint.AttackBonusEsoterics;
        if (race.HasFlag(ERaceFlag.Insect))   return EPoint.AttackBonusInsect;
        if (race.HasFlag(ERaceFlag.Fire))     return EPoint.AttackBonusFire;
        if (race.HasFlag(ERaceFlag.Ice))      return EPoint.AttackBonusIce;
        if (race.HasFlag(ERaceFlag.Desert))   return EPoint.AttackBonusDesert;
        if (race.HasFlag(ERaceFlag.Tree))     return EPoint.AttackBonusTree;

        return null;
    }

    public static EPoint? GetElementsResistPoint(ERaceFlag race)
    {
        if (race.HasFlag(ERaceFlag.AttackElectric)) return EPoint.ResistElectric;
        if (race.HasFlag(ERaceFlag.AttackFire))     return EPoint.ResistFire;
        if (race.HasFlag(ERaceFlag.AttackIce))      return EPoint.ResistIce;
        if (race.HasFlag(ERaceFlag.AttackWind))     return EPoint.ResistWind;
        if (race.HasFlag(ERaceFlag.AttackEarth))    return EPoint.ResistEarth;
        if (race.HasFlag(ERaceFlag.AttackDark))     return EPoint.ResistDark;

        return null;
    }

    public static EPoint GetClassBonusPoint(EPlayerClass playerClass)
    {
        return playerClass switch
        {
            EPlayerClass.Warrior => EPoint.AttackBonusWarrior,
            EPlayerClass.Ninja => EPoint.AttackBonusAssassin,
            EPlayerClass.Sura => EPoint.AttackBonusSura,
            EPlayerClass.Shaman => EPoint.AttackBonusShaman,
            _ => throw new ArgumentOutOfRangeException(nameof(playerClass))
        };
    }
    
    public static EPoint GetClassResistPoint(EPlayerClass playerClass)
    {
        return playerClass switch
        {
            EPlayerClass.Warrior => EPoint.ResistWarrior,
            EPlayerClass.Ninja => EPoint.ResistAssassin,
            EPlayerClass.Sura => EPoint.ResistSura,
            EPlayerClass.Shaman => EPoint.ResistShaman,
            _ => throw new ArgumentOutOfRangeException(nameof(playerClass))
        };
    }

}
