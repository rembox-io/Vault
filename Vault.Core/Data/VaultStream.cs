using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vault.Core.Exceptions;

namespace Vault.Core.Data
{
    public class VaultStream : Stream
    {
        internal VaultStream(Stream backStream, int startBlockIndex, VaultConfiguration vaultConfiguration)
        {
            _backStream = backStream;
            _startBlockIndex = startBlockIndex;
            _vaultConfiguration = vaultConfiguration;

            ValidateAndCalculating();
        }

        private void ValidateAndCalculating()
        {
            var result = new List<BlockInfo>();
            var seaarchingBlockIndex = _startBlockIndex;

            _vaultInfo = GetVaultInfo();

            while (seaarchingBlockIndex != -1)
            {
                var block = GetBlockInfo(seaarchingBlockIndex);

                if (block == null)
                    throw new VaultException();

                seaarchingBlockIndex = block.Continuation == 0 ? -1 : block.Continuation;
                result.Add(block);
            }

            _allocated = result.Sum(p => p.Allocated);
            _blocks = result.OrderBy(p => p.Index).ToList();
        }

        private BlockInfo GetBlockInfo(int blockIndex)
        {
            try
            {
                var offset = GetBlockOffset(blockIndex);
                var buffer = new byte[_vaultConfiguration.BlockMetadataSize];
                _backStream.Seek(offset, SeekOrigin.Begin);
                _backStream.Read(buffer, 0, _vaultConfiguration.BlockMetadataSize);

                var result = new BlockInfo(buffer);
                return result;
            }
            catch (Exception exception)
            {
                return null;
            }
        }

        internal VaultInfo GetVaultInfo()
        {
            _backStream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[_vaultConfiguration.VaultMetadataSize];
            _backStream.Read(buffer, 0, buffer.Length);

            return new VaultInfo(buffer, _vaultConfiguration);
        }

        internal void SetBlockLength(BlockInfo block, int blockLength)
        {
        }

        internal BlockInfo[] AllocateBlocks(int numberOfBlocksToAllocated)
        {
            var resultIndexes = new BlockInfo[numberOfBlocksToAllocated];
            for (int i = 0; i < numberOfBlocksToAllocated; i++)
            {
                var block = new BlockInfo();
                block.Index = (ushort) _vaultInfo.NumbersOfAllocatedBlocks;
                block.Flags = BlockFlags.None;
                var blockBinary = block.ToBinary();

                _backStream.Seek(0, SeekOrigin.End);

                _backStream.Write(blockBinary, 0, blockBinary.Length);
                _backStream.Write(Enumerable.Repeat((byte)0, _vaultConfiguration.BlockContentSize).ToArray(), 0, _vaultConfiguration.BlockContentSize);

                resultIndexes[i] = block;

                _vaultInfo.Mask[block.Index] = true;
                _vaultInfo.NumbersOfAllocatedBlocks++;
            }

            ApplayVaultInfo();

            return resultIndexes;
        }

        internal void ReleaseBlocks(params ushort[] blocksForRelease)
        {
            var invalidIndexes = blocksForRelease.Where(p => _vaultInfo.NumbersOfAllocatedBlocks - 1 < p).ToArray();
            if (invalidIndexes.Any())
                throw new ArgumentException($"Can't release blocks with indexes:{string.Join(",", invalidIndexes)}");

            foreach (var index in blocksForRelease)
            {
                _vaultInfo.Mask[index] = false;

                var blockOffset = GetBlockOffset(index);
                _backStream.Seek(blockOffset, SeekOrigin.Begin);

                var info = new BlockInfo(index, 0, 0, BlockFlags.None);
                var infoBuffer = info.ToBinary();
                _backStream.Write(infoBuffer, 0, infoBuffer.Length);

                var zeroArray = new byte[_vaultConfiguration.BlockContentSize];
                _backStream.Write(zeroArray, 0, zeroArray.Length);
            }

            ApplayVaultInfo();
        }

        internal int GetBlockOffset(int blockIndex)
        {
            return _vaultConfiguration.VaultMetadataSize + blockIndex* _vaultConfiguration.BlockFullSize;
        }

