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
            _blockMaskStorage = new LazyStorage<int, BitMask>(GetRecordBlockMask);
        }

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

        internal Chunk[] CreateChunkSequenceForRecordBinary(byte[] binary)
        {
            var chunkContentArray = binary.Split(Chunk.MaxContentSize);
            var chunkArray = new Chunk[chunkContentArray.Length];
            for (int index = 0; index < chunkContentArray.Length; index++)
            {
                var chunk = new Chunk();
                chunk.Id = GetNextAvialableRecordIndex();
                chunk.Content = chunkContentArray[index];
                if (index == chunkContentArray.Length - 1)
                {
                    chunk.Continuation = 0;
                    chunk.Flags |= ChunkFlags.IsLastRecord;
                }
                else
                {
                    if(index > 0)
                        chunkArray[index - 1].Id = chunk.Continuation;

                    chunk.Flags |= ~ChunkFlags.IsLastRecord;
                }

                if (index == 0)
                    chunk.Flags |= ChunkFlags.IsFirstRecord;
                else
                    chunk.Flags |= ~ChunkFlags.IsFirstRecord;

                chunkArray[index] = chunk;
            }
            return chunkArray;
        }

        internal Chunk ReadChunk(ushort recordId)
        {
            var recordAllocated = _blockMaskStorage[recordId/_numberOfRecordsInRecordBlock][LocalizeChunkId(recordId)];
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

            if (!currentChunk.Flags.HasFlag(ChunkFlags.IsFirstRecord))
                throw new VaultException("Chunk sequence cant start from chunk without IsFirstRecord flag.");

            while (!currentChunk.Flags.HasFlag(ChunkFlags.IsLastRecord) && currentChunk.Continuation > 0)
            {
                currentChunk = ReadChunk(currentChunk.Continuation);
                result.Add(currentChunk);
            }

            if (!result.Last().Flags.HasFlag(ChunkFlags.IsLastRecord))
                throw new VaultException("Chunk sequence cant start with chunk without IsLastRecord flag.");

            return result.ToArray();
        }

        private int GetChunkOffset(ushort recordId)
        {
            var part = recordId/_numberOfRecordsInRecordBlock;
            var offset = part*_recordsBlockSize + _recordMaskSize;

            var recordIndexInRecordBlock = LocalizeChunkId(recordId);

            offset += recordIndexInRecordBlock * _recordSize;

            return offset;
        }

        private int LocalizeChunkId(ushort recordId)
        {
            var recordIndexInRecordBlock = recordId%_numberOfRecordsInRecordBlock;
            recordIndexInRecordBlock = recordIndexInRecordBlock == 0 ? recordId : recordIndexInRecordBlock;
            return recordIndexInRecordBlock;
        }        

        private ushort GetNextAvialableRecordIndex()
        {
            for (int blockIndex = 0; blockIndex < _blockMaskStorage.Count; blockIndex++)
            {
                var result = _blockMaskStorage[blockIndex].GetFirstIndexOf(false);
                if (result > -1)
                {
                    if(result > ushort.MaxValue)
                        throw new VaultException("Reached maximum number of chunks in vault.");
                    return (ushort) result;
                }
            }

            var mask = GetOrCreateRecrodsBlockMask(_blockMaskStorage.Count);


            return (ushort)mask.GetFirstIndexOf(false);
        }

        private void SetChunkOccupatedValue(ushort chunkId, bool value)
        {
            var localChunkId = LocalizeChunkId(chunkId);
            var blockIndex = chunkId/_numberOfRecordsInRecordBlock;

            var offset = _fullRecordSzie * blockIndex;
            _blockMaskStorage[blockIndex].SetValueTo(localChunkId, value);
            _stream.Seek(offset, SeekOrigin.Begin);
            _stream.Write(_blockMaskStorage[blockIndex].Bytes, 0 ,_blockMaskStorage[blockIndex].Bytes.Length);
        }

        internal bool GetChunkOccupatedValue(ushort chunkId)
        {
            var localChunkId = LocalizeChunkId(chunkId);
            var blockIndex = chunkId / _numberOfRecordsInRecordBlock;

            return _blockMaskStorage[blockIndex][localChunkId];
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
            var blockMask = _blockMaskStorage[blockIndex];
            if (blockMask != null)
                return blockMask;

            var newBitMask = new byte[_recordMaskSize];
            _stream.Seek(_recordsBlockSize * blockIndex, SeekOrigin.Begin);
            _stream.Write(newBitMask, 0, newBitMask.Length);

            return new BitMask(newBitMask);
            
        }

        // fields

        private readonly LazyStorage<int, BitMask> _blockMaskStorage;

        private static readonly int _recordSize = 1024;
        private static readonly int _fullRecordSzie = 1143;
        private static readonly int _numberOfRecordsInRecordBlock = 1016;

        // Full size of block, wich contains 1016 records by 1024 byte,  and avialability metadata for them by 127 byte;
        internal static readonly int _recordsBlockSize = 1040511;
        internal static readonly int _recordMaskSize = 127;


        private readonly Stream _stream;
    }
}