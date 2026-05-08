using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace XPacketRpc;

public ref struct XPRpcReader
{
    private readonly ReadOnlySpan<byte> source;
    private int position;

    public XPRpcReader(ReadOnlySpan<byte> source)
    {
        this.source = source;
        this.position = 0;
    }

    public int Position => this.position;
    public int Remaining => this.source.Length - this.position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte()
    {
        EnsureAvailable(1);
        return this.source[this.position++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadInt16()
    {
        EnsureAvailable(2);
        var v = BinaryPrimitives.ReadInt16LittleEndian(this.source.Slice(this.position));
        this.position += 2;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUInt16()
    {
        EnsureAvailable(2);
        var v = BinaryPrimitives.ReadUInt16LittleEndian(this.source.Slice(this.position));
        this.position += 2;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        EnsureAvailable(4);
        var v = BinaryPrimitives.ReadInt32LittleEndian(this.source.Slice(this.position));
        this.position += 4;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        EnsureAvailable(4);
        var v = BinaryPrimitives.ReadUInt32LittleEndian(this.source.Slice(this.position));
        this.position += 4;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64()
    {
        EnsureAvailable(8);
        var v = BinaryPrimitives.ReadInt64LittleEndian(this.source.Slice(this.position));
        this.position += 8;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUInt64()
    {
        EnsureAvailable(8);
        var v = BinaryPrimitives.ReadUInt64LittleEndian(this.source.Slice(this.position));
        this.position += 8;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());

    public uint ReadVarUInt32()
    {
        uint result = 0;
        int shift = 0;
        while (true)
        {
            if (shift >= 35)
                throw new RpcSerializationException("VarUInt32 is overlong (more than 5 bytes).");

            byte b = ReadByte();
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }
    }

    public string ReadString()
    {
        uint length = ReadVarUInt32();
        if (length == 0) return string.Empty;
        EnsureAvailable((int)length);
        var slice = this.source.Slice(this.position, (int)length);
        this.position += (int)length;
        return System.Text.Encoding.UTF8.GetString(slice);
    }

    public byte[] ReadBytes()
    {
        uint length = ReadVarUInt32();
        if (length == 0) return Array.Empty<byte>();
        EnsureAvailable((int)length);
        var arr = this.source.Slice(this.position, (int)length).ToArray();
        this.position += (int)length;
        return arr;
    }

    public Guid ReadGuid()
    {
        EnsureAvailable(16);
        var slice = this.source.Slice(this.position, 16);
        this.position += 16;
        return new Guid(slice, bigEndian: false);
    }

    public DateTime ReadDateTime()
    {
        EnsureAvailable(9);
        long ticks = BinaryPrimitives.ReadInt64LittleEndian(this.source.Slice(this.position));
        byte kind = this.source[this.position + 8];
        this.position += 9;
        return new DateTime(ticks, (DateTimeKind)kind);
    }

    public DateTimeOffset ReadDateTimeOffset()
    {
        EnsureAvailable(10);
        long ticks = BinaryPrimitives.ReadInt64LittleEndian(this.source.Slice(this.position));
        short minutes = BinaryPrimitives.ReadInt16LittleEndian(this.source.Slice(this.position + 8));
        this.position += 10;
        return new DateTimeOffset(ticks, TimeSpan.FromMinutes(minutes));
    }

    public TimeSpan ReadTimeSpan() => new(ReadInt64());

    public decimal ReadDecimal()
    {
        EnsureAvailable(16);
        var s = this.source.Slice(this.position, 16);
        this.position += 16;
        Span<int> bits = stackalloc int[4];
        bits[0] = BinaryPrimitives.ReadInt32LittleEndian(s);
        bits[1] = BinaryPrimitives.ReadInt32LittleEndian(s.Slice(4));
        bits[2] = BinaryPrimitives.ReadInt32LittleEndian(s.Slice(8));
        bits[3] = BinaryPrimitives.ReadInt32LittleEndian(s.Slice(12));
        return new decimal(bits);
    }

    private void EnsureAvailable(int count)
    {
        if (this.position + count > this.source.Length)
            throw new RpcSerializationException(
                $"Unexpected end of payload (need {count} bytes at position {this.position}, " +
                $"only {this.source.Length - this.position} remaining).");
    }
}

public delegate T ReadElemDelegate<T>(ref XPRpcReader r);

public static class XPRpcReaderHelpers
{
    public static System.Collections.Generic.List<T> ReadList<T>(ref XPRpcReader r, ReadElemDelegate<T> read)
    {
        uint n = r.ReadVarUInt32();
        var list = new System.Collections.Generic.List<T>((int)n);
        for (uint i = 0; i < n; i++) list.Add(read(ref r));
        return list;
    }

    public static T[] ReadArray<T>(ref XPRpcReader r, ReadElemDelegate<T> read)
    {
        uint n = r.ReadVarUInt32();
        var arr = new T[n];
        for (uint i = 0; i < n; i++) arr[i] = read(ref r);
        return arr;
    }

    public static System.Collections.Generic.Dictionary<K, V> ReadDict<K, V>(
        ref XPRpcReader r, ReadElemDelegate<K> readK, ReadElemDelegate<V> readV) where K : notnull
    {
        uint n = r.ReadVarUInt32();
        var d = new System.Collections.Generic.Dictionary<K, V>((int)n);
        for (uint i = 0; i < n; i++)
        {
            var k = readK(ref r);
            var v = readV(ref r);
            d[k] = v;
        }
        return d;
    }
}
