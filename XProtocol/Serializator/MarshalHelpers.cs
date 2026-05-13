using System;
using System.Runtime.InteropServices;

namespace XProtocol.Serializator
{
    internal static class MarshalHelpers
    {
        public static byte[] ToBytes(object value, Type t)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var size = Marshal.SizeOf(t);
            var arr = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, ptr, false);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return arr;
        }

        public static object FromBytes(byte[] bytes, Type t)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure(handle.AddrOfPinnedObject(), t);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
