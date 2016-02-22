namespace Vault.Tests
{
    public static class Gc
    {
        public static byte[] GetByteBufferFromPattern(byte[] pattern, int bufferSize, int numberOfWriteingBytes)
        {
            var buffer = new byte[bufferSize];
            for (int i = 0; i < numberOfWriteingBytes; i++)
            {
                var patternIndex = (i + pattern.Length)%pattern.Length;
                buffer[i] = pattern[patternIndex];
            }
            return buffer;
        }

        public static byte[] P1(int size)
        {
            return GetByteBufferFromPattern(Pattern1, size, size);
        }

        public static byte[] P2(int size)
        {
            return GetByteBufferFromPattern(Pattern2, size, size);
        }

        public static byte[] P3(int size)
        {
            return GetByteBufferFromPattern(Pattern3, size, size);
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