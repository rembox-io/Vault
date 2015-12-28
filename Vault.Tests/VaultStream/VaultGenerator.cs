using System.IO;
using Vault.Core;
using Vault.Core.Data;

namespace Vault.Tests.VaultStream
{
    public class VaultGenerator
    {
        public VaultGenerator()
        {
            _stream = new MemoryStream();
            _writer = new BinaryWriter(_stream);
        }

        public VaultGenerator InitializeVault()
        {
            var buffer = new byte[Constants.VaultMetadataSize];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = 1;

            _writer.Write(buffer);

            WriteBlock(pattern: new byte[] {10, 11, 12},
                isMasterBlock: true,
                isFirstBlock: true);

            return this;
        }

        public VaultGenerator WriteBlock(ushort continuation = 0, int allocated = DefaultBlockCOntentSize, byte[] pattern = null, bool isFirstBlock = true, bool isMasterBlock = false)
        {
            if (pattern == null)
                pattern = new byte[] {1, 2, 3};


            var flags = BlockFlags.None;
            if(isFirstBlock)
                flags |= BlockFlags.IsFirstBlock;
            if(isMasterBlock)
                flags |= BlockFlags.IsMaserBlock;
            if(continuation == 0)
                flags |= BlockFlags.IsLastBlock;

            var blockInfo = new BlockInfo(_currentIndex, continuation, allocated, flags);

            var allocatedSize = allocated < DefaultBlockCOntentSize ? allocated : DefaultBlockCOntentSize;

            var buffer = GetByteBufferFromPattern(pattern, DefaultBlockCOntentSize, allocatedSize);

            _writer.Write(blockInfo.ToBinary());
            _writer.Write(buffer);

            _currentIndex++;

            return this;
        }

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

        public Stream GetStream()
        {
            _stream.Seek(0, SeekOrigin.Begin);
            return _stream;
        }

        private ushort _currentIndex;

        private readonly MemoryStream _stream;
        private readonly BinaryWriter _writer;

        private const int DefaultBlockCOntentSize = 55;
    }
}
