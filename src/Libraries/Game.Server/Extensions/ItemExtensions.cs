using QuantumCore.API;
using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Items;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Systems.Stats;
using QuantumCore.Caching;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Persistence;

namespace QuantumCore.Game.Extensions;

public static class ItemExtensions
{
    public static int GetMinWeaponBaseDamage(this ItemData item)
    {
        return item.Values[3];
    }

    public static int GetMaxWeaponBaseDamage(this ItemData item)
    {
        return item.Values[4];
    }

    public static int GetMinMagicWeaponBaseDamage(this ItemData item)
    {
        return item.Values[1];
    }

    public static int GetMaxMagicWeaponBaseDamage(this ItemData item)
    {
        return item.Values[2];
    }

    public static int GetApplyValue(this ItemData item, EApplyType type)
    {
        var apply = item.Applies.FirstOrDefault(x => (EApplyType)x.Type == type);

        return (int)(apply?.Value ?? 0);
    }

    /// <summary>
    /// Weapon damage added additionally to the base damage
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public static int GetAdditionalWeaponDamage(this ItemData item)
    {
        return item.Values[5];
    }

    public static int GetMinWeaponDamage(this ItemData item)
    {
        return item.GetMinWeaponBaseDamage() + item.GetAdditionalWeaponDamage();
    }

    public static int GetMaxWeaponDamage(this ItemData item)
    {
        return item.GetMaxWeaponBaseDamage() + item.GetAdditionalWeaponDamage();
    }

    public static int GetMinMagicWeaponDamage(this ItemData item)
    {
        return item.GetMinMagicWeaponBaseDamage() + item.GetAdditionalWeaponDamage();
    }

    public static int GetMaxMagicWeaponDamage(this ItemData item)
    {
        return item.GetMaxMagicWeaponBaseDamage() + item.GetAdditionalWeaponDamage();
    }

    public static int RollWeaponDamage(this ItemData? item)
    {
        if (item is null) return 0;
        
        return CoreRandom.GenerateInt32(item.GetMinWeaponBaseDamage(), item.GetMaxWeaponBaseDamage() + 1) +
               item.GetAdditionalWeaponDamage();
    }
    
    public static int RollWeaponMagicDamage(this ItemData? item)
    {
        if (item is null) return 0;
        
        return CoreRandom.GenerateInt32(item.GetMinMagicWeaponBaseDamage(), item.GetMaxMagicWeaponBaseDamage() + 1) +
               item.GetAdditionalWeaponDamage();
    }

    public static bool IsType(this ItemData item, EItemType type)
    {
        return (EItemType)item.Type == type;
    }

    /// <summary>
    /// Gets the item type as an EItemType enum
    /// </summary>
    public static EItemType GetItemType(this ItemData item)
    {
        return (EItemType)item.Type;
    }

    public static object GetSubtypeCast(this ItemData item)
    {
        var type = item.GetItemType();
        if (ItemSubtype.TryFrom(type, item.Subtype, out var st))
        {
            if (st.IsWeapon)   return (EWeaponSubtype)st.ToRaw();
            if (st.IsArmor)    return (EArmorSubtype)st.ToRaw();
            if (st.IsUse)      return (EUseSubtype)st.ToRaw();
            if (st.IsCostume)  return (ECostumeSubtype)st.ToRaw();
        }

        return item.Subtype; // fallback to raw
    }

    public static bool IsSubtype(this ItemData item, params EArmorSubtype[] armorSubtypes)
    {
        if (!ItemSubtype.TryFrom(item.GetItemType(), item.Subtype, out var subtype))
        {
            return false;
        }

        return subtype.IsArmor && armorSubtypes.Any(armorSubtype => (EArmorSubtype)subtype.ToRaw() == armorSubtype);
    }

    public static bool IsSubtype(this ItemData item, params ECostumeSubtype[] costumeSubtypes)
    {
        if (!ItemSubtype.TryFrom(item.GetItemType(), item.Subtype, out var subtype))
        {
            return false;
        }

        return subtype.IsCostume && costumeSubtypes.Any(costumeSubtype => (ECostumeSubtype)subtype.ToRaw() == costumeSubtype);
    }
    
    public static bool IsSubtype(this ItemData item, params EWeaponSubtype[] weaponSubtypes)
    {
        if (!ItemSubtype.TryFrom(item.GetItemType(), item.Subtype, out var subtype))
        {
            return false;
        }

        return subtype.IsWeapon && weaponSubtypes.Any(weaponSubtype => (EWeaponSubtype)subtype.ToRaw() == weaponSubtype);
    }

    public static bool IsSubtype(this ItemData item, params EUseSubtype[] useSubtypes)
    {
        if (!ItemSubtype.TryFrom(item.GetItemType(), item.Subtype, out var subtype))
        {
            return false;
        }

        return subtype.IsUse && useSubtypes.Any(useSubtype => (EUseSubtype)subtype.ToRaw() == useSubtype);
    }

