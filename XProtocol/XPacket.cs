using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace XProtocol
{
    public class XPacket
    {
        public byte PacketType { get; set; }
        public byte PacketSubtype { get; set; }
        public List<XPacketField> Fields { get; } = new List<XPacketField>();
        public bool Protected { get; set; }
        public bool ChangeHeaders { get; set; }

        private XPacket() { }

        public static XPacket Create(byte type, byte subtype)
        {
            return new XPacket
            {
                PacketType = type,
                PacketSubtype = subtype
            };
        }

        public static XPacket Create(XPacketType type)
        {
            var (btype, bsubtype) = XPacketTypeManager.GetType(type);
            return Create(btype, bsubtype);
        }

        public void AppendValue(object structure)
        {
            if (structure == null)
            {
                throw new ArgumentNullException(nameof(structure));
            }

            if (!structure.GetType().IsValueType)
            {
                throw new ArgumentException("Only value types are supported.", nameof(structure));
            }

            var bytes = FixedObjectToByteArray(structure);
            if (bytes.Length > byte.MaxValue)
            {
                throw new InvalidOperationException("Field is too large (>255 bytes).");
            }

            Fields.Add(new XPacketField
            {
                FieldSize = (byte)bytes.Length,
                Contents = bytes
            });
        }

        internal void AppendChunks(byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (payload.Length == 0)
            {
                throw new ArgumentException("Payload must be non-empty.", nameof(payload));
            }

            int offset = 0;
            while (offset < payload.Length)
            {
                int size = Math.Min(byte.MaxValue, payload.Length - offset);
                var chunk = new byte[size];
                Buffer.BlockCopy(payload, offset, chunk, 0, size);
                Fields.Add(new XPacketField
                {
                    FieldSize = (byte)size,
                    Contents = chunk
                });
                offset += size;
            }
        }

        internal byte[] GetRawAt(int index)
        {
            if (index < 0 || index >= Fields.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return Fields[index].Contents ?? Array.Empty<byte>();
        }

        public T GetValueAt<T>(int index) where T : struct
        {
            if (index < 0 || index >= Fields.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var field = Fields[index];
            return ByteArrayToFixedObject<T>(field.Contents);
        }

        public object GetValueAt(int index, Type t)
        {
            if (index < 0 || index >= Fields.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return typeof(XPacket)
                .GetMethod(nameof(GetValueAt), new[] { typeof(int) })
                .MakeGenericMethod(t)
                .Invoke(this, new object[] { index });
        }

        public byte[] ToPacket()
        {
            if (Fields.Count > byte.MaxValue)
            {
                throw new InvalidOperationException("Too many fields (>255).");
            }

            var ms = new MemoryStream();
            ms.Write(ChangeHeaders
                ? new byte[] { 0x95, 0xAA, 0xFF, PacketType, PacketSubtype }
                : new byte[] { 0xAF, 0xAA, 0xAF, PacketType, PacketSubtype }, 0, 5);

            ms.WriteByte((byte)Fields.Count);

            foreach (var f in Fields)
            {
                ms.WriteByte(f.FieldSize);
                if (f.FieldSize > 0)
                {
                    ms.Write(f.Contents, 0, f.FieldSize);
                }
            }

            ms.Write(new byte[] { 0xFF, 0x00 }, 0, 2);
            return ms.ToArray();
        }

        public static XPacket Parse(byte[] packet, bool markAsEncrypted = false)
        {
            if (packet == null || packet.Length < 8)
            {
                return null;
            }

            bool encrypted = false;
            if (!(packet[0] == 0xAF && packet[1] == 0xAA && packet[2] == 0xAF))
            {
                if (packet[0] == 0x95 && packet[1] == 0xAA && packet[2] == 0xFF)
                {
                    encrypted = true;
                }
                else
                {
                    return null;
                }
            }

            var type = packet[3];
            var subtype = packet[4];
            var fieldCount = packet[5];

            var xp = new XPacket
            {
                PacketType = type,
                PacketSubtype = subtype,
                Protected = markAsEncrypted
            };

            int pos = 6;
            int payloadEnd = packet.Length - 2;

            for (int i = 0; i < fieldCount; i++)
            {
                if (pos + 1 > payloadEnd)
                {
                    return null;
                }

                var size = packet[pos++];
                if (pos + size > payloadEnd)
                {
                    return null;
                }

                var contents = size != 0 ? packet.Skip(pos).Take(size).ToArray() : null;
                pos += size;

                xp.Fields.Add(new XPacketField
                {
                    FieldSize = size,
                    Contents = contents
                });
            }

            if (pos != payloadEnd || packet[pos] != 0xFF || packet[pos + 1] != 0x00)
            {
                return null;
            }

            return encrypted ? DecryptPacket(xp) : xp;
        }

        public XPacket Encrypt()
        {
            return EncryptPacket(this);
        }

        public static XPacket EncryptPacket(XPacket packet)
        {
            if (packet == null)
            {
                return null;
            }

            var rawBytes = packet.ToPacket();
            var encrypted = XProtocolEncryptor.Encrypt(rawBytes);

            int requiredFields = (encrypted.Length + byte.MaxValue - 1) / byte.MaxValue;
            if (requiredFields > byte.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Encrypted packet exceeds {byte.MaxValue} wire fields (needs {requiredFields}). Reduce payload size.");
            }

            var p = Create(0, 0);
            p.AppendChunks(encrypted);
            p.ChangeHeaders = true;
            return p;
        }

        public static XPacket Decrypt(XPacket packet)
        {
            return DecryptPacket(packet);
        }

        private static XPacket DecryptPacket(XPacket packet)
        {
            if (packet == null || packet.Fields.Count == 0)
            {
                return null;
            }

            int total = 0;
            foreach (var f in packet.Fields)
            {
                total += f.FieldSize;
            }

            var rawData = new byte[total];
            int offset = 0;
            foreach (var f in packet.Fields)
            {
                if (f.FieldSize > 0)
                {
                    Buffer.BlockCopy(f.Contents, 0, rawData, offset, f.FieldSize);
                    offset += f.FieldSize;
                }
            }

            var decrypted = XProtocolEncryptor.Decrypt(rawData);
            return Parse(decrypted, true);
        }

        internal void AppendRawBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            if (bytes.Length > byte.MaxValue)
            {
                throw new InvalidOperationException("Field is too large (>255 bytes).");
            }

            Fields.Add(new XPacketField
            {
                FieldSize = (byte)bytes.Length,
                Contents = bytes
            });
        }

        private static byte[] FixedObjectToByteArray(object value)
        {
            var size = Marshal.SizeOf(value.GetType());
            var arr = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return arr;
        }

        private static T ByteArrayToFixedObject<T>(byte[] bytes) where T : struct
        {
            T value;
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                value = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
            return value;
        }
    }
}
