using System;
using System.IO;
using System.Text;

namespace XProtocol.Serializator
{
    internal static class ShapeCodec
    {
        public static byte[] WriteField(FieldShape shape, object value)
        {
            using var ms = new MemoryStream();
            WriteFieldInto(ms, shape, value);
            return ms.ToArray();
        }

        public static object ReadField(FieldShape shape, ChunkReader reader)
        {
            switch (shape)
            {
                case ValueShape v:
                    return ReadValue(v, reader);
                case StringShape:
                    return ReadString(reader);
                case ArrayShape a:
                    return ReadArray(a, reader);
                case ListShape l:
                    return ReadList(l, reader);
                case DictShape d:
                    return ReadDict(d, reader);
                default:
                    throw new InvalidOperationException($"Unsupported shape: {shape.GetType().Name}");
            }
        }

        private static void WriteFieldInto(MemoryStream ms, FieldShape shape, object value)
        {
            switch (shape)
            {
                case ValueShape v:
                    WriteValue(ms, v, value);
                    break;
                case StringShape:
                    WriteString(ms, value);
                    break;
                case ArrayShape a:
                    WriteArray(ms, a, value);
                    break;
                case ListShape l:
                    WriteList(ms, l, value);
                    break;
                case DictShape d:
                    WriteDict(ms, d, value);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported shape: {shape.GetType().Name}");
            }
        }

        private static void WriteValue(MemoryStream ms, ValueShape shape, object value)
        {
            var bytes = MarshalHelpers.ToBytes(value, shape.ClrType);
            ms.Write(bytes, 0, bytes.Length);
        }

        private static object ReadValue(ValueShape shape, ChunkReader reader)
        {
            var size = System.Runtime.InteropServices.Marshal.SizeOf(shape.ClrType);
            var buf = new byte[size];
            reader.ReadBytes(buf, 0, size);
            return MarshalHelpers.FromBytes(buf, shape.ClrType);
        }

        private static void WriteString(MemoryStream ms, object value)
        {
            var s = (string)value ?? string.Empty;
            var utf8 = Encoding.UTF8.GetBytes(s);
            if (utf8.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"string exceeds {ushort.MaxValue} UTF-8 bytes (actual: {utf8.Length}).");
            }
            WriteUInt16LE(ms, (ushort)utf8.Length);
            ms.Write(utf8, 0, utf8.Length);
        }

        private static string ReadString(ChunkReader reader)
        {
            // The 2-byte length prefix must live within the first chunk of the string field.
            // If the current chunk has fewer than 2 bytes remaining, the header is truncated.
            if (reader.CurrentChunkRemaining < 2)
            {
                throw new InvalidOperationException(
                    $"string header truncated (first chunk size {reader.CurrentChunkRemaining} < 2).");
            }
            int len = reader.ReadUInt16LE();
            var buf = new byte[len];
            if (len > 0)
            {
                if (len > reader.Available)
                {
                    throw new InvalidOperationException(
                        $"string truncated (need {len} bytes, have {reader.Available} remaining).");
                }
                reader.ReadBytes(buf, 0, len);
            }
            return Encoding.UTF8.GetString(buf);
        }

        private static void WriteUInt16LE(MemoryStream ms, ushort v)
        {
            ms.WriteByte((byte)(v & 0xFF));
            ms.WriteByte((byte)((v >> 8) & 0xFF));
        }

        private static void WriteArray(MemoryStream ms, ArrayShape shape, object value)
        {
            var arr = (Array)value ?? Array.CreateInstance(shape.ElementClrType, 0);
            if (arr.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"collection exceeds {ushort.MaxValue} elements (actual: {arr.Length}).");
            }
            WriteUInt16LE(ms, (ushort)arr.Length);

            if (shape.Element is ValueShape vs && vs.ClrType == typeof(byte))
            {
                var src = (byte[])arr;
                ms.Write(src, 0, src.Length);
                return;
            }

            for (int i = 0; i < arr.Length; i++)
            {
                WriteFieldInto(ms, shape.Element, arr.GetValue(i));
            }
        }

        private static object ReadArray(ArrayShape shape, ChunkReader reader)
        {
            int count = reader.ReadUInt16LE();
            var arr = Array.CreateInstance(shape.ElementClrType, count);

            if (shape.Element is ValueShape vs && vs.ClrType == typeof(byte))
            {
                if (count > 0)
                {
                    reader.ReadBytes((byte[])arr, 0, count);
                }
                return arr;
            }

            for (int i = 0; i < count; i++)
            {
                arr.SetValue(ReadField(shape.Element, reader), i);
            }
            return arr;
        }

        private static void WriteList(MemoryStream ms, ListShape shape, object value)
        {
            var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(shape.ElementClrType);
            var list = (System.Collections.IList)(value ?? Activator.CreateInstance(listType));
            if (list.Count > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"collection exceeds {ushort.MaxValue} elements (actual: {list.Count}).");
            }
            WriteUInt16LE(ms, (ushort)list.Count);

            foreach (var item in list)
            {
                WriteFieldInto(ms, shape.Element, item);
            }
        }

        private static object ReadList(ListShape shape, ChunkReader reader)
        {
            int count = reader.ReadUInt16LE();
            var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(shape.ElementClrType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType);
            for (int i = 0; i < count; i++)
            {
                list.Add(ReadField(shape.Element, reader));
            }
            return list;
        }

        private static void WriteDict(MemoryStream ms, DictShape shape, object value)
        {
            var dictType = typeof(System.Collections.Generic.Dictionary<,>).MakeGenericType(shape.KeyClrType, shape.ValueClrType);
            var dict = (System.Collections.IDictionary)(value ?? Activator.CreateInstance(dictType));
            if (dict.Count > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"collection exceeds {ushort.MaxValue} elements (actual: {dict.Count}).");
            }
            WriteUInt16LE(ms, (ushort)dict.Count);

            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                WriteFieldInto(ms, shape.Key, entry.Key);
                WriteFieldInto(ms, shape.Value, entry.Value);
            }
        }

        private static object ReadDict(DictShape shape, ChunkReader reader)
        {
            int count = reader.ReadUInt16LE();
            var dictType = typeof(System.Collections.Generic.Dictionary<,>).MakeGenericType(shape.KeyClrType, shape.ValueClrType);
            var dict = (System.Collections.IDictionary)Activator.CreateInstance(dictType);
            for (int i = 0; i < count; i++)
            {
                var key = ReadField(shape.Key, reader);
                var val = ReadField(shape.Value, reader);
                dict.Add(key, val);
            }
            return dict;
        }
    }
}
