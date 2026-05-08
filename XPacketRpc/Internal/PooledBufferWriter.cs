using System.Buffers;

namespace XPacketRpc.Internal;

public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
{
    private readonly ArrayPool<byte> pool;
    private byte[] buffer;
    private int written;

    public PooledBufferWriter(ArrayPool<byte> pool, int initialCapacity = 256)
    {
        if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        this.pool = pool;
        this.buffer = pool.Rent(initialCapacity);
        this.written = 0;
    }

    public int WrittenCount => this.written;
    public ReadOnlySpan<byte> WrittenSpan => this.buffer.AsSpan(0, this.written);
    public ReadOnlyMemory<byte> WrittenMemory => this.buffer.AsMemory(0, this.written);

    public void Advance(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (this.written + count > this.buffer.Length)
            throw new InvalidOperationException("Cannot advance past the end of the buffer.");
        this.written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return this.buffer.AsMemory(this.written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return this.buffer.AsSpan(this.written);
    }

    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
        if (sizeHint == 0) sizeHint = 1;

        int available = this.buffer.Length - this.written;
        if (available >= sizeHint) return;

        int requested = checked(this.written + sizeHint);
        int newSize = Math.Max(this.buffer.Length * 2, requested);
        var next = this.pool.Rent(newSize);
        Buffer.BlockCopy(this.buffer, 0, next, 0, this.written);
        this.pool.Return(this.buffer);
        this.buffer = next;
    }

    public void Dispose()
    {
        if (this.buffer.Length > 0)
        {
            this.pool.Return(this.buffer);
            this.buffer = Array.Empty<byte>();
        }
    }
}
