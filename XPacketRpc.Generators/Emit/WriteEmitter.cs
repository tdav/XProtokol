using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using XPacketRpc.Generators.Discovery;

namespace XPacketRpc.Generators.Emit;

internal sealed class WriteEmitter
{
    private readonly TypeWalker walker;

    public WriteEmitter(TypeWalker walker) { this.walker = walker; }

    public string EmitWriteMethod(INamedTypeSymbol type)
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

        var sb = new IndentedStringBuilder();
        sb.AppendLine($"internal static void Write(global::{Fq(type)} value, global::System.Buffers.IBufferWriter<byte> w)");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            if (bitmapBytes > 0)
            {
                EmitBitmap(sb, sorted, nullableMembers, bitmapBytes);
            }

            for (int i = 0; i < sorted.Length; i++)
            {
                EmitField(sb, sorted[i], i, nullableMembers);
            }
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitBitmap(
        IndentedStringBuilder sb,
        MemberDescriptor[] sorted,
        (MemberDescriptor m, int i)[] nullableMembers,
        int bitmapBytes)
    {
        if (bitmapBytes == 1)
        {
            sb.AppendLine("byte bitmap = 0;");
            int bit = 0;
            foreach (var (m, _) in nullableMembers)
            {
                sb.AppendLine($"if (value.{m.Name} is null) bitmap |= 0b{System.Convert.ToString(1 << bit, 2).PadLeft(8, '0')};");
                bit++;
            }
            sb.AppendLine("var __span = w.GetSpan(1);");
            sb.AppendLine("__span[0] = bitmap;");
            sb.AppendLine("w.Advance(1);");
        }
        else
        {
            sb.AppendLine($"global::System.Span<byte> bitmap = stackalloc byte[{bitmapBytes}];");
            int idx = 0;
            foreach (var (m, _) in nullableMembers)
            {
                int byteIdx = idx / 8;
                int bit = idx % 8;
                sb.AppendLine($"if (value.{m.Name} is null) bitmap[{byteIdx}] |= 0b{System.Convert.ToString(1 << bit, 2).PadLeft(8, '0')};");
                idx++;
            }
            sb.AppendLine($"var __span = w.GetSpan({bitmapBytes});");
            sb.AppendLine("bitmap.CopyTo(__span);");
            sb.AppendLine($"w.Advance({bitmapBytes});");
        }
    }

    private void EmitField(
        IndentedStringBuilder sb,
        MemberDescriptor m,
        int sortedIndex,
        (MemberDescriptor m, int i)[] nullableMembers)
    {
        if (m.IsNullable)
        {
            sb.AppendLine($"if (value.{m.Name} is not null)");
            sb.AppendLine("{");
            using (sb.Indent())
            {
                EmitFieldValue(sb, m, accessExpr: $"value.{m.Name}", forceNonNull: true);
            }
            sb.AppendLine("}");
        }
        else if (NeedsNullCheck(m))
        {
            sb.AppendLine($"if (value.{m.Name} is null) global::XPacketRpc.Internal.Writers.ThrowNullRequired(\"{m.Name}\");");
            EmitFieldValue(sb, m, accessExpr: $"value.{m.Name}", forceNonNull: true);
        }
        else
        {
            EmitFieldValue(sb, m, accessExpr: $"value.{m.Name}", forceNonNull: false);
        }
    }

    private static bool NeedsNullCheck(MemberDescriptor m)
    {
        if (m.Type.IsValueType) return false;
        return true;
    }

    private void EmitFieldValue(IndentedStringBuilder sb, MemberDescriptor m, string accessExpr, bool forceNonNull)
    {
        var w = "global::XPacketRpc.Internal.Writers";
        switch (m.Kind)
        {
            case WireKind.Bool:
                sb.AppendLine($"{w}.WriteByte((byte)({accessExpr} ? 1 : 0), w);");
                break;
            case WireKind.SByte:
                sb.AppendLine($"{w}.WriteByte((byte){accessExpr}, w);");
                break;
            case WireKind.Byte:
                sb.AppendLine($"{w}.WriteByte({accessExpr}, w);");
                break;
            case WireKind.Int16: sb.AppendLine($"{w}.WriteInt16LE({accessExpr}, w);"); break;
            case WireKind.UInt16: sb.AppendLine($"{w}.WriteUInt16LE({accessExpr}, w);"); break;
            case WireKind.Int32: sb.AppendLine($"{w}.WriteInt32LE({accessExpr}, w);"); break;
            case WireKind.UInt32: sb.AppendLine($"{w}.WriteUInt32LE({accessExpr}, w);"); break;
            case WireKind.Int64: sb.AppendLine($"{w}.WriteInt64LE({accessExpr}, w);"); break;
            case WireKind.UInt64: sb.AppendLine($"{w}.WriteUInt64LE({accessExpr}, w);"); break;
            case WireKind.Single: sb.AppendLine($"{w}.WriteSingleLE({accessExpr}, w);"); break;
            case WireKind.Double: sb.AppendLine($"{w}.WriteDoubleLE({accessExpr}, w);"); break;
            case WireKind.Decimal: sb.AppendLine($"{w}.WriteDecimalLE({accessExpr}, w);"); break;
            case WireKind.String: sb.AppendLine($"{w}.WriteString({accessExpr}, w);"); break;
            case WireKind.Guid: sb.AppendLine($"{w}.WriteGuid({accessExpr}, w);"); break;
            case WireKind.DateTime: sb.AppendLine($"{w}.WriteDateTime({accessExpr}, w);"); break;
            case WireKind.DateTimeOffset: sb.AppendLine($"{w}.WriteDateTimeOffset({accessExpr}, w);"); break;
            case WireKind.TimeSpan: sb.AppendLine($"{w}.WriteTimeSpan({accessExpr}, w);"); break;
            case WireKind.ByteArray: sb.AppendLine($"{w}.WriteBytes({accessExpr}, w);"); break;

            case WireKind.Enum:
                {
                    var enumType = (INamedTypeSymbol)m.Type;
                    var underlying = enumType.EnumUnderlyingType!.SpecialType;
                    var (cast, writeFn) = MapEnumUnderlying(underlying);
                    sb.AppendLine($"{w}.{writeFn}(({cast}){accessExpr}, w);");
                }
                break;

            case WireKind.Nullable:
                {
                    var inner = m.ElementOrInner!;
                    EmitInlineWriteForType(sb, inner, $"{accessExpr}.Value");
                }
                break;

            case WireKind.Array:
                EmitCollectionWrite(sb, m.ElementOrInner!, accessExpr, "Length", "[i]");
                break;

            case WireKind.List:
                EmitCollectionWrite(sb, m.ElementOrInner!, accessExpr, "Count", "[i]");
                break;

            case WireKind.Dictionary:
                EmitDictionaryWrite(sb, m.DictKey!, m.DictValue!, accessExpr);
                break;

            case WireKind.NestedDto:
                {
                    var t = (INamedTypeSymbol)m.Type;
                    sb.AppendLine($"global::XPacketRpc.Generated.__XPRpcGen_{Sanitize(t.Name)}.Write({accessExpr}, w);");
                }
                break;

            default:
                sb.AppendLine($"// XPRPC004: unsupported type {m.Type.ToDisplayString()}");
                break;
        }
    }

    private void EmitInlineWriteForType(IndentedStringBuilder sb, ITypeSymbol type, string accessExpr)
    {
        var (kind, inner, dk, dv) = ClassifyForInline(type);
        var fake = new MemberDescriptor(
            Name: "_inline",
            Type: type,
            IsField: false,
            IsNullable: false,
            Kind: kind,
            ElementOrInner: inner,
            DictKey: dk,
            DictValue: dv);
        EmitFieldValue(sb, fake, accessExpr, forceNonNull: true);
    }

    private void EmitCollectionWrite(IndentedStringBuilder sb, ITypeSymbol elemType, string access, string countProp, string indexerSuffix)
    {
        var w = "global::XPacketRpc.Internal.Writers";
        sb.AppendLine($"{w}.WriteVarUInt32((uint){access}.{countProp}, w);");
        sb.AppendLine($"for (int i = 0; i < {access}.{countProp}; i++)");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            EmitInlineWriteForType(sb, elemType, $"{access}{indexerSuffix}");
        }
        sb.AppendLine("}");
    }

    private void EmitDictionaryWrite(IndentedStringBuilder sb, ITypeSymbol keyType, ITypeSymbol valType, string access)
    {
        var w = "global::XPacketRpc.Internal.Writers";
        sb.AppendLine($"{w}.WriteVarUInt32((uint){access}.Count, w);");
        sb.AppendLine($"foreach (var __kv in {access})");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            EmitInlineWriteForType(sb, keyType, "__kv.Key");
            EmitInlineWriteForType(sb, valType, "__kv.Value");
        }
        sb.AppendLine("}");
    }

    private (WireKind, ITypeSymbol?, ITypeSymbol?, ITypeSymbol?) ClassifyForInline(ITypeSymbol t)
    {
        if (t is IArrayTypeSymbol arr && arr.ElementType.SpecialType == SpecialType.System_Byte)
            return (WireKind.ByteArray, null, null, null);
        if (t is IArrayTypeSymbol arr2)
            return (WireKind.Array, arr2.ElementType, null, null);
        switch (t.SpecialType)
        {
            case SpecialType.System_Boolean: return (WireKind.Bool, null, null, null);
            case SpecialType.System_Byte: return (WireKind.Byte, null, null, null);
            case SpecialType.System_SByte: return (WireKind.SByte, null, null, null);
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
        var fq = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fq == "global::System.Guid") return (WireKind.Guid, null, null, null);
        if (fq == "global::System.DateTimeOffset") return (WireKind.DateTimeOffset, null, null, null);
        if (fq == "global::System.TimeSpan") return (WireKind.TimeSpan, null, null, null);
        if (t.TypeKind == TypeKind.Enum) return (WireKind.Enum, null, null, null);

        if (t is INamedTypeSymbol nts && nts.IsGenericType)
        {
            var open = nts.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (open == "global::System.Collections.Generic.List<T>")
                return (WireKind.List, nts.TypeArguments[0], null, null);
            if (open == "global::System.Collections.Generic.Dictionary<TKey, TValue>")
                return (WireKind.Dictionary, null, nts.TypeArguments[0], nts.TypeArguments[1]);
            if (open == "global::System.Nullable<T>")
                return (WireKind.Nullable, nts.TypeArguments[0], null, null);
        }
        return (WireKind.NestedDto, null, null, null);
    }

    private static (string cast, string fn) MapEnumUnderlying(SpecialType u)
    {
        switch (u)
        {
            case SpecialType.System_Byte: return ("byte", "WriteByte");
            case SpecialType.System_SByte: return ("byte", "WriteByte");
            case SpecialType.System_Int16: return ("short", "WriteInt16LE");
            case SpecialType.System_UInt16: return ("ushort", "WriteUInt16LE");
            case SpecialType.System_Int32: return ("int", "WriteInt32LE");
            case SpecialType.System_UInt32: return ("uint", "WriteUInt32LE");
            case SpecialType.System_Int64: return ("long", "WriteInt64LE");
            case SpecialType.System_UInt64: return ("ulong", "WriteUInt64LE");
            default: return ("int", "WriteInt32LE");
        }
    }

    private static string Fq(INamedTypeSymbol t) =>
        t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");

    private static string Sanitize(string name)
    {
        var chars = new char[name.Length];
        for (int i = 0; i < name.Length; i++)
            chars[i] = char.IsLetterOrDigit(name[i]) ? name[i] : '_';
        return new string(chars);
    }
}

internal static class Fnv1aGen
{
    public static uint Hash(string s)
    {
        uint h = 2166136261u;
        for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
        return h;
    }
}