    public static uint GetHairPartId(this IEquipment equipmentWindow, IItemManager itemManager)
    {
        if (equipmentWindow.Hair != null)
        {
            var item = itemManager.GetItem(equipmentWindow.Hair.ItemId);
            
            return item.GetHairPartId();
        }

        return 0;
    }

    public static uint GetHairPartId(this ItemData? itemData)
    {
        if (itemData is { Values.Count: > 3 })
        {
            return (uint) itemData.Values[3];
        }

        return 0;
    }

    public static uint GetBodyPartId(this IEquipment equipmentWindow)
    {
        var bodyPartItem = equipmentWindow.Costume ?? equipmentWindow.Body;

        return bodyPartItem?.ItemId ?? 0;
    }

    public static uint GetWeaponId(this IInventory inventory)
    {
        return inventory.EquipmentWindow.Weapon?.ItemId ?? 0;
    }

    public static EquipmentSlots? GetWearSlot(this IItemManager itemManager, uint itemId)
    {
        var proto = itemManager.GetItem(itemId);
        if (proto == null)
        {
            return null;
        }

        return proto.GetWearSlot();
    }

    public static EquipmentSlots? GetWearSlot(this ItemData proto)
    {
        if (proto.IsSubtype(ECostumeSubtype.CostumeBody))
        {
            return EquipmentSlots.Costume;
        }
        
        if (proto.IsSubtype(ECostumeSubtype.CostumeHair))
        {
            return EquipmentSlots.Hair;
        }
        
        return ((EWearFlags)proto.WearFlags).GetWearSlot();
    }

    private static EquipmentSlots? GetWearSlot(this EWearFlags wearFlags)
    {
        return wearFlags switch
        {
            EWearFlags.None => null,
            _ when wearFlags.HasFlag(EWearFlags.Head)     => EquipmentSlots.Head,
            _ when wearFlags.HasFlag(EWearFlags.Shoes)    => EquipmentSlots.Shoes,
            _ when wearFlags.HasFlag(EWearFlags.Bracelet) => EquipmentSlots.Bracelet,
            _ when wearFlags.HasFlag(EWearFlags.Weapon)   => EquipmentSlots.Weapon,
            _ when wearFlags.HasFlag(EWearFlags.Necklace) => EquipmentSlots.Necklace,
            _ when wearFlags.HasFlag(EWearFlags.Earrings) => EquipmentSlots.Earring,
            _ when wearFlags.HasFlag(EWearFlags.Body)     => EquipmentSlots.Body,
            _ when wearFlags.HasFlag(EWearFlags.Shield)   => EquipmentSlots.Shield,
            _ when wearFlags.HasFlag(EWearFlags.Arrow)    => EquipmentSlots.Arrow,
            _ when wearFlags.HasFlag(EWearFlags.Unique)   => EquipmentSlots.Unique1,
            _ => throw new NotImplementedException($"No equipment slot for wear flags: {wearFlags}")
        };
    }

    public static async Task<ItemInstance?> GetItem(this IItemRepository repository, ICacheManager cacheManager,
        Guid id)
    {
        var key = "item:" + id;

        if (await cacheManager.Server.Exists(key) > 0)
        {
            return await cacheManager.Server.Get<ItemInstance>(key);
        }

        var item = await repository.GetItemAsync(id);
        await cacheManager.Server.Set(key, item);
        return item;
    }

    public static async Task DeletePlayerItemAsync(this IItemRepository repository, ICacheManager cacheManager,
        uint playerId, uint itemId)
    {
        var key = $"item:{itemId}";

        await cacheManager.Del(key);

        await repository.DeletePlayerItemAsync(playerId, itemId);
    }

    public static async IAsyncEnumerable<ItemInstance> GetItems(this IItemRepository repository,
        ICacheManager cacheManager, uint player, byte window)
    {
        var key = "items:" + player + ":" + window;

        var list = cacheManager.Server.CreateList<Guid>(key);

        // Check if the window list exists
        if (await cacheManager.Server.Exists(key) > 0)
        {
            var itemIds = await list.Range(0, -1);

            foreach (var id in itemIds)
            {
                var item = await GetItem(repository, cacheManager, id);
                if (item is not null)
                {
                    yield return item;
                }
            }
        }
        else
        {
            var ids = await repository.GetItemIdsForPlayerAsync(player, window);

            foreach (var id in ids)
            {
                await list.Push(id);

                var item = await GetItem(repository, cacheManager, id);
                if (item is not null)
                {
                    yield return item;
                }
            }
        }
    }

