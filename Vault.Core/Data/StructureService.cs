using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vault.Core.Exceptions;
using Vault.Core.Tools;

namespace Vault.Core.Data
{
    public class StructureService 
    {
        public StructureService(Stream stream)
        {
            _stream = stream;
            BlockMaskStorage = new LazyStorage<int, BitMask>(GetRecordBlockMask);
        }

        // public methods

        public Record ReadRecord(ushort recordId)
        {
            var chunkSequence = ReadChunkSequence(recordId);

            var record = GetRecordFromChunkSequence(chunkSequence);

            return record;
        }

        public ushort WriteRecord(Record record)
        {
            var binary = record.ToBinary();

            Chunk[] chunks = CreateChunkSequenceForRecordBinary(binary);

            foreach (var chunk in chunks)
                WriteChunk(chunk);

            record.Id = chunks[0].Id;
            return chunks[0].Id;
        }

        // internal methods

        internal Chunk[] CreateChunkSequenceForRecordBinary(byte[] binary)
        {
            var chunkContentArray = binary.Split(Chunk.MaxContentSize, true);
            var chunkArray = new Chunk[chunkContentArray.Length];
            for (int index = 0; index < chunkContentArray.Length; index++)
            {
                var chunk = new Chunk();
                chunk.Id = GetAndReserveNextAvialableRecordIndex();
                chunk.Content = chunkContentArray[index];

                if (index == 0)
                {
                    chunk.Flags = ChunkFlags.IsFirstChunk;
                    if(chunkContentArray.Length == 1)
                        chunk.Flags |= ChunkFlags.IsLastChunk;
                }
                else
                {
                    if (index > 0)
                        chunkArray[index - 1].Continuation = chunk.Id;
                    if (chunkContentArray.Length - 1 == index)
                        chunk.Flags = ChunkFlags.IsLastChunk;
                }

                chunkArray[index] = chunk;
            }
            return chunkArray;
        }

        internal Chunk ReadChunk(ushort recordId)
        {
            var recordAllocated = BlockMaskStorage[recordId/_numberOfRecordsInRecordBlock][LocalizeChunkId(recordId)];
            if (!recordAllocated)
                throw new VaultException();

            var offset = GetChunkOffset(recordId);
            var buffer = new byte[_recordSize];
            _stream.Seek(offset, SeekOrigin.Begin);
            _stream.Read(buffer, 0, _recordSize);

            return new Chunk(buffer);
        }

        internal void WriteChunk(Chunk chunk)
        {
            var blockIndex = chunk.Id/_numberOfRecordsInRecordBlock;
            var block = GetOrCreateRecrodsBlockMask(blockIndex);

            if (block == null)
                throw new VaultException();

            var offset = GetChunkOffset(chunk.Id);
            _stream.Seek(offset, SeekOrigin.Begin);
            var binary = chunk.ToBinary();
            _stream.Write(binary, 0, binary.Length);
            SetChunkOccupatedValue(chunk.Id, true);
        }

        internal Record GetRecordFromChunkSequence(Chunk[] chunkSequence)
        {
            var recordRawLength = chunkSequence.Sum(p => p.Content.Length);

            var buffer = new byte[recordRawLength];
            buffer.Write(w =>
            {
                foreach (var chunk in chunkSequence)
                {
                    w.Write(chunk.Content);
                }
            });

            var record = new Record(buffer);
            return record;
        }

        internal Chunk[] ReadChunkSequence(ushort headChunkId)
        {
            var result = new List<Chunk>();

            var currentChunk = ReadChunk(headChunkId);
            result.Add(currentChunk);

            if (!currentChunk.Flags.HasFlag(ChunkFlags.IsFirstChunk))
                throw new VaultException("Chunk sequence cant start from chunk without IsFirstChunk flag.");

            while (!currentChunk.Flags.HasFlag(ChunkFlags.IsLastChunk) && currentChunk.Continuation > 0)
            {
                currentChunk = ReadChunk(currentChunk.Continuation);
                result.Add(currentChunk);
            }

            if (!result.Last().Flags.HasFlag(ChunkFlags.IsLastChunk))
                throw new VaultException("Chunk sequence cant start with chunk without IsLastChunk flag.");

            return result.ToArray();
        }

