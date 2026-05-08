using System.Linq;
using Microsoft.CodeAnalysis;
using XPacketRpc.Generators.Discovery;

namespace XPacketRpc.Generators.Emit;

internal sealed class ReadEmitter
{
    private readonly TypeWalker walker;

    public ReadEmitter(TypeWalker walker) { this.walker = walker; }

    public string EmitReadMethod(INamedTypeSymbol type)
    {
        var members = walker.GetMembers(type);
        var sorted = members
            .OrderBy(m => Fnv1aGen.Hash(m.Name))
            .ThenBy(m => m.Name, System.StringComparer.Ordinal)
            .ToArray();

        var nullableMembers = sorted
            .Select((m, i) => (m, i))
            .Where(t => t.m.IsNullable)
            .ToArray();
        int bitmapBytes = (nullableMembers.Length + 7) / 8;

        var fq = $"global::{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "")}";
        var sb = new IndentedStringBuilder();
        sb.AppendLine($"internal static {fq} Read(ref global::XPacketRpc.XPRpcReader r)");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            if (bitmapBytes == 1)
            {
                sb.AppendLine("byte bitmap = r.ReadByte();");
            }
            else if (bitmapBytes > 1)
            {
                sb.AppendLine($"global::System.Span<byte> bitmap = stackalloc byte[{bitmapBytes}];");
                sb.AppendLine($"for (int i = 0; i < {bitmapBytes}; i++) bitmap[i] = r.ReadByte();");
            }

            int nullableIdx = 0;
            foreach (var m in sorted)
            {
                if (m.IsNullable)
                {
                    int byteIdx = nullableIdx / 8;
                    int bit = nullableIdx % 8;
                    var bitmapAccess = bitmapBytes == 1 ? "bitmap" : $"bitmap[{byteIdx}]";
                    var maskBin = System.Convert.ToString(1 << bit, 2).PadLeft(8, '0');
                    sb.AppendLine($"bool {Camel(m.Name)}IsNull = ({bitmapAccess} & 0b{maskBin}) != 0;");
                    sb.AppendLine($"{TypeName(m.Type)} {Camel(m.Name)} = default!;");
                    sb.AppendLine($"if (!{Camel(m.Name)}IsNull)");
                    sb.AppendLine("{");
                    using (sb.Indent())
                    {
                        sb.AppendLine($"{Camel(m.Name)} = {ReadExpr(m)};");
                    }
                    sb.AppendLine("}");
                    nullableIdx++;
                }
                else
                {
                    sb.AppendLine($"{TypeName(m.Type)} {Camel(m.Name)} = {ReadExpr(m)};");
                }
            }

            // Phase 8: Construction via CtorBinder strategy.
            var binder = new CtorBinder(this.walker);
            var plan = binder.Bind(type);

            switch (plan.Strategy)
            {
                case CtorStrategy.Parameterless:
                    {
                        bool hasInitOnly = type.GetMembers()
                            .OfType<IPropertySymbol>()
                            .Any(p => p.SetMethod?.IsInitOnly == true);
                        if (hasInitOnly)
                        {
                            // Use object-initializer syntax: init setters are only callable from initializers.
                            if (sorted.Length == 0)
                            {
                                sb.AppendLine($"return new {fq}();");
                            }
                            else
                            {
                                var assignments = string.Join(", ", sorted.Select(m => $"{m.Name} = {Camel(m.Name)}"));
                                sb.AppendLine($"return new {fq}() {{ {assignments} }};");
                            }
                        }
                        else
                        {
                            sb.AppendLine($"var __result = new {fq}();");
                            for (int i = 0; i < sorted.Length; i++)
                            {
                                var m = sorted[i];
                                sb.AppendLine($"__result.{m.Name} = {Camel(m.Name)};");
                            }
                            sb.AppendLine("return __result;");
                        }
                    }
                    break;

                case CtorStrategy.AllParams:
                    {
                        var args = string.Join(", ", plan.CtorParams.Select(p => Camel(p.Name)));
                        sb.AppendLine($"return new {fq}({args});");
                    }
                    break;

                case CtorStrategy.Mixed:
                    {
                        var args = string.Join(", ", plan.CtorParams.Select(p => Camel(p.Name)));
                        // Check if any setter member is init-only; init setters require object-initializer syntax.
                        bool anyInitOnly = plan.SetterMembers.Any(m =>
                        {
                            var prop = type.GetMembers(m.Name).OfType<IPropertySymbol>().FirstOrDefault();
                            return prop?.SetMethod?.IsInitOnly == true;
                        });
                        if (anyInitOnly)
                        {
                            var inits = string.Join(", ", plan.SetterMembers.Select(m => $"{m.Name} = {Camel(m.Name)}"));
                            sb.AppendLine($"return new {fq}({args}) {{ {inits} }};");
                        }
                        else
                        {
                            sb.AppendLine($"var __result = new {fq}({args});");
                            for (int i = 0; i < plan.SetterMembers.Length; i++)
                            {
                                var m = plan.SetterMembers[i];
                                sb.AppendLine($"__result.{m.Name} = {Camel(m.Name)};");
                            }
                            sb.AppendLine("return __result;");
                        }
                    }
                    break;

                case CtorStrategy.Impossible:
                    {
                        var safeReason = (plan.Reason ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
                        sb.AppendLine($"throw new global::XPacketRpc.RpcSerializationException(\"XPRPC003: cannot construct {type.Name}: {safeReason}\");");
                    }
                    break;
            }
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string ReadExpr(MemberDescriptor m)
    {
        switch (m.Kind)
        {
            case WireKind.Bool: return "(r.ReadByte() != 0)";
            case WireKind.Byte: return "r.ReadByte()";
            case WireKind.SByte: return "(sbyte)r.ReadByte()";
            case WireKind.Int16: return "r.ReadInt16()";
            case WireKind.UInt16: return "r.ReadUInt16()";
            case WireKind.Int32: return "r.ReadInt32()";
            case WireKind.UInt32: return "r.ReadUInt32()";
            case WireKind.Int64: return "r.ReadInt64()";
            case WireKind.UInt64: return "r.ReadUInt64()";
            case WireKind.Single: return "r.ReadSingle()";
            case WireKind.Double: return "r.ReadDouble()";
            case WireKind.Decimal: return "r.ReadDecimal()";
            case WireKind.String: return "r.ReadString()";
            case WireKind.Guid: return "r.ReadGuid()";
            case WireKind.DateTime: return "r.ReadDateTime()";
            case WireKind.DateTimeOffset: return "r.ReadDateTimeOffset()";
            case WireKind.TimeSpan: return "r.ReadTimeSpan()";
            case WireKind.ByteArray: return "r.ReadBytes()";
            case WireKind.Enum:
                {
                    var et = (INamedTypeSymbol)m.Type;
                    var u = et.EnumUnderlyingType!.SpecialType;
                    return $"({TypeName(m.Type)})r.{ReadEnumFn(u)}()";
                }
            case WireKind.NestedDto:
                return $"global::XPacketRpc.Generated.__XPRpcGen_{Sanitize(m.Type.Name)}.Read(ref r)";
            case WireKind.List:
                {
                    var elem = m.ElementOrInner!;
                    var elemTypeName = TypeName(elem);
                    var elemRead = ReadExprForType(elem);
                    return $"global::XPacketRpc.XPRpcReaderHelpers.ReadList<{elemTypeName}>(ref r, static (ref global::XPacketRpc.XPRpcReader r) => {elemRead})";
                }
            case WireKind.Array:
                {
                    var elem = m.ElementOrInner!;
                    var elemTypeName = TypeName(elem);
                    var elemRead = ReadExprForType(elem);
                    return $"global::XPacketRpc.XPRpcReaderHelpers.ReadArray<{elemTypeName}>(ref r, static (ref global::XPacketRpc.XPRpcReader r) => {elemRead})";
                }
            case WireKind.Dictionary:
                {
                    var k = m.DictKey!;
                    var v = m.DictValue!;
                    var kName = TypeName(k);
                    var vName = TypeName(v);
                    var kRead = ReadExprForType(k);
                    var vRead = ReadExprForType(v);
                    return $"global::XPacketRpc.XPRpcReaderHelpers.ReadDict<{kName}, {vName}>(ref r, " +
                           $"static (ref global::XPacketRpc.XPRpcReader r) => {kRead}, " +
                           $"static (ref global::XPacketRpc.XPRpcReader r) => {vRead})";
                }
            case WireKind.Nullable:
                {
                    var inner = m.ElementOrInner!;
                    return ReadExprForType(inner);
                }
            default:
                return $"default! /* unsupported {m.Type} */";
        }
    }

    private string ReadExprForType(ITypeSymbol t)
    {
        switch (t.SpecialType)
        {
            case SpecialType.System_Boolean: return "(r.ReadByte() != 0)";
            case SpecialType.System_Byte: return "r.ReadByte()";
            case SpecialType.System_SByte: return "(sbyte)r.ReadByte()";
            case SpecialType.System_Int16: return "r.ReadInt16()";
            case SpecialType.System_UInt16: return "r.ReadUInt16()";
            case SpecialType.System_Int32: return "r.ReadInt32()";
            case SpecialType.System_UInt32: return "r.ReadUInt32()";
            case SpecialType.System_Int64: return "r.ReadInt64()";
            case SpecialType.System_UInt64: return "r.ReadUInt64()";
            case SpecialType.System_Single: return "r.ReadSingle()";
            case SpecialType.System_Double: return "r.ReadDouble()";
            case SpecialType.System_Decimal: return "r.ReadDecimal()";
            case SpecialType.System_String: return "r.ReadString()";
            case SpecialType.System_DateTime: return "r.ReadDateTime()";
        }
        var fq = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fq == "global::System.Guid") return "r.ReadGuid()";
        if (fq == "global::System.DateTimeOffset") return "r.ReadDateTimeOffset()";
        if (fq == "global::System.TimeSpan") return "r.ReadTimeSpan()";
        if (t.TypeKind == TypeKind.Enum)
        {
            var u = ((INamedTypeSymbol)t).EnumUnderlyingType!.SpecialType;
            return $"({TypeName(t)})r.{ReadEnumFn(u)}()";
        }
        return $"global::XPacketRpc.Generated.__XPRpcGen_{Sanitize(t.Name)}.Read(ref r)";
    }

    private static string ReadEnumFn(SpecialType u)
    {
        switch (u)
        {
            case SpecialType.System_Byte:
            case SpecialType.System_SByte: return "ReadByte";
            case SpecialType.System_Int16: return "ReadInt16";
            case SpecialType.System_UInt16: return "ReadUInt16";
            case SpecialType.System_Int32: return "ReadInt32";
            case SpecialType.System_UInt32: return "ReadUInt32";
            case SpecialType.System_Int64: return "ReadInt64";
            case SpecialType.System_UInt64: return "ReadUInt64";
            default: return "ReadInt32";
        }
    }

    private static string TypeName(ITypeSymbol t) =>
        t.ToDisplayString(new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

    private static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    private static string Sanitize(string n)
    {
        var chars = new char[n.Length];
        for (int i = 0; i < n.Length; i++)
            chars[i] = char.IsLetterOrDigit(n[i]) ? n[i] : '_';
        return new string(chars);
    }
}