    public static async Task<bool> Destroy(this ItemInstance item, ICacheManager cacheManager)
    {
        var key = "item:" + item.Id;

        if (item.PlayerId != default)
        {
            var oldList = cacheManager.Server.CreateList<Guid>($"items:{item.PlayerId}:{item.Window}");
            await oldList.Rem(1, item.Id);
        }

        return await cacheManager.Server.Del(key) != 0;
    }

    public static Task Persist(this ItemInstance item, IItemRepository itemRepository)
    {
        return itemRepository.SaveItemAsync(item);
    }

    /// <summary>
    /// Sets the item position, window, and owner.
    /// Refresh the cache lists if needed, and persists the item
    /// </summary>
    /// <param name="item"></param>
    /// <param name="cacheManager"></param>
    /// <param name="owner">Owner the item is given to</param>
    /// <param name="window">Window the item is placed in</param>
    /// <param name="pos">Position of the item in the window</param>
    public static async Task Set(this ItemInstance item, ICacheManager cacheManager, uint owner, byte window, uint pos,
        IItemRepository itemRepository)
    {
        var isPlayerDifferent = item.PlayerId != owner;
        var isWindowDifferent = item.Window != window;

        item.PlayerId = owner;
        item.Window = window;
        item.Position = pos;
        await Persist(item, itemRepository);

        if (isPlayerDifferent || isWindowDifferent)
        {
            if (item.PlayerId != default)
            {
                // Remove from last list
                var oldList = cacheManager.Server.CreateList<Guid>($"items:{item.PlayerId}:{item.Window}");
                await oldList.Rem(1, item.Id);
            }

            if (owner != default)
            {
                var newList = cacheManager.Server.CreateList<Guid>($"items:{owner}:{window}");
                await newList.Push(item.Id);
            }
        }
    }

