using Microsoft.CodeAnalysis;

namespace XPacketRpc.Generators.Discovery;

internal sealed record DiscoveredType(
    ITypeSymbol Type,
    Location? CallSiteLocation,
    bool IsOpen);
