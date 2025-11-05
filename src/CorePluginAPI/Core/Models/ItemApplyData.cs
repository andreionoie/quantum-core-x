using BinarySerialization;

namespace QuantumCore.API.Core.Models;

public class ItemApplyData
{
    [FieldOrder(1)] public byte Type { get; set; }
    [FieldOrder(2)] public uint Value { get; set; }

    public void Deconstruct(out EApplyType type, out int value)
    {
        type = (EApplyType)Type;
        value = (int)Value;
    }
}
