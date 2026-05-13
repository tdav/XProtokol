using System;
using System.Collections.Generic;
using XProtocol.Serializator;

namespace XProtocol
{
    public static class XPacketTypeManager
    {
        private static readonly Dictionary<XPacketType, (byte Type, byte Subtype)> typeRegistry =
            new Dictionary<XPacketType, (byte Type, byte Subtype)>();

        private static readonly Dictionary<Type, (byte Type, byte Subtype)> bytesByDtoType =
            new Dictionary<Type, (byte Type, byte Subtype)>();

        private static readonly Dictionary<Type, FieldDescriptor[]> descriptorCache =
            new Dictionary<Type, FieldDescriptor[]>();

        private static readonly object syncRoot = new object();

        static XPacketTypeManager()
        {
            Register<XPacketHandshake>(XPacketType.Handshake, 1, 0);
        }

        public static void Register<T>(XPacketType packetType, byte type, byte subtype) where T : class
        {
            lock (syncRoot)
            {
                if (typeRegistry.ContainsKey(packetType))
                {
                    throw new InvalidOperationException($"Packet type {packetType:G} is already registered.");
                }

                var descriptors = BuildDescriptors(typeof(T));
                descriptorCache[typeof(T)] = descriptors;
                bytesByDtoType[typeof(T)] = (type, subtype);
                typeRegistry[packetType] = (type, subtype);
            }
        }

        public static (byte Type, byte Subtype) GetType(XPacketType packetType)
        {
            lock (syncRoot)
            {
                if (!typeRegistry.TryGetValue(packetType, out var pair))
                {
                    throw new InvalidOperationException($"Packet type {packetType:G} is not registered.");
                }
                return pair;
            }
        }

        public static XPacketType GetTypeFromPacket(XPacket packet)
        {
            var type = packet.PacketType;
            var subtype = packet.PacketSubtype;

            lock (syncRoot)
            {
                foreach (var kv in typeRegistry)
                {
                    if (kv.Value.Type == type && kv.Value.Subtype == subtype)
                    {
                        return kv.Key;
                    }
                }
            }
            return XPacketType.Unknown;
        }

        internal static FieldDescriptor[] GetDescriptors(Type t)
        {
            lock (syncRoot)
            {
                if (!descriptorCache.TryGetValue(t, out var d))
                {
                    throw new InvalidOperationException(
                        $"Type {t.Name} is not registered. Call XPacketTypeManager.Register<{t.Name}>(...) first.");
                }
                return d;
            }
        }

        internal static (byte Type, byte Subtype) GetBytesFor(Type t)
        {
            lock (syncRoot)
            {
                if (!bytesByDtoType.TryGetValue(t, out var bytes))
                {
                    throw new InvalidOperationException(
                        $"Type {t.Name} is not registered. Call XPacketTypeManager.Register<{t.Name}>(...) first.");
                }
                return bytes;
            }
        }

        private static FieldDescriptor[] BuildDescriptors(Type t)
        {
            return ShapeResolver.BuildDescriptors(t, new HashSet<Type>());
        }
    }
}
