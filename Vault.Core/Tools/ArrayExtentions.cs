using System;
using System.IO;

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

    }
}
