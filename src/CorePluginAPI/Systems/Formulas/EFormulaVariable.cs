using System.Reflection;
using System.Runtime.Serialization;

namespace QuantumCore.API.Systems.Formulas;

public enum EFormulaVariable
{
    [EnumMember(Value = "lv")] Level,
    [EnumMember(Value = "k")] SkillLevel,
    [EnumMember(Value = "v")] CurrentValue,
    [EnumMember(Value = "maxv")] MaxValue,
    [EnumMember(Value = "atk")] AttackValue,
    [EnumMember(Value = "ar")] AttackRating,
    [EnumMember(Value = "str")] Strength,
    [EnumMember(Value = "dex")] Dexterity,
    [EnumMember(Value = "con")] Constitution,
    [EnumMember(Value = "iq")] Intelligence,
    [EnumMember(Value = "maxhp")] MaxHp,
    [EnumMember(Value = "maxsp")] MaxSp,
    [EnumMember(Value = "wep")] WeaponAttack,
    [EnumMember(Value = "mwep")] MagicWeaponAttack,
    [EnumMember(Value = "mtk")] MagicAttack,
    [EnumMember(Value = "def")] Defence,
    [EnumMember(Value = "odef")] OriginalDefence,
    [EnumMember(Value = "horse_level")] HorseLevel,
    [EnumMember(Value = "chain")] ChainCount
}

public static class EFormulaVariableExtensions
{
    private static readonly Dictionary<EFormulaVariable, string> VariableIdentifiers;

    static EFormulaVariableExtensions()
    {
        VariableIdentifiers = new Dictionary<EFormulaVariable, string>();
        foreach (var variable in Enum.GetValues<EFormulaVariable>())
        {
            VariableIdentifiers[variable] = GetEnumMemberValue(variable);
        }
    }

    /// <summary>
    /// Returns the identifier used in the raw poly expression (atk, dex, maxhp etc.)
    /// </summary>
    public static string GetIdentifier(this EFormulaVariable variable)
    {
        return VariableIdentifiers[variable];
    }

    private static string GetEnumMemberValue(EFormulaVariable variable)
    {
        var member = typeof(EFormulaVariable)
            .GetMember(variable.ToString(), BindingFlags.Public | BindingFlags.Static);

        var enumMember = member.Length > 0
            ? member[0].GetCustomAttribute<EnumMemberAttribute>()
            : null;

        if (enumMember is null || string.IsNullOrWhiteSpace(enumMember.Value))
        {
            throw new InvalidOperationException($"EFormulaVariable '{variable}' does not define an EnumMember value.");
        }

        return enumMember.Value;
    }
}