    public static EPoint ToPoint(this EApplyType applyType)
    {
         return applyType switch
            {
                EApplyType.None => EPoint.None,
                EApplyType.MaxHp => EPoint.MaxHp,
                EApplyType.MaxSp => EPoint.MaxSp,
                EApplyType.Con => EPoint.Ht,
                EApplyType.Int => EPoint.Iq,
                EApplyType.Str => EPoint.St,
                EApplyType.Dex => EPoint.Dx,
                EApplyType.AttackSpeed => EPoint.AttackSpeed,
                EApplyType.MovSpeed => EPoint.MoveSpeed,
                EApplyType.CastSpeed => EPoint.CastingSpeed,
                EApplyType.HpRegen => EPoint.HpRegen,
                EApplyType.SpRegen => EPoint.SpRegen,
                EApplyType.PoisonPct => EPoint.PoisonPercentage,
                EApplyType.StunPct => EPoint.StunPercentage,
                EApplyType.SlowPct => EPoint.SlowPercentage,
                EApplyType.CriticalPct => EPoint.CriticalPercentage,
                EApplyType.PenetratePct => EPoint.PenetratePercentage,
                EApplyType.AttackBonusHuman => EPoint.AttackBonusHuman,
                EApplyType.AttackBonusAnimal => EPoint.AttackBonusAnimal,
                EApplyType.AttackBonusOrc => EPoint.AttackBonusOrc,
                EApplyType.AttackBonusMilgyo => EPoint.AttackBonusEsoterics,
                EApplyType.AttackBonusUndead => EPoint.AttackBonusUndead,
                EApplyType.AttackBonusDevil => EPoint.AttackBonusDevil,
                EApplyType.StealHp => EPoint.StealHp,
                EApplyType.StealSp => EPoint.StealSp,
                EApplyType.ManaBurnPct => EPoint.ManaBurnPercentage,
                EApplyType.DamageSpRecover => EPoint.DamageSpRecover,
                EApplyType.Block => EPoint.Block,
                EApplyType.Dodge => EPoint.Dodge,
                EApplyType.ResistSword => EPoint.ResistSword,
                EApplyType.ResistTwoHand => EPoint.ResistTwoHanded,
                EApplyType.ResistDagger => EPoint.ResistDagger,
                EApplyType.ResistBell => EPoint.ResistBell,
                EApplyType.ResistFan => EPoint.ResistFan,
                EApplyType.ResistBow => EPoint.ResistBow,
                EApplyType.ResistFire => EPoint.ResistFire,
                EApplyType.ResistElec => EPoint.ResistElectric,
                EApplyType.ResistMagic => EPoint.ResistMagic,
                EApplyType.ResistWind => EPoint.ResistWind,
                EApplyType.ReflectMelee => EPoint.ReflectMelee,
                EApplyType.ReflectCurse => EPoint.ReflectCurse,
                EApplyType.PoisonReduce => EPoint.PoisonReduce,
                EApplyType.KillSpRecover => EPoint.KillSpRecover,
                EApplyType.ExpDoubleBonus => EPoint.ExpDoubleBonus,
                EApplyType.GoldDoubleBonus => EPoint.GoldDoubleBonus,
                EApplyType.ItemDropBonus => EPoint.ItemDropBonus,
                EApplyType.PotionBonus => EPoint.PotionBonus,
                EApplyType.KillHpRecover => EPoint.KillHpRecover,
                EApplyType.ImmuneStun => EPoint.ImmuneStun,
                EApplyType.ImmuneSlow => EPoint.ImmuneSlow,
                EApplyType.ImmuneFall => EPoint.ImmuneFall,
                EApplyType.Skill => EPoint.None, // not implemented yet
                EApplyType.BowDistance => EPoint.BowDistance,
                EApplyType.AttGradeBonus => EPoint.AttackGradeBonus,
                EApplyType.DefGradeBonus => EPoint.DefenceGradeBonus,
                EApplyType.MagicAttGrade => EPoint.MagicAttackGradeBonus,
                EApplyType.MagicDefGrade => EPoint.MagicDefenceGradeBonus,
                EApplyType.CursePct => EPoint.CursePercentage,
                EApplyType.MaxStamina => EPoint.MaxStamina,
                EApplyType.AttackBonusWarrior => EPoint.AttackBonusWarrior,
                EApplyType.AttackBonusAssassin => EPoint.AttackBonusAssassin,
                EApplyType.AttackBonusSura => EPoint.AttackBonusSura,
                EApplyType.AttackBonusShaman => EPoint.AttackBonusShaman,
                EApplyType.AttackBonusMonster => EPoint.AttackBonusMonster,
                EApplyType.MallAttackBonus => EPoint.MallAttBonus,
                EApplyType.MallDefBonus => EPoint.MallDefBonus,
                EApplyType.MallExpBonus => EPoint.MallExpBonus,
                EApplyType.MallItemBonus => EPoint.MallItemBonus,
                EApplyType.MallGoldBonus => EPoint.MallGoldBonus,
                EApplyType.MaxHpPct => EPoint.MaxHpPercentage,
                EApplyType.MaxSpPct => EPoint.MaxSpPercentage,
                EApplyType.SkillDamageBonus => EPoint.SkillDamageBonus,
                EApplyType.NormalHitDamageBonus => EPoint.NormalHitDamageBonus,
                EApplyType.SkillDefendBonus => EPoint.SkillDefendBonus,
                EApplyType.NormalHitDefendBonus => EPoint.NormalHitDefendBonus,
                EApplyType.ResistWarrior => EPoint.ResistWarrior,
                EApplyType.ResistAssassin => EPoint.ResistAssassin,
                EApplyType.ResistSura => EPoint.ResistSura,
                EApplyType.ResistShaman => EPoint.ResistShaman,
                EApplyType.Energy => EPoint.Energy,
                EApplyType.DefGrade => EPoint.DefenceGrade,
                EApplyType.CostumeAttrBonus => EPoint.CostumeAttrBonus,
                EApplyType.MagicAttackBonusPer => EPoint.MagicAttackBonusPer,
                EApplyType.MeleeMagicAttackBonusPer => EPoint.MeleeMagicAttackBonusPer,
                EApplyType.ResistIce => EPoint.ResistIce,
                EApplyType.ResistEarth => EPoint.ResistEarth,
                EApplyType.ResistDark => EPoint.ResistDark,
                EApplyType.AntiCriticalPct => EPoint.ResistCritical,
                EApplyType.AntiPenetratePct => EPoint.ResistPenetrate,
                _ => EPoint.None
            };
    }

    public static EAffectType ToAffectType(this EApplyType applyType)
    {
        return applyType switch
        {
            EApplyType.Con => EAffectType.Constitution,
            EApplyType.Int => EAffectType.Intelligence,
            EApplyType.Str => EAffectType.Strength,
            EApplyType.Dex => EAffectType.Dexterity,
            EApplyType.AttackSpeed => EAffectType.AttackSpeed,
            EApplyType.MovSpeed => EAffectType.MoveSpeed,
            EApplyType.CastSpeed => EAffectType.CastSpeed,
            EApplyType.AttGradeBonus => EAffectType.AttackGrade,
            EApplyType.DefGradeBonus => EAffectType.DefenseGrade,
            _ => EAffectType.None
        };
    }

    public static EAffect ToAffect(this EApplyType applyType)
    {
        return applyType switch
        {
            EApplyType.AttackSpeed => EAffect.AttackSpeedPotion,
            EApplyType.MovSpeed => EAffect.MovementSpeedPotion,
            _ => EAffect.None
        };
    }
    
    public static EquipmentModifierSource AsModifierSource(this EquipmentSlots slot) => new(slot);

    public readonly record struct EquipmentModifierSource(EquipmentSlots Slot) : IStatEngine.IModifierSource
    {
        public string ModifierKey => $"equip:{Slot}";
    }
}
