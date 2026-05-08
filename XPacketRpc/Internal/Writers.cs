using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace XPacketRpc.Internal;

public static class Writers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(byte value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(1);
        span[0] = value;
        w.Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt16LE(short value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(2);
        BinaryPrimitives.WriteInt16LittleEndian(span, value);
        w.Advance(2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt16LE(ushort value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(2);
        BinaryPrimitives.WriteUInt16LittleEndian(span, value);
        w.Advance(2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt32LE(int value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(4);
        BinaryPrimitives.WriteInt32LittleEndian(span, value);
        w.Advance(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt32LE(uint value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(4);
        BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        w.Advance(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt64LE(long value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(8);
        BinaryPrimitives.WriteInt64LittleEndian(span, value);
        w.Advance(8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt64LE(ulong value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(8);
        BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        w.Advance(8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteSingleLE(float value, IBufferWriter<byte> w)
    {
        WriteInt32LE(BitConverter.SingleToInt32Bits(value), w);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteDoubleLE(double value, IBufferWriter<byte> w)
    {
        WriteInt64LE(BitConverter.DoubleToInt64Bits(value), w);
    }

    public static void WriteVarUInt32(uint value, IBufferWriter<byte> w)
    {
        // LEB128 unsigned. Up to 5 bytes.
        var span = w.GetSpan(5);
        int i = 0;
        while (value >= 0x80)
        {
            span[i++] = (byte)(value | 0x80);
            value >>= 7;
        }
        span[i++] = (byte)value;
        w.Advance(i);
    }

    public static void WriteString(string value, IBufferWriter<byte> w)
    {
        // Single-pass UTF-8 encoding with backpatched varint length prefix.
        // Saves the GetByteCount scan (one of two UTF-8 string traversals).
        if (string.IsNullOrEmpty(value))
        {
            WriteByte(0, w);
            return;
        }

        var encoding = System.Text.Encoding.UTF8;
        int maxBytes = encoding.GetMaxByteCount(value.Length);
        // Reserve space for max varint (5 bytes) + worst-case UTF-8 payload.
        var span = w.GetSpan(5 + maxBytes);
        int actual = encoding.GetBytes(value, span.Slice(5));

        int varSize = EncodeVarUInt32Inline(span, (uint)actual);
        if (varSize != 5 && actual > 0)
        {
            // Shift payload left into position immediately after the varint.
            // Span<T>.CopyTo handles overlapping forward copy safely.
            span.Slice(5, actual).CopyTo(span.Slice(varSize));
        }
        w.Advance(varSize + actual);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EncodeVarUInt32Inline(Span<byte> span, uint value)
    {
        int i = 0;
        while (value >= 0x80)
        {
            span[i++] = (byte)(value | 0x80);
            value >>= 7;
        }
        span[i++] = (byte)value;
        return i;
    }

    public static void WriteBytes(byte[] value, IBufferWriter<byte> w)
    {
        WriteVarUInt32((uint)value.Length, w);
        if (value.Length == 0) return;
        var span = w.GetSpan(value.Length);
        value.CopyTo(span);
        w.Advance(value.Length);
    }

    public static void WriteGuid(Guid value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(16);
        if (!value.TryWriteBytes(span, bigEndian: false, out _))
            throw new RpcSerializationException("Guid.TryWriteBytes failed (unexpected).");
        w.Advance(16);
    }

    public static void WriteDateTime(DateTime value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(9);
        BinaryPrimitives.WriteInt64LittleEndian(span, value.Ticks);
        span[8] = (byte)value.Kind;
        w.Advance(9);
    }

    public static void WriteDateTimeOffset(DateTimeOffset value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(10);
        BinaryPrimitives.WriteInt64LittleEndian(span, value.Ticks);
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(8), (short)value.Offset.TotalMinutes);
        w.Advance(10);
    }

    public static void WriteTimeSpan(TimeSpan value, IBufferWriter<byte> w)
    {
        WriteInt64LE(value.Ticks, w);
    }

    public static void WriteDecimalLE(decimal value, IBufferWriter<byte> w)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        var span = w.GetSpan(16);
        BinaryPrimitives.WriteInt32LittleEndian(span, bits[0]);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4), bits[1]);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8), bits[2]);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(12), bits[3]);
        w.Advance(16);
    }

    [DoesNotReturn]
    public static void ThrowNullRequired(string fieldName)
        => throw new RpcSerializationException($"Field '{fieldName}' is non-nullable but value was null.");
}