        internal int GetChunkOffset(ushort recordId)
        {
            var part = recordId/_numberOfRecordsInRecordBlock;
            var offset = part*_recordsBlockSize + _recordMaskSize;

            var recordIndexInRecordBlock = LocalizeChunkId(recordId);

            offset += recordIndexInRecordBlock * _recordSize;

            return offset;
        }

        internal Dictionary<int, BitMask> Masks => BlockMaskStorage.CloneAsDictionary();

        // private methods

        private int LocalizeChunkId(ushort recordId)
        {
            var recordIndexInRecordBlock = recordId%_numberOfRecordsInRecordBlock;
            recordIndexInRecordBlock = recordId < _numberOfRecordsInRecordBlock ? recordId : recordIndexInRecordBlock;
            return recordIndexInRecordBlock;
        }        

        private ushort GetAndReserveNextAvialableRecordIndex()
        {
            for (int blockIndex = 0; blockIndex < BlockMaskStorage.Count; blockIndex++)
            {
                var result = BlockMaskStorage[blockIndex].GetFirstIndexOf(false);
                BlockMaskStorage[blockIndex].SetReserveValueTo(result, true);
                if (result > -1)
                {
                    if(result > ushort.MaxValue)
                        throw new VaultException("Reached maximum number of chunks in vault.");
                    return (ushort) result;
                }
            }

            var mask = GetOrCreateRecrodsBlockMask(BlockMaskStorage.Count);
            var value = mask.GetFirstIndexOf(false);
            mask.SetReserveValueTo(value, true);
            return (ushort)value;
        }

        private void SetChunkOccupatedValue(ushort chunkId, bool value)
        {
            var localChunkId = LocalizeChunkId(chunkId);
            var blockIndex = chunkId/_numberOfRecordsInRecordBlock;

            var offset = _fullRecordSzie * blockIndex;
            BlockMaskStorage[blockIndex].SetValueTo(localChunkId, value);
            _stream.Seek(offset, SeekOrigin.Begin);
            _stream.Write(BlockMaskStorage[blockIndex].Bytes, 0 ,BlockMaskStorage[blockIndex].Bytes.Length);
        }

        internal bool GetChunkOccupatedValue(ushort chunkId)
        {
            var localChunkId = LocalizeChunkId(chunkId);
            var blockIndex = chunkId / _numberOfRecordsInRecordBlock;

            return BlockMaskStorage[blockIndex][localChunkId];
        }

        private BitMask GetRecordBlockMask(int blockIndex)
        {
            var offset = _recordsBlockSize * blockIndex;
            if (offset > _stream.Length)
                throw new VaultException();

            _stream.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[_recordMaskSize];
            _stream.Read(buffer, 0, _recordMaskSize);

            return new BitMask(buffer);
        }

        private BitMask GetOrCreateRecrodsBlockMask(int blockIndex)
        {
            var blockMask = BlockMaskStorage[blockIndex];
            if (blockMask != null)
                return blockMask;

            var newBitMask = new byte[_recordMaskSize];
            _stream.Seek(_recordsBlockSize * blockIndex, SeekOrigin.Begin);
            _stream.Write(newBitMask, 0, newBitMask.Length);

            return new BitMask(newBitMask);
            
        }

        // fields

        internal readonly LazyStorage<int, BitMask> BlockMaskStorage;

        private static readonly int _recordSize = 1024;
        private static readonly int _fullRecordSzie = 1143;
        private static readonly int _numberOfRecordsInRecordBlock = 1016;

        // Full size of block, wich contains 1016 records by 1024 byte,  and avialability metadata for them by 127 byte;
        internal static readonly int _recordsBlockSize = 1040511;
        internal static readonly int _recordMaskSize = 127;


        private readonly Stream _stream;
    }
}