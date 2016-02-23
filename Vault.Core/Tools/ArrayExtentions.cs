using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
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
            if (self.Length < chunkLength)
                chunkLength = self.Length;

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
                var to = chunkLength;
                if (i == numberOfChunks - 1)
                {
                    int size;
                    if (collapseLastChunkToContent)
                    {
                        size = self.Length%chunkLength;
                        if (size == 0)
                            size = self.Length;
                    }
                    else
                    {
                        size = chunkLength;
                    }

                    to = self.Length%chunkLength;
                    if (to == 0)
                    {
                        to = self.Length < chunkLength
                            ? self.Length
                            : chunkLength;
                    }

                    result[i] = new byte[size];
                }
                else
                {
                    result[i] = new byte[chunkLength];
                }
                Array.Copy(self, from, result[i], 0, to);
            }

            return result;
        }

        public static byte[] Join(params byte[][] contentArray)
        {
            var length = contentArray.Sum(p => p.Length);
            var buffer = new byte[length];

            int offset = 0;
            foreach (var content in contentArray)
            {
                Array.Copy(content, 0, buffer, offset, content.Length);
                offset += content.Length;
            }

            return buffer;
        }
    }
}
