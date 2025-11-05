using QuantumCore.API.Game.Types.Items;

namespace QuantumCore.API.Game.Items;

/// <summary>
/// Defines when this handler should be invoked.
/// All specified conditions must match (AND logic).
/// Multiple [HandlesItemUse] attributes on one class use OR logic.
/// If multiple handlers match the same item, last registered wins (plugins override core).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class HandlesItemUseAttribute : Attribute
{
    private EItemType _type;

    private EWeaponSubtype _weaponSubtype;
    private EArmorSubtype _armorSubtype;
    private EUseSubtype _useSubtype;
    private ECostumeSubtype _costumeSubtype;

    public EItemType Type
    {
        get => _type;
        set { _type = value; HasType = true; }
    }

    public EWeaponSubtype WeaponSubtype
    {
        get => _weaponSubtype;
        set { _weaponSubtype = value; HasWeaponSubtype = true; }
    }

    public EArmorSubtype ArmorSubtype
    {
        get => _armorSubtype;
        set { _armorSubtype = value; HasArmorSubtype = true; }
    }

    public EUseSubtype UseSubtype
    {
        get => _useSubtype;
        set { _useSubtype = value; HasUseSubtype = true; }
    }

    public ECostumeSubtype CostumeSubtype
    {
        get => _costumeSubtype;
        set { _costumeSubtype = value; HasCostumeSubtype = true; }
    }

    public uint ItemId { get; set; }

    public uint ItemIdMin { get; set; }

    public uint ItemIdMax { get; set; }

    public bool HasType { get; private set; }

    public bool HasWeaponSubtype { get; private set; }

    public bool HasArmorSubtype { get; private set; }

    public bool HasUseSubtype { get; private set; }

    public bool HasCostumeSubtype { get; private set; }

    public bool HasSpecificSubtype => HasWeaponSubtype || HasArmorSubtype || HasUseSubtype || HasCostumeSubtype;

    /// <summary>
    /// Computes the effective subtype discriminated union from main type and the specific subtype fields when present,
    /// returns false when not enough info.
    /// </summary>
    public bool TryGetSubtypeUnion(out ItemSubtype subtype)
    {
        subtype = default;

        if (!HasType) return false;

        switch (Type)
        {
            case EItemType.Weapon when HasWeaponSubtype:
                subtype = ItemSubtype.FromWeapon(_weaponSubtype);
                return true;
            case EItemType.Armor when HasArmorSubtype:
                subtype = ItemSubtype.FromArmor(_armorSubtype);
                return true;
            case EItemType.Use when HasUseSubtype:
                subtype = ItemSubtype.FromUse(_useSubtype);
                return true;
            case EItemType.Costume when HasCostumeSubtype:
                subtype = ItemSubtype.FromCostume(_costumeSubtype);
                return true;
            
            // TODO
            case EItemType.FishingRod:
            case EItemType.Skillbook:
            case EItemType.Polymorph:
            case EItemType.Pickaxe:

            case EItemType.None:
            default:
                return false;
        }
    }
    public bool HasItemId => ItemId != 0;
    public bool HasItemIdRange => ItemIdMin != 0 || ItemIdMax != 0;
}
