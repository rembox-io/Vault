namespace Vault.Core.Data
{
    public class VaultConfiguration
    {
        public int BlockFullSize { get; set; }
        public int BlockMetadataSize { get; set; }
        public int BlockContentSize => BlockFullSize - BlockMetadataSize;
    }
}
