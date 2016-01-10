using System;
using System.IO;
using System.Text;

namespace Vault.Core.Tools
{
    public static class ArrayExtentions
    {
        public static byte[] Write(this byte[] self, Action<BinaryWriter> action)
        {
            using (var stream = new MemoryStream(self))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    action(writer);
                }
            }

            return self;
        }

        public static BinaryWriter WriteString2(this BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write((short)bytes.Length);
            writer.Write(bytes);
            return writer;
        }

        public static byte[] Read(this byte[] self, Action<BinaryReader> action)
        {
            using (var stream = new MemoryStream(self))
            {
                using (var reader = new BinaryReader(stream))
                {
                    action(reader);
                }
            }

            return self;
        }

        public static string ReadString2(this BinaryReader reader)
        {
            var stringSize = reader.ReadInt16();
            var buffer = reader.ReadBytes(stringSize);
            var result = Encoding.UTF8.GetString(buffer);
            return result;
        }

    }
}
