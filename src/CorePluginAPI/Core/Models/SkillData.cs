using System.Collections.Immutable;
using System.Diagnostics;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Formulas;

namespace QuantumCore.API.Core.Models;

[DebuggerDisplay("{Name} ({Id})")]
public class SkillData
{
    public required ESkill Id { get; set; }
    public string Name { get; set; }
    public ESkillCategoryType Type { get; set; }
    public short LevelStep { get; set; }
    public short MaxLevel { get; set; }
    public short LevelLimit { get; set; }
    public EPoint PointOn { get; set; } = EPoint.None;
    public SkillFormula PointFormula { get; set; } = SkillFormula.Zero;
    public SkillFormula SpCostFormula { get; set; } = SkillFormula.Zero;
    public SkillFormula DurationFormula { get; set; } = SkillFormula.Zero;
    public SkillFormula DurationSpCostFormula { get; set; } = SkillFormula.Zero;
    public SkillFormula CooldownFormula { get; set; } = SkillFormula.Zero;
    public SkillFormula MasterBonusFormula { get; set; } = SkillFormula.Zero;
    public SkillFormula AttackGradeFormula { get; set; } = SkillFormula.Zero;
    public ESkillFlags Flags { get; set; }
    public EAffect AffectFlag { get; set; } = EAffect.None;
    public EPoint PointOn2 { get; set; } = EPoint.None;
    public SkillFormula PointFormula2 { get; set; } = SkillFormula.Zero;
    public SkillFormula DurationFormula2 { get; set; } = SkillFormula.Zero;
    public EAffect AffectFlag2 { get; set; } = EAffect.None;
    public int PrerequisiteSkillVnum { get; set; } = 0;
    public int PrerequisiteSkillLevel { get; set; } = 0;
    public ESkillType SkillType { get; set; } = ESkillType.Normal;
    public short MaxHit { get; set; } = 0;
    public SkillFormula SplashAroundDamageAdjustFormula { get; set; } = SkillFormula.Zero;
    public int TargetRange { get; set; } = 1000;
    public uint SplashRange { get; set; } = 0;

    public ImmutableArray<EFormulaVariable> AllRequiredVariables { get; set; } = ImmutableArray<EFormulaVariable>.Empty;
}
