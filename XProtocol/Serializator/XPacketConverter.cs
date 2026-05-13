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
                byte[] bytes;
                try
                {
                    bytes = ShapeCodec.WriteField(desc.Shape, desc.Getter(obj));
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException(
                        $"{typeof(T).Name}.{desc.Field.Name}: {ex.Message}", ex);
                }
                packet.AppendChunks(bytes);
            }

            if (packet.Fields.Count > byte.MaxValue)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name}: packet exceeds {byte.MaxValue} wire fields (actual: {packet.Fields.Count}).");
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
            var instance = new T();
            var reader = new ChunkReader(packet, 0);

            foreach (var desc in descriptors)
            {
                object value;
                try
                {
                    value = ShapeCodec.ReadField(desc.Shape, reader);
                }
                catch (InvalidOperationException ex)
                {
                    if (desc.Shape is ValueShape)
                    {
                        // Insufficient bytes for a value-type means the wire is short on whole fields.
                        throw new InvalidOperationException(
                            $"Field count mismatch for {typeof(T).Name}: expected {descriptors.Length}, got {packet.Fields.Count}.", ex);
                    }
                    if (desc.Shape is StringShape)
                    {
                        throw new InvalidOperationException(
                            $"{typeof(T).Name}.{desc.Field.Name}: string truncated ({ex.Message}).", ex);
                    }
                    throw new InvalidOperationException(
                        $"{typeof(T).Name}.{desc.Field.Name}: {ex.Message}", ex);
                }
                desc.Setter(instance, value);
            }

            if (reader.Available != 0)
            {
                throw new InvalidOperationException(
                    $"Field count mismatch for {typeof(T).Name}: trailing bytes after all descriptors consumed (remaining: {reader.Available}, wireIdx: {reader.WireIdx}, fields: {packet.Fields.Count}).");
            }

            return instance;
        }
    }
}
