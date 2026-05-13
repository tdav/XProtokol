using System;
using XPacketRpc;
using XProtocol;
using XProtocol.Serializator;

namespace Test
{
    internal class TestPacket
    {
        public int TestNumber;
        public double TestDouble;
        public bool TestBoolean;
        public string TestString;
    }

    internal class Program
    {
        private static void Main()
        {

            Console.Title = "";
            Console.ForegroundColor = ConsoleColor.White;

            XPacketTypeManager.Register<TestPacket>(XPacketType.GetOrderAllMethod, /* type */ 1, /* subtype */ 1);

            var dto = new TestPacket
            {
                TestNumber = 12345,
                TestDouble = 3.14,
                TestBoolean = true,
                TestString = "Hello, World!hffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff Hello, World!hffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffHello, World!hffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffHello, World!hffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffHello, World!hffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffHello, World!hffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffHello, World!hffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffHello, World!hffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffHello, World!hffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff111"
            };

            var packet = XPacketConverter.Serialize(dto);
            var encrypted = packet.Encrypt().ToPacket();
            var decrypted = XPacket.Parse(encrypted);
            var roundtrip = XPacketConverter.Deserialize<TestPacket>(decrypted);

            Console.WriteLine(
                $"TestNumber={roundtrip.TestNumber}, TestDouble={roundtrip.TestDouble}, " +
                $"TestBoolean={roundtrip.TestBoolean}, TestString.Length={roundtrip.TestString?.Length ?? 0}");
            Console.WriteLine($"TestString matches: {dto.TestString == roundtrip.TestString}");

            Console.ReadLine();
        }
    }
}
