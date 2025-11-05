using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.Types.Skills;

namespace QuantumCore.API.Extensions;

public static class PlayerExtensions
{
    public static EPlayerClass GetClass(this EPlayerClassGendered playerClass)
    {
        return playerClass switch
        {
            EPlayerClassGendered.WarriorMale => EPlayerClass.Warrior,
            EPlayerClassGendered.NinjaFemale => EPlayerClass.Ninja,
            EPlayerClassGendered.SuraMale => EPlayerClass.Sura,
            EPlayerClassGendered.ShamanFemale => EPlayerClass.Shaman,
            EPlayerClassGendered.WarriorFemale => EPlayerClass.Warrior,
            EPlayerClassGendered.NinjaMale => EPlayerClass.Ninja,
            EPlayerClassGendered.SuraFemale => EPlayerClass.Sura,
            EPlayerClassGendered.ShamanMale => EPlayerClass.Shaman,
            _ => throw new ArgumentOutOfRangeException(nameof(playerClass), playerClass, null)
        };
    }

    public static EPlayerGender GetGender(this EPlayerClassGendered playerClass)
    {
        return playerClass switch
        {
            EPlayerClassGendered.WarriorMale => EPlayerGender.Male,
            EPlayerClassGendered.NinjaFemale => EPlayerGender.Female,
            EPlayerClassGendered.SuraMale => EPlayerGender.Male,
            EPlayerClassGendered.ShamanFemale => EPlayerGender.Female,
            EPlayerClassGendered.WarriorFemale => EPlayerGender.Female,
            EPlayerClassGendered.NinjaMale => EPlayerGender.Male,
            EPlayerClassGendered.SuraFemale => EPlayerGender.Female,
            EPlayerClassGendered.ShamanMale => EPlayerGender.Male,
            _ => throw new ArgumentOutOfRangeException(nameof(playerClass), playerClass, null)
        };
    }

    public static SkillGroup GetSkillGroup(this PlayerData playerData)
    {
        if (SkillGroup.TryFrom(playerData.PlayerClass.GetClass(), playerData.SkillGroup, out var skillGroup))
        {
           return skillGroup; 
        }
        
        throw new ArgumentOutOfRangeException(nameof(playerData));
    }
    
    public static bool IsSkillGroup(this PlayerData playerData, EWarriorSkillGroup war)
    {
        var rawSkillGroup = playerData.GetSkillGroup().ToRaw();
        return (EWarriorSkillGroup)rawSkillGroup == war;
    }

    public static bool IsSkillGroup(this PlayerData playerData, ENinjaSkillGroup ninja)
    {
        var rawSkillGroup = playerData.GetSkillGroup().ToRaw();
        return (ENinjaSkillGroup)rawSkillGroup == ninja;
    }

    public static bool IsSkillGroup(this PlayerData playerData, ESuraSkillGroup sura)
    {
        var rawSkillGroup = playerData.GetSkillGroup().ToRaw();
        return (ESuraSkillGroup)rawSkillGroup == sura;
    }

    public static bool IsSkillGroup(this PlayerData playerData, EShamanSkillGroup shaman)
    {
        var rawSkillGroup = playerData.GetSkillGroup().ToRaw();
        return (EShamanSkillGroup)rawSkillGroup == shaman;
    }
}
