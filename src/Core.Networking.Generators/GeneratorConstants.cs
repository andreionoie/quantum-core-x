using Microsoft.CodeAnalysis;

namespace QuantumCore.Networking;

internal static class GeneratorConstants
{
    internal static readonly string[] SupportedTypesByBitConverter =
        ["Half", "Double", "Single", "Int16", "Int32", "Int64", "UInt16", "UInt32", "UInt64", "Char"];

    internal static readonly string[] NoCastTypes = ["Byte", "SByte"];

    internal const SpecialType VarLenSupportedLengthType = SpecialType.System_UInt16;

    public const string FIELDATTRIBUTE_FULLNAME = "QuantumCore.Networking.FieldAttribute";
    public const string SUBPACKETATTRIBUTE_FULLNAME = "QuantumCore.Networking.SubPacketAttribute";
    public const string PACKETGENEREATOR_ATTRIBUTE_FULLNAME = "QuantumCore.Networking.PacketGeneratorAttribute";

    public const string FIELDATTRIBUTE_PACKETSIZEARG = "PacketSize";
    public const string FIELDATTRIBUTE_VARLENARG = "VarLen";
}
