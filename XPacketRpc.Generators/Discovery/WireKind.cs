namespace XPacketRpc.Generators.Discovery;

internal enum WireKind
{
    Unknown,
    Bool, SByte, Byte, Int16, UInt16, Int32, UInt32, Int64, UInt64, Single, Double, Decimal,
    String, Guid, DateTime, DateTimeOffset, TimeSpan, ByteArray, Enum,
    Nullable,
    Array,
    List,
    Dictionary,
    NestedDto,
}
