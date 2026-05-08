using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XPacketRpc.Generators.Discovery;

internal sealed class CallSiteCollector
{
    private static readonly HashSet<string> XPRpcMethods = new()
    {
        "Touch", "Write", "Read"
    };
    private static readonly HashSet<string> RpcSerializerMethods = new()
    {
        "Serialize", "Deserialize"
    };

    public IEnumerable<DiscoveredType> Collect(
        SyntaxTree tree,
        SemanticModel model,
        CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (TryExtract(invocation, model, ct, out var discovered))
                yield return discovered!;
        }
    }

    private bool TryExtract(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken ct,
        out DiscoveredType? discovered)
    {
        discovered = null;

        var symbolInfo = model.GetSymbolInfo(invocation, ct);
        var method = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.FirstOrDefault() as IMethodSymbol;
        if (method is null) return false;

        if (!IsRelevant(method)) return false;
        if (method.TypeArguments.Length != 1) return false;

        var t = method.TypeArguments[0];
        bool isOpen = t.TypeKind == TypeKind.TypeParameter;

        discovered = new DiscoveredType(
            Type: t,
            CallSiteLocation: invocation.GetLocation(),
            IsOpen: isOpen);
        return true;
    }

    private static bool IsRelevant(IMethodSymbol method)
    {
        var container = method.ContainingType;
        if (container is null) return false;
        var fq = container.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (fq == "global::XPacketRpc.XPRpc" && XPRpcMethods.Contains(method.Name)) return true;
        if (fq == "global::XPacketRpc.IRpcSerializer" && RpcSerializerMethods.Contains(method.Name)) return true;

        // Also: implementations of IRpcSerializer (Serialize/Deserialize on subclasses)
        if (RpcSerializerMethods.Contains(method.Name) &&
            container.AllInterfaces.Any(i =>
                i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::XPacketRpc.IRpcSerializer"))
        {
            return true;
        }
        return false;
    }
}