        internal Range[] GetBackStreaamRangesToCopy(int count)
        {
            var searchRange = new Range((int)Position, (int)Position + count);
            
            var startBlockIndex = (int)Position/ _vaultConfiguration.BlockContentSize;
            var endBlockIndex = (Position + count)/ _vaultConfiguration.BlockContentSize;

            var result = new List<Range>();
            for (int i = startBlockIndex; i <= endBlockIndex && i < _blocks.Count; i++)
            {
                var block = _blocks[i];
                var range = GetBackRangeForBlock(block.Index, block.Allocated, i, searchRange);
                result.Add(range);
            }

            return result.ToArray();
        }

        internal Range GetBackRangeForBlock(int blockIndex, int blockAllocated, int localBlockIndex, Range upRange)
        {
            int blockLocalFrom = localBlockIndex* _vaultConfiguration.BlockContentSize;
            int blockLocalTo = blockLocalFrom + blockAllocated;

            if(blockLocalTo<upRange.From || blockLocalFrom > upRange.To)
                return Range.Empty;

            var resultLocalFrom = upRange.From >= blockLocalFrom ? upRange.From : blockLocalFrom;
            var resultLocalTo = upRange.To <= blockLocalTo ? upRange.To : blockLocalTo;

            var offset = GetBlockOffset(blockIndex) + _vaultConfiguration.BlockMetadataSize;

            var from = resultLocalFrom - blockLocalFrom;
            var to = resultLocalTo - blockLocalFrom;
            return new Range(from + offset, to + offset);
        }

        internal int GetNumberOfBlocksForLength(long length)
        {
            if (length == 0)
                return 0;

            var result = length/_vaultConfiguration.BlockContentSize;
            if (result == 0)
                return 1;

            if (length%_vaultConfiguration.BlockContentSize > 0)
                result++;
            return (int) result;
        }

        internal void ApplayVaultInfo()
        {
            var bytes = _vaultInfo.ToBinary((short)_vaultConfiguration.VaultMetadataSize);

            _backStream.Seek(0, SeekOrigin.Begin);
            _backStream.Write(bytes, 0, bytes.Length);
        }

        #region Stream Impl

        public override void Flush()
        {
            throw new System.NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                Position = offset;
            }
            else if (origin == SeekOrigin.Current)
            {
                Position += offset;
            }
            else
            {
                Position = Length + offset;
            }
            return Position;
        }

        public override void SetLength(long value)
        {
            if (value == Length)
                return;

            int numberOfRequiredBlocks = GetNumberOfBlocksForLength(value);

            if (value > Length)
            {
                var numberOfBlocksForRelease = _blocks.Count - numberOfRequiredBlocks;
                var blocksForRelease = _blocks.Skip(_blocks.Count - numberOfBlocksForRelease).Select(p=>p.Index).ToArray();

                ReleaseBlocks(blocksForRelease);

                SetBlockLength(_blocks.Last(), (int)value%_vaultConfiguration.BlockContentSize);
            }
            else
            {
                var numberOfBlocksToAllocated = numberOfRequiredBlocks - _blocks.Count;

                AllocateBlocks(numberOfBlocksToAllocated);

                SetBlockLength(_blocks.Last(), (int)value% _vaultConfiguration.BlockContentSize);
            }
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            var ranges = GetBackStreaamRangesToCopy(count);
            var numberOfreadedBytes = 0;

            foreach (var range in ranges)
            {
                _backStream.Seek(range.From, SeekOrigin.Begin);
                _backStream.Read(buffer, offset, range.Length);
                offset += range.Length;
                numberOfreadedBytes += range.Length;
            }

            return numberOfreadedBytes;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        public override long Length => _allocated;

        public override long Position
        {
            get { return _position; }
            set
            {
                if(value <0 || Length - 1< value )
                    throw new ArgumentOutOfRangeException(nameof(value));

                _position = value;
            }
        }


        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;

        internal int BlockCount => _blocks.Count;
        internal Stream BackStream => _backStream;

        #endregion

        // private methods

        private long _currentPosition = 0;
        private int _baseOffset = 0;
        private int _allocated = 0;
        private List<BlockInfo> _blocks;
        private VaultInfo _vaultInfo;

        private readonly Stream _backStream;
        private readonly int _startBlockIndex;
        private readonly VaultConfiguration _vaultConfiguration;
        private long _position;
    }
}