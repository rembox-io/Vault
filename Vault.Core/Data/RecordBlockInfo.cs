namespace Vault.Core.Data
{
    public class RecordBlockInfo
    {
        public int Id { get; set; }

        public int NumberofAllocatedRecords { get; set; }

        public BitMask RecordMask { get; set; }


    }
}