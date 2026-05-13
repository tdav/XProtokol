using System;
using System.Text;

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
                if (desc.Kind == FieldKind.ValueType)
                {
                    packet.AppendValue(desc.Getter(obj));
                }
                else
                {
                    var s = desc.StringGetter(obj) ?? string.Empty;
                    var utf8 = Encoding.UTF8.GetBytes(s);

                    if (utf8.Length > ushort.MaxValue)
                    {
                        throw new InvalidOperationException(
                            $"{typeof(T).Name}.{desc.Field.Name}: string exceeds {ushort.MaxValue} UTF-8 bytes (actual: {utf8.Length}).");
                    }

                    var payload = new byte[utf8.Length + 2];
                    payload[0] = (byte)(utf8.Length & 0xFF);
                    payload[1] = (byte)((utf8.Length >> 8) & 0xFF);
                    Buffer.BlockCopy(utf8, 0, payload, 2, utf8.Length);
                    packet.AppendChunks(payload);
                }
            }

            if (packet.Fields.Count > byte.MaxValue)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name}: packet exceeds {byte.MaxValue} wire fields (actual: {packet.Fields.Count}). Reduce string field sizes.");
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
            int wireIdx = 0;

            foreach (var desc in descriptors)
            {
                if (desc.Kind == FieldKind.ValueType)
                {
                    if (wireIdx >= packet.Fields.Count)
                    {
                        throw new InvalidOperationException(
                            $"Field count mismatch for {typeof(T).Name}: expected {descriptors.Length}, got {packet.Fields.Count}.");
                    }
                    var raw = packet.GetValueAt(wireIdx, desc.Field.FieldType);
                    desc.Setter(instance, raw);
                    wireIdx++;
                }
                else
                {
                    if (wireIdx >= packet.Fields.Count)
                    {
                        throw new InvalidOperationException(
                            $"Field count mismatch for {typeof(T).Name}: expected {descriptors.Length}, got {packet.Fields.Count}.");
                    }
                    var first = packet.GetRawAt(wireIdx++);
                    if (first.Length < 2)
                    {
                        throw new InvalidOperationException(
                            $"{typeof(T).Name}.{desc.Field.Name}: string header truncated (first chunk size {first.Length} < 2).");
                    }
                    int len = first[0] | (first[1] << 8);

                    var acc = new byte[len];
                    int filled = 0;
                    int firstPayload = Math.Min(first.Length - 2, len);
                    Buffer.BlockCopy(first, 2, acc, 0, firstPayload);
                    filled += firstPayload;

                    while (filled < len)
                    {
                        if (wireIdx >= packet.Fields.Count)
                        {
                            throw new InvalidOperationException(
                                $"{typeof(T).Name}.{desc.Field.Name}: string truncated (need {len} bytes, have {filled} after consuming all remaining wire fields).");
                        }
                        var next = packet.GetRawAt(wireIdx++);
                        int take = Math.Min(next.Length, len - filled);
                        Buffer.BlockCopy(next, 0, acc, filled, take);
                        filled += take;
                    }

                    var str = Encoding.UTF8.GetString(acc);
                    desc.StringSetter(instance, str);
                }
            }

            if (wireIdx != packet.Fields.Count)
            {
                throw new InvalidOperationException(
                    $"Field count mismatch for {typeof(T).Name}: expected {wireIdx}, got {packet.Fields.Count}.");
            }

            return instance;
        }
    }
}
