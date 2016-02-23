using System;
using System.Diagnostics;

namespace Vault.Tests
{
    public static class Gc
    {
        public static byte[] GetByteBufferFromPattern(byte[] pattern, int bufferSize, int numberOfWriteingBytes, byte[] prefix = null)
        {
            var buffer = new byte[bufferSize];

            int startIndex = 0;
            if (prefix != null)
            {
                startIndex = prefix.Length;
                Array.Copy(prefix, buffer, prefix.Length);
            }

            for (int i = startIndex; i < numberOfWriteingBytes; i++)
            {
                var patternIndex = (i + pattern.Length)%pattern.Length;
                buffer[i] = pattern[patternIndex];
            }
            return buffer;
        }

        public static byte[] P1(int size, byte[] prefix = null)
        {
            return GetByteBufferFromPattern(Pattern1, size, size, prefix);
        }

        public static byte[] P2(int size, byte[] prefix = null)
        {
            return GetByteBufferFromPattern(Pattern2, size, size, prefix);
        }

        public static byte[] P3(int size, byte[] prefix = null)
        {
            return GetByteBufferFromPattern(Pattern3, size, size, prefix);
        }

        public static byte[] Empty(int size)
        {
            return GetByteBufferFromPattern(new [] { (byte)0 }, size, size);
        }

        public static readonly byte[] Pattern1 = { 21, 22, 23, 24, 25 };
        public static readonly byte[] Pattern2 = { 31, 32, 33, 34, 35 };
        public static readonly byte[] Pattern3 = { 41, 42, 43, 44, 45 };
        public static readonly byte[] PatternEmpty = { 0 };
    }
}