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

            return chunks[0].Id;
        }

        private Chunk[] CreateChunkSequenceForRecordBinary(byte[] binary)
        {
            var chunkContentArray = binary.Split(Chunk.ContentSize);
            var chunkArray = new Chunk[chunkContentArray.Length];
            for (int index = 0; index < chunkContentArray.Length; index++)
            {
                var chunk = new Chunk();
                chunk.Id = GetNextAvialableRecordIndex();
                chunk.Content = chunkContentArray[index];
                if (index == chunkContentArray.Length - 1)
                {
                    chunk.Continuation = 0;
                    chunk.Flags = ChunkFlags.IsLastRecord;
                }
                else
                {
                    chunkArray[index - 1].Id = chunk.Continuation;
                    chunk.Flags &= ~ChunkFlags.IsLastRecord;
                }

                if (index == 0)
                    chunk.Flags &= ChunkFlags.IsFirstRecord;
                else
                    chunk.Flags &= ~ChunkFlags.IsFirstRecord;

                chunkArray[index] = chunk;
            }
            return chunkArray;
        }

        private Chunk ReadChunk(ushort recordId)
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

        private void WriteChunk(Chunk chunk)
        {
            var offset = GetChunkOffset(chunk.Id);
            _stream.Seek(offset, SeekOrigin.Begin);
            var binary = chunk.ToBinary();
            _stream.Write(binary, 0, binary.Length);
            SetChunkOccupatedValue(chunk.Id, true);
        }

        private Record GetRecordFromChunkSequence(Chunk[] chunkSequence)
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

        private Chunk[] ReadChunkSequence(ushort headChunkId)
        {
            Chunk currentChunk = null;
            var result = new List<Chunk>();

            currentChunk = ReadChunk(headChunkId);
            result.Add(currentChunk);

            while (!currentChunk.Flags.HasFlag(ChunkFlags.IsLastRecord) && currentChunk.Continuation > 0)
            {
                currentChunk = ReadChunk(currentChunk.Continuation);
                result.Add(currentChunk);
            }

            return result.ToArray();
        }

        private int GetChunkOffset(ushort recordId)
        {
            var part = recordId/_numberOfRecordsInRecordBlock;
            var offset = part*_fullRecordBlockSzie + _recordMaskSize;

            var recordIndexInRecordBlock = LocalizeChunkId(recordId);

            offset *= recordIndexInRecordBlock * _recordSize;

            return offset;
        }

        private int LocalizeChunkId(ushort recordId)
        {
            var recordIndexInRecordBlock = recordId%_numberOfRecordsInRecordBlock;
            recordIndexInRecordBlock = recordIndexInRecordBlock == 0 ? recordId : recordIndexInRecordBlock;
            return recordIndexInRecordBlock;
        }

        private BitMask GetRecordBlockMask(int blockIndex)
        {
            var offset = _fullRecordBlockSzie*blockIndex;
            if (offset > _stream.Length)
                return null;

            _stream.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[_recordMaskSize];
            _stream.Read(buffer, 0, _recordMaskSize);

            return new BitMask(buffer);
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

            var mask = GetOrCreateBlockMask(_blockMaskStorage.Count);


            return (ushort)mask.GetFirstIndexOf(false);
        }

        private void SetChunkOccupatedValue(ushort chunkId, bool value)
        {
            var localChunkId = LocalizeChunkId(chunkId);
            var blockIndex = chunkId/_numberOfRecordsInRecordBlock;

            var offset = _fullRecordBlockSzie * blockIndex;
            _blockMaskStorage[blockIndex].SetValueOf(localChunkId, value);
            _stream.Seek(offset, SeekOrigin.Begin);
            _stream.Write(_blockMaskStorage[blockIndex].Bytes, 0 ,_blockMaskStorage[blockIndex].Bytes.Length);
        }

        private BitMask GetOrCreateBlockMask(int blockMaskId)
        {
            var blockMask = _blockMaskStorage[blockMaskId];
            if (blockMask != null)
                return blockMask;

            var newBitMask = new byte[_recordMaskSize];
            _stream.Seek(0, SeekOrigin.End);
            _stream.Write(newBitMask, 0, newBitMask.Length);

            return new BitMask(newBitMask);
            
        }

        // fields

        private readonly LazyStorage<int, BitMask> _blockMaskStorage = null;

        private readonly int _recordSize = 1024;
        private readonly int _recordMaskSize = 127;
        private readonly int _fullRecordBlockSzie = 1143;
        private readonly int _numberOfRecordsInRecordBlock = 1016;
        
        private readonly Stream _stream;
    }
}