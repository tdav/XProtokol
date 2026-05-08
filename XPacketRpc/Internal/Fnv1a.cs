namespace XPacketRpc.Internal;

/// <summary>
/// Canonical FNV-1a 32-bit hash. The IDENTICAL implementation is duplicated in
/// XPacketRpc.Generators (the generator cannot reference the runtime assembly).
/// When you change this, synchronize both copies and update Fnv1aTests.
/// </summary>
public static class Fnv1a
{
    private const uint OffsetBasis = 2166136261u;
    private const uint Prime = 16777619u;

    public static uint Hash(string s)
    {
        uint h = OffsetBasis;
        for (int i = 0; i < s.Length; i++)
        {
            h ^= s[i];
            h *= Prime;
        }
        return h;
    }
}
