using System;

namespace XProtocol.Serializator
{
    public static class XPacketConverter
    {
        public static XPacket Serialize<T>(T obj) where T : class
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            var (btype, bsubtype) = XPacketTypeManager.GetBytesFor(typeof(T));
            var descriptors = XPacketTypeManager.GetDescriptors(typeof(T));

            var packet = XPacket.Create(btype, bsubtype);

            foreach (var desc in descriptors)
            {
                var value = desc.Getter(obj);
                packet.AppendValue(value);
            }

            return packet;
        }

        public static T Deserialize<T>(XPacket packet) where T : class, new()
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            var descriptors = XPacketTypeManager.GetDescriptors(typeof(T));

            if (packet.Fields.Count != descriptors.Length)
            {
                throw new InvalidOperationException(
                    $"Field count mismatch for {typeof(T).Name}: expected {descriptors.Length}, got {packet.Fields.Count}.");
            }

            var instance = new T();

            for (int i = 0; i < descriptors.Length; i++)
            {
                var desc = descriptors[i];
                var raw = packet.GetValueAt(i, desc.Field.FieldType);
                desc.Setter(instance, raw);
            }

            return instance;
        }
    }
}
