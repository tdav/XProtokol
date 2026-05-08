using Microsoft.CodeAnalysis;

namespace XPacketRpc.Generators.Diagnostics;

internal static class Descriptors
{
    private const string Category = "XPacketRpc";

    public static readonly DiagnosticDescriptor OpenGenericCallSite = new(
        id: "XPRPC001",
        title: "Open-generic call-site cannot be resolved",
        messageFormat: "Open-generic call-site for '{0}': T '{1}' cannot be resolved at compile-time. " +
                       "Add 'XPRpc.Touch<ConcreteType>()' in startup so the source generator can emit code.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OpenGenericType = new(
        id: "XPRPC002",
        title: "Open-generic type in transitive closure",
        messageFormat: "Open-generic type '{0}' reached in transitive closure of DTO '{1}'. " +
                       "Sourcegen requires closed types.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CannotConstructType = new(
        id: "XPRPC003",
        title: "Cannot construct type",
        messageFormat: "Cannot construct '{0}': no parameterless constructor and no constructor with " +
                       "parameters matching property names; or some required member has no setter and " +
                       "is not in any constructor.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedFieldType = new(
        id: "XPRPC004",
        title: "Unsupported field type",
        messageFormat: "Field type '{0}' of '{1}.{2}' is unsupported. " +
                       "Supported: primitives, string, Guid, DateTime, DateTimeOffset, TimeSpan, decimal, " +
                       "byte[], enums, T[], List<T>, Dictionary<K,V>, nested DTO, Nullable<T>.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FieldHashCollision = new(
        id: "XPRPC005",
        title: "Field name collision after FNV-1a hash",
        messageFormat: "Fields '{0}' and '{1}' of '{2}' produce identical FNV-1a hash AND identical name " +
                       "(should be impossible). Rename one field.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyType = new(
        id: "XPRPC006",
        title: "Type has no serializable members",
        messageFormat: "Type '{0}' has no public fields or properties — wire payload will be empty " +
                       "(or just the nullability bitmap which is also empty).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
