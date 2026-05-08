namespace XProtocol.Tests
{
    public class SimpleDto
    {
        public int A;
        public double B;
        public bool C;
    }

    public class EmptyDto
    {
    }

    public class BadDtoWithReferenceField
    {
        public int A;
        public string Bad;
    }

    public class UnregisteredDto
    {
        public int X;
    }

    public static class AssemblyFixture
    {
        public const XPacketType SimpleDtoType = (XPacketType)100;
        public const XPacketType EmptyDtoType = (XPacketType)101;

        [Before(HookType.Assembly)]
        public static void Init()
        {
            XPacketTypeManager.Register<SimpleDto>(SimpleDtoType, 100, 0);
            XPacketTypeManager.Register<EmptyDto>(EmptyDtoType, 101, 0);
        }
    }
}
