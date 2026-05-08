using Microsoft.CodeAnalysis;

namespace XPacketRpc.Generators.Discovery;

internal sealed record MemberDescriptor(
    string Name,
    ITypeSymbol Type,
    bool IsField,
    bool IsNullable,
    WireKind Kind,
    ITypeSymbol? ElementOrInner,
    ITypeSymbol? DictKey,
    ITypeSymbol? DictValue);
