using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumCore.API.Game.Items;
using QuantumCore.API.Game.Types.Items;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Extensions;

namespace QuantumCore.Game.ItemHandlers.Consumables;

[HandlesItemUse(Type = EItemType.Skillbook)]
public class SkillbookHandler(ILogger<SkillbookHandler> logger, IOptions<GameOptions> gameOptions)
    : IItemUseHandler
{
    private readonly SkillsOptions _skillsOptions = gameOptions.Value.Skills;

    public async Task<bool> HandleAsync(ItemUseContext context)
    {
        var player = context.Player;
        var item = context.Item;
        var itemProto = context.ItemProto;

        // Determine skill ID from either generic skillbook socket or item values
        var skillId = itemProto.Id == _skillsOptions.GenericSkillBookId
            ? itemProto.Sockets[0]
            : itemProto.Values[0];

        if (!Enum.TryParse<ESkill>(skillId.ToString(), out var skill))
        {
            logger.LogWarning("Skill with Id({SkillId}) not defined", skillId);
            return false;
        }

        if (!player.Skills.LearnSkillByBook(skill))
        {
            return false;
        }

        // Set cooldown for next skill read
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var delay = CoreRandom.GenerateInt32(_skillsOptions.SkillBookDelayMin,
            _skillsOptions.SkillBookDelayMax + 1);

        player.Skills.SetSkillNextReadTime(skill, (int)currentTime + delay);

        // Consume the skillbook
        player.RemoveItem(item);
        player.SendRemoveItem(context.Window, context.Position);

        return await Task.FromResult(true);
    }
}
