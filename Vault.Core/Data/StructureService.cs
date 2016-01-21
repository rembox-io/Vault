using System.IO;
using Vault.Core.Exceptions;
using Vault.Core.Tools;

namespace Vault.Core.Data
{
    internal class StructureService 
    {
        public StructureService(Stream stream)
        {
            _stream = stream;
            _blockMaskStorage = new LazyStorage<int, BitMask>(GetRecordBlockMask);
        }

        public Record GetRecordById(ushort recordId)
        {
            var recordAllocated = _blockMaskStorage[recordId/_numberOfRecordsInRecordBlock][LocalizeRecordId(recordId)];
            if (!recordAllocated)
                throw new VaultException();

            var offset = GetRecordOffset(recordId);
            var buffer = new byte[_recordSize];
            _stream.Seek(offset, SeekOrigin.Begin);
            _stream.Read(buffer, 0, _recordSize);

            return new Record(buffer);
        }

        public void WriteRecord(ushort parentRecordId, Record record)
        {
            
        }

        private int GetRecordOffset(ushort recordId)
        {
            var part = recordId/_numberOfRecordsInRecordBlock;
            var offset = part*_fullRecordBlockSzie + _recordMaskSize;

            var recordIndexInRecordBlock = LocalizeRecordId(recordId);

            offset *= recordIndexInRecordBlock * _recordSize;

            return offset;
        }

        private int LocalizeRecordId(ushort recordId)
        {
            var recordIndexInRecordBlock = recordId%_numberOfRecordsInRecordBlock;
            recordIndexInRecordBlock = recordIndexInRecordBlock == 0 ? recordId : recordIndexInRecordBlock;
            return recordIndexInRecordBlock;
        }

        private BitMask GetRecordBlockMask(int blockIndex)
        {
            var offset = _fullRecordBlockSzie*blockIndex;
            _stream.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[_recordMaskSize];
            _stream.Read(buffer, 0, _recordMaskSize);

            return new BitMask(buffer);
        }

        private readonly LazyStorage<int, BitMask> _blockMaskStorage = null;

        private readonly int _recordSize = 1024;
        private readonly int _recordMaskSize = 127;
        private readonly int _fullRecordBlockSzie = 1143;
        private readonly int _numberOfRecordsInRecordBlock = 1016;
        
        private readonly Stream _stream;
    }
}