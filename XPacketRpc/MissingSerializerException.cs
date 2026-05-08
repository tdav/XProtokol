namespace XPacketRpc;

public sealed class MissingSerializerException : Exception
{
    public Type MissingType { get; }

    public MissingSerializerException(Type missingType)
        : base(BuildMessage(missingType))
    {
        this.MissingType = missingType;
    }

    private static string BuildMessage(Type t) =>
        $"No generated serializer for type '{t.FullName}'. " +
        $"Add a closed-generic call-site (e.g. XPRpc.Touch<{t.Name}>()) so the source generator can emit code.";
}
