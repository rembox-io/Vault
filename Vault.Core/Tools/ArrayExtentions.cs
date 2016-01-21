using System;
using System.Diagnostics.Contracts;
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

        public static byte[][] Split(this byte[] self, int chunkLength, bool collapseLastChunkToContent = false)
        {
            Contract.Requires(self != null);

            int numberOfChunks = 0;
            if (self.Length <= chunkLength)
            {
                numberOfChunks = 1;
            }
            else
            {
                numberOfChunks = self.Length/chunkLength;
                if (self.Length%chunkLength > 0)
                    numberOfChunks++;
            }

            var result = new byte[numberOfChunks][];
            for (int i = 0; i < numberOfChunks; i++)
            {
                var from = i*chunkLength;
                var to = from + chunkLength;
                if (i == numberOfChunks - 1)
                {
                    to = from + self.Length%chunkLength;
                    if (collapseLastChunkToContent)
                    {
                        result[i] = new byte[to];
                    }
                }
                else
                {
                    result[i] = new byte[chunkLength];
                }
                Array.Copy(self, from, result[i], 0, to);
            }

            return result;
        }
    }
}
