using OneOf;

namespace QuantumCore.API.Game.Types.Items;

public readonly struct ItemSubtype : IEquatable<ItemSubtype>
{
    private readonly OneOf<EWeaponSubtype, EArmorSubtype, EUseSubtype, ECostumeSubtype> _value;

    public static ItemSubtype FromWeapon(EWeaponSubtype weapon) => new(weapon);
    public static ItemSubtype FromArmor(EArmorSubtype armor) => new(armor);
    public static ItemSubtype FromUse(EUseSubtype use) => new(use);
    public static ItemSubtype FromCostume(ECostumeSubtype costume) => new(costume);

    public static bool TryFrom(EItemType parent, byte rawSubtype, out ItemSubtype subtype)
    {
        subtype = default;

        switch (parent)
        {
            case EItemType.Weapon:
                if (Enum.IsDefined(typeof(EWeaponSubtype), rawSubtype))      // TODO: switch to the faster EnumUtils.IsDefined ?
                {
                    subtype = FromWeapon((EWeaponSubtype)rawSubtype);
                    return true;
                }
                return false;

            case EItemType.Armor:
                if (Enum.IsDefined(typeof(EArmorSubtype), rawSubtype))
                {
                    subtype = FromArmor((EArmorSubtype)rawSubtype);
                    return true;
                }
                return false;

            case EItemType.Use:
                if (Enum.IsDefined(typeof(EUseSubtype), rawSubtype))
                {
                    subtype = FromUse((EUseSubtype)rawSubtype);
                    return true;
                }
                return false;

            case EItemType.Costume:
                if (Enum.IsDefined(typeof(ECostumeSubtype), rawSubtype))
                {
                    subtype = FromCostume((ECostumeSubtype)rawSubtype);
                    return true;
                }
                return false;

            // TODO
            case EItemType.None:
            case EItemType.FishingRod:
            case EItemType.Skillbook:
            case EItemType.Polymorph:
            case EItemType.Pickaxe:
            default:
                return false;
        }
    }

    public bool IsWeapon => _value.IsT0;
    public bool IsArmor => _value.IsT1;
    public bool IsUse => _value.IsT2;
    public bool IsCostume => _value.IsT3;

    public byte ToRaw() => _value.Match(
        weapon => (byte)weapon,
        armor => (byte)armor,
        use => (byte)use,
        costume => (byte)costume
    );

    public override string ToString() => _value.Match(
        weapon =>   Enum.GetName(weapon) ?? ((byte)weapon).ToString(),
        armor =>    Enum.GetName(armor) ?? ((byte)armor).ToString(),
        use =>      Enum.GetName(use) ?? ((byte)use).ToString(),
        costume =>  Enum.GetName(costume) ?? ((byte)costume).ToString()
    );

    private int Discriminant => _value.IsT0 ? 0 : _value.IsT1 ? 1 : _value.IsT2 ? 2 : _value.IsT3 ? 3 : -1;
    public bool Equals(ItemSubtype other) => Discriminant == other.Discriminant && ToRaw() == other.ToRaw();
    public override bool Equals(object? obj) => obj is ItemSubtype other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Discriminant, ToRaw());
    public static bool operator ==(ItemSubtype left, ItemSubtype right) => left.Equals(right);
    public static bool operator !=(ItemSubtype left, ItemSubtype right) => !left.Equals(right);

    private ItemSubtype(EWeaponSubtype weapon)   => _value = weapon;
    private ItemSubtype(EArmorSubtype armor)     => _value = armor;
    private ItemSubtype(EUseSubtype use)         => _value = use;
    private ItemSubtype(ECostumeSubtype costume) => _value = costume;

}
