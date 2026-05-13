using System;
using XProtocol;
using XProtocol.Serializator;

namespace Test
{
    internal class TestPacket
    {
        public int TestNumber;
        public double TestDouble;
        public bool TestBoolean;
    }

    internal class Program
    {
        private static void Main()
        {

            Console.Title = "";
            Console.ForegroundColor = ConsoleColor.White;

            var dto = new TestPacket
            {
                TestNumber = 12345,
                TestDouble = 3.14,
                TestBoolean = true
            };

            var packet = XPacketConverter.Serialize(dto);
            var encrypted = packet.Encrypt().ToPacket();
            var decrypted = XPacket.Parse(encrypted);
            var roundtrip = XPacketConverter.Deserialize<TestPacket>(decrypted);

            Console.WriteLine($"TestNumber={roundtrip.TestNumber}, TestDouble={roundtrip.TestDouble}, TestBoolean={roundtrip.TestBoolean}");

            Console.ReadLine();
        }
    }
}
