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
    }
}
