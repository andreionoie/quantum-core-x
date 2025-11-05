using QuantumCore.API.Game.Types.Skills;

namespace QuantumCore.API.Game.Skills;

public interface ISkill
{
    public ESkill SkillId { get; set; }
    public ESkillMasterType MasterType { get; set; }
    public byte Level { get; set; }
    public int NextReadTime { get; set; }
    // Tracks the last time the player used this skill (server time in ms)
    public long? LastUsedServerTime { get; set; }
}
