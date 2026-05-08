using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace XPacketRpc.Generators.Discovery;

internal sealed class TypeWalker
{
    private readonly Compilation comp;
    private readonly INamedTypeSymbol? listOpen;
    private readonly INamedTypeSymbol? dictOpen;
    private readonly INamedTypeSymbol? nullableOpen;

    public TypeWalker(Compilation comp)
    {
        this.comp = comp;
        this.listOpen = comp.GetTypeByMetadataName("System.Collections.Generic.List`1");
        this.dictOpen = comp.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
        this.nullableOpen = comp.GetTypeByMetadataName("System.Nullable`1");
    }

    /// <summary>Returns members of type: public instance fields + properties (declared in type).</summary>
    public IReadOnlyList<MemberDescriptor> GetMembers(INamedTypeSymbol type)
    {
        var result = new List<MemberDescriptor>();
        foreach (var m in type.GetMembers())
        {
            if (m.DeclaredAccessibility != Accessibility.Public) continue;
            if (m.IsStatic) continue;

            switch (m)
            {
                case IFieldSymbol f when !f.IsConst && !f.IsImplicitlyDeclared:
                    result.Add(MakeMember(f.Name, f.Type, isField: true));
                    break;
                case IPropertySymbol p when !p.IsIndexer:
                    result.Add(MakeMember(p.Name, p.Type, isField: false));
                    break;
            }
        }
        return result;
    }

    private MemberDescriptor MakeMember(string name, ITypeSymbol type, bool isField)
    {
        var (kind, inner, k, v) = ClassifyType(type);
        bool nullable = type.NullableAnnotation == NullableAnnotation.Annotated
                        || kind == WireKind.Nullable;

        return new MemberDescriptor(name, type, isField, nullable, kind, inner, k, v);
    }

    private (WireKind kind, ITypeSymbol? inner, ITypeSymbol? key, ITypeSymbol? val) ClassifyType(ITypeSymbol t)
    {
        // Nullable<T>
        if (t is INamedTypeSymbol nts && this.nullableOpen is not null &&
            SymbolEqualityComparer.Default.Equals(nts.OriginalDefinition, this.nullableOpen))
        {
            return (WireKind.Nullable, nts.TypeArguments[0], null, null);
        }

        // byte[]
        if (t is IArrayTypeSymbol arr && arr.ElementType.SpecialType == SpecialType.System_Byte)
            return (WireKind.ByteArray, null, null, null);

        // T[]
        if (t is IArrayTypeSymbol genArr)
            return (WireKind.Array, genArr.ElementType, null, null);

        // Enum
        if (t.TypeKind == TypeKind.Enum) return (WireKind.Enum, null, null, null);

        // Built-ins
        switch (t.SpecialType)
        {
            case SpecialType.System_Boolean: return (WireKind.Bool, null, null, null);
            case SpecialType.System_SByte: return (WireKind.SByte, null, null, null);
            case SpecialType.System_Byte: return (WireKind.Byte, null, null, null);
            case SpecialType.System_Int16: return (WireKind.Int16, null, null, null);
            case SpecialType.System_UInt16: return (WireKind.UInt16, null, null, null);
            case SpecialType.System_Int32: return (WireKind.Int32, null, null, null);
            case SpecialType.System_UInt32: return (WireKind.UInt32, null, null, null);
            case SpecialType.System_Int64: return (WireKind.Int64, null, null, null);
            case SpecialType.System_UInt64: return (WireKind.UInt64, null, null, null);
            case SpecialType.System_Single: return (WireKind.Single, null, null, null);
            case SpecialType.System_Double: return (WireKind.Double, null, null, null);
            case SpecialType.System_Decimal: return (WireKind.Decimal, null, null, null);
            case SpecialType.System_String: return (WireKind.String, null, null, null);
            case SpecialType.System_DateTime: return (WireKind.DateTime, null, null, null);
        }

        // By full name — Guid, DateTimeOffset, TimeSpan
        var fq = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fq == "global::System.Guid") return (WireKind.Guid, null, null, null);
        if (fq == "global::System.DateTimeOffset") return (WireKind.DateTimeOffset, null, null, null);
        if (fq == "global::System.TimeSpan") return (WireKind.TimeSpan, null, null, null);

        // List<T>, Dictionary<K,V>
        if (t is INamedTypeSymbol gnts && gnts.IsGenericType)
        {
            if (this.listOpen is not null && SymbolEqualityComparer.Default.Equals(gnts.OriginalDefinition, this.listOpen))
                return (WireKind.List, gnts.TypeArguments[0], null, null);

            if (this.dictOpen is not null && SymbolEqualityComparer.Default.Equals(gnts.OriginalDefinition, this.dictOpen))
                return (WireKind.Dictionary, null, gnts.TypeArguments[0], gnts.TypeArguments[1]);
        }

        // Otherwise — nested DTO
        return (WireKind.NestedDto, null, null, null);
    }

    /// <summary>Transitive closure: root + all nested DTO + element/key/value DTO recursively.</summary>
    public IReadOnlyCollection<INamedTypeSymbol> Closure(INamedTypeSymbol root)
    {
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var stack = new Stack<INamedTypeSymbol>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (!visited.Add(t)) continue;

            foreach (var m in GetMembers(t))
            {
                AddCandidates(m, stack, visited);
            }
        }
        return visited;
    }

    private void AddCandidates(MemberDescriptor m, Stack<INamedTypeSymbol> stack, HashSet<INamedTypeSymbol> visited)
    {
        switch (m.Kind)
        {
            case WireKind.NestedDto:
                if (m.Type is INamedTypeSymbol n) stack.Push(n);
                break;
            case WireKind.Nullable:
            case WireKind.Array:
            case WireKind.List:
                if (m.ElementOrInner is INamedTypeSymbol el && IsDtoCandidate(el)) stack.Push(el);
                break;
            case WireKind.Dictionary:
                if (m.DictKey is INamedTypeSymbol dk && IsDtoCandidate(dk)) stack.Push(dk);
                if (m.DictValue is INamedTypeSymbol dv && IsDtoCandidate(dv)) stack.Push(dv);
                break;
        }
    }

    private bool IsDtoCandidate(INamedTypeSymbol t)
    {
        var (kind, _, _, _) = ClassifyType(t);
        return kind == WireKind.NestedDto;
    }
}
