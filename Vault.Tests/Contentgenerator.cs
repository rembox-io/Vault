using System;
using System.Diagnostics;
using Vault.Core.Data;

namespace Vault.Tests
{
    public static class Gc
    {
        public static byte[] GetByteBufferFromPattern(byte[] pattern, int bufferSize, int numberOfWriteingBytes, byte[] prefix = null)
        {
            int startIndex = 0;
            var buffer = new byte[bufferSize];

            if (prefix != null)
            {
                startIndex = prefix.Length;
                if (bufferSize >= numberOfWriteingBytes + prefix.Length)
                    numberOfWriteingBytes += startIndex;
                Array.Copy(prefix, buffer, prefix.Length);
            }

            var j = 0;
            for (int i = startIndex; i < numberOfWriteingBytes; i++)
            {
                var patternIndex = (j++ + pattern.Length)%pattern.Length;
                buffer[i] = pattern[patternIndex];
            }
            return buffer;
        }

        public static byte[] P1(int size = Chunk.MaxContentSize, byte[] prefix = null, int bufferSize = -1)
        {
            if (bufferSize == -1)
                bufferSize = size;
            return GetByteBufferFromPattern(Pattern1, bufferSize, size, prefix);
        }

        public static byte[] P2(int size = Chunk.MaxContentSize, byte[] prefix = null)
        {
            return GetByteBufferFromPattern(Pattern2, size, size, prefix);
        }

        public static byte[] P3(int size = Chunk.MaxContentSize, byte[] prefix = null)
        {
            return GetByteBufferFromPattern(Pattern3, size, size, prefix);
        }

        public static byte[] Empty(int size = Chunk.MaxContentSize)
        {
            return GetByteBufferFromPattern(new [] { (byte)0 }, size, size);
        }

        public static readonly byte[] Pattern1 = { 21, 22, 23, 24, 25 };
        public static readonly byte[] Pattern2 = { 31, 32, 33, 34, 35 };
        public static readonly byte[] Pattern3 = { 41, 42, 43, 44, 45 };
        public static readonly byte[] PatternEmpty = { 0 };
    }
}