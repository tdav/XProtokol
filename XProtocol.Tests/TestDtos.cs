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
        public System.Collections.Generic.HashSet<int> Bad;
    }

    public class StringDto
    {
        public int A;
        public string S;
        public bool B;
    }

    public class MultiStringDto
    {
        public string First;
        public int Middle;
        public string Last;
    }

    public class UnsupportedRefDto
    {
        public int A;
        public object Bad;
    }

    public class UnregisteredDto
    {
        public int X;
    }

    public class RegistrationOnlyStringDto
    {
        public string X;
    }

    public class IntArrayDto
    {
        public int A;
        public int[] Values;
    }

    public class ByteArrayDto
    {
        public byte[] Payload;
    }

    public class StringArrayDto
    {
        public string[] Tags;
    }

    public class IntListDto
    {
        public System.Collections.Generic.List<int> Numbers;
    }

    public class StringListDto
    {
        public string Header;
        public System.Collections.Generic.List<string> Items;
    }

    public class IntStringDictDto
    {
        public System.Collections.Generic.Dictionary<int, string> Map;
    }

    public class StringIntDictDto
    {
        public System.Collections.Generic.Dictionary<string, int> Map;
    }

    public static class AssemblyFixture
    {
        public const XPacketType SimpleDtoType = (XPacketType)100;
        public const XPacketType EmptyDtoType = (XPacketType)101;

        [Before(HookType.Assembly)]
        public static void Init()
        {
            XPacketTypeManager.Register<SimpleDto>(SimpleDtoType, 100, 0);
            XPacketTypeManager.Register<StringDto>((XPacketType)200, 200, 0);
            XPacketTypeManager.Register<MultiStringDto>((XPacketType)201, 201, 0);
            XPacketTypeManager.Register<IntArrayDto>((XPacketType)300, 44, 0);
            XPacketTypeManager.Register<ByteArrayDto>((XPacketType)301, 45, 0);
            XPacketTypeManager.Register<StringArrayDto>((XPacketType)302, 46, 0);
            XPacketTypeManager.Register<IntListDto>((XPacketType)50, 50, 0);
            XPacketTypeManager.Register<StringListDto>((XPacketType)51, 51, 0);
            XPacketTypeManager.Register<IntStringDictDto>((XPacketType)60, 60, 0);
            XPacketTypeManager.Register<StringIntDictDto>((XPacketType)61, 61, 0);
        }
    }
}
