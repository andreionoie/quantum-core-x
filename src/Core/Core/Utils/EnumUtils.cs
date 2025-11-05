using System.Runtime.CompilerServices;

namespace QuantumCore.Core.Utils;

/// <summary>
/// Fast, cache-friendly validation and guarded casts from raw values to <typeparamref name="TEnum"/>.
/// Accepts only named enum members (no combinations for [Flags]).
/// </summary>
public static class EnumUtils<TEnum> where TEnum : struct, Enum
{
    private static readonly TypeCode UnderlyingTypeCode = Type.GetTypeCode(Enum.GetUnderlyingType(typeof(TEnum)));

    private static readonly ulong Mask = UnderlyingTypeCode switch
    {
        TypeCode.SByte or TypeCode.Byte => byte.MaxValue,
        TypeCode.Int16 or TypeCode.UInt16 => ushort.MaxValue,
        TypeCode.Int32 or TypeCode.UInt32 => uint.MaxValue,
        TypeCode.Int64 or TypeCode.UInt64 => ulong.MaxValue,
        _ => throw new InvalidOperationException("Unsupported enum underlying type.")
    };

    private static readonly ulong[] EnumValuesSorted = BuildSortedArray();
    private static readonly string EnumName = typeof(TEnum).Name;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDefined(ulong value)
    {
        return TryLookup(value, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDefined(long value)
    {
        return TryLookup(unchecked((ulong) value), out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDefined(uint value)
    {
        return IsDefined((ulong) value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDefined(int value)
    {
        return IsDefined((long) value);
    }

    /// <summary>Guarded cast: returns the enum if valid, otherwise throws.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEnum CheckedCast(ulong rawValue)
    {
        if (!TryLookup(rawValue, out var key))
        {
            throw new ArgumentOutOfRangeException(nameof(rawValue), rawValue,
                $"Not a defined {EnumName} enum value.");
        }

        return FromUInt64Bits(key);
    }

    /// <summary>Guarded cast: returns the enum if valid, otherwise throws.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEnum CheckedCast(long rawValue)
    {
        var raw = unchecked((ulong) rawValue);
        if (!TryLookup(raw, out var key))
        {
            throw new ArgumentOutOfRangeException(nameof(rawValue), rawValue,
                $"Not a defined {EnumName} enum value.");
        }

        return FromUInt64Bits(key);
    }

    /// <summary>Guarded cast: returns the enum if valid, otherwise throws.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEnum CheckedCast(uint rawValue) => CheckedCast((ulong) rawValue);

    /// <summary>Guarded cast: returns the enum if valid, otherwise throws.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEnum CheckedCast(int rawValue) => CheckedCast((long) rawValue);

    /// <summary>Try-parse into enum if <paramref name="value"/> is a named member.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCast(ulong value, out TEnum result)
    {
        if (TryLookup(value, out var key))
        {
            result = FromUInt64Bits(key);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Try-parse into enum if <paramref name="value"/> is a named member.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCast(long value, out TEnum result)
    {
        return TryCast(unchecked((ulong) value), out result);
    }

    /// <summary>Try-parse into enum if <paramref name="value"/> is a named member.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCast(uint value, out TEnum result)
    {
        return TryCast((ulong) value, out result);
    }

    /// <summary>Try-parse into enum if <paramref name="value"/> is a named member.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCast(int value, out TEnum result)
    {
        return TryCast((long) value, out result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryLookup(ulong raw, out ulong key)
    {
        // Reject values that don't fit in the enum's underlying width.
        // This prevents wrap-around (263 -> 7 for byte-backed enums).
        if ((raw & ~Mask) != 0)
        {
            key = 0;
            return false;
        }

        key = raw; // use the unmasked value
        key = raw & Mask;
        return Array.BinarySearch(EnumValuesSorted, key) >= 0;
    }

    private static ulong ToUInt64Bits(TEnum value)
    {
        return UnderlyingTypeCode switch
        {
            TypeCode.SByte => unchecked((ulong) Unsafe.As<TEnum, sbyte>(ref value)),
            TypeCode.Byte => Unsafe.As<TEnum, byte>(ref value),
            TypeCode.Int16 => unchecked((ulong) Unsafe.As<TEnum, short>(ref value)),
            TypeCode.UInt16 => Unsafe.As<TEnum, ushort>(ref value),
            TypeCode.Int32 => unchecked((ulong) Unsafe.As<TEnum, int>(ref value)),
            TypeCode.UInt32 => Unsafe.As<TEnum, uint>(ref value),
            TypeCode.Int64 => unchecked((ulong) Unsafe.As<TEnum, long>(ref value)),
            TypeCode.UInt64 => Unsafe.As<TEnum, ulong>(ref value),
            _ => throw new InvalidOperationException("Unsupported enum underlying type.")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TEnum FromUInt64Bits(ulong key)
    {
        switch (UnderlyingTypeCode)
        {
            case TypeCode.SByte:
                var sb = unchecked((sbyte) key);
                return Unsafe.As<sbyte, TEnum>(ref sb);
            case TypeCode.Byte:
                var b = (byte) key;
                return Unsafe.As<byte, TEnum>(ref b);
            case TypeCode.Int16:
                var s = unchecked((short) key);
                return Unsafe.As<short, TEnum>(ref s);
            case TypeCode.UInt16:
                var us = (ushort) key;
                return Unsafe.As<ushort, TEnum>(ref us);
            case TypeCode.Int32:
                var i = unchecked((int) key);
                return Unsafe.As<int, TEnum>(ref i);
            case TypeCode.UInt32:
                var ui = (uint) key;
                return Unsafe.As<uint, TEnum>(ref ui);
            case TypeCode.Int64:
                var l = unchecked((long) key);
                return Unsafe.As<long, TEnum>(ref l);
            case TypeCode.UInt64:
                var ul = key;
                return Unsafe.As<ulong, TEnum>(ref ul);
            default:
                throw new InvalidOperationException("Unsupported enum underlying type.");
        }
    }

    private static ulong[] BuildSortedArray()
    {
        var source = Enum.GetValues<TEnum>();

        if (source.Length <= 256)
        {
            Span<ulong> temp = stackalloc ulong[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                temp[i] = ToUInt64Bits(source[i]);
            }

            temp.Sort();
            return temp.ToArray();
        }

        var result = new ulong[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            result[i] = ToUInt64Bits(source[i]);
        }

        Array.Sort(result);
        return result;
    }
}
