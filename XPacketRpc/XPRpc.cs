using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace XPacketRpc;

public static class XPRpc
{
    public delegate void WriteDelegate<T>(T value, IBufferWriter<byte> writer);
    public delegate T ReadDelegate<T>(ref XPRpcReader reader);

    // Reflection / dynamic dispatch fallback. Hot path uses Cache<T>.
    private static readonly ConcurrentDictionary<Type, object> writers = new();
    private static readonly ConcurrentDictionary<Type, object> readers = new();

    // Per-T static cache. Eliminates dictionary lookup + cast on hot path.
    // JIT specializes Cache<T> per closed generic, so reads are a direct field load.
    private static class Cache<T>
    {
        public static WriteDelegate<T>? Writer;
        public static ReadDelegate<T>? Reader;
    }

    /// <summary>
    /// No-op. Exists only so the source generator sees T in call-site analysis
    /// and emits code. Call in startup for types resolved via MakeGenericMethod.
    /// </summary>
    public static void Touch<T>() { /* no-op */ }

    /// <summary>
    /// Public for use by generated module-initializers in arbitrary consumer assemblies.
    /// Do not call directly — generator does it automatically.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register<T>(WriteDelegate<T> write, ReadDelegate<T> read)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(read);
        Cache<T>.Writer = write;
        Cache<T>.Reader = read;
        writers[typeof(T)] = write;
        readers[typeof(T)] = read;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(T value, IBufferWriter<byte> writer)
    {
        var w = Cache<T>.Writer;
        if (w is null) ThrowMissing(typeof(T));
        w(value, writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? Read<T>(ReadOnlySpan<byte> source)
    {
        var r = Cache<T>.Reader;
        if (r is null) ThrowMissing(typeof(T));
        var reader = new XPRpcReader(source);
        return r(ref reader);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowMissing(Type t) => throw new MissingSerializerException(t);
}
