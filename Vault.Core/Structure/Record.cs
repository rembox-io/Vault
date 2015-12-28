using System;

namespace Vault.Core.Structure
{
    public class Record
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ModefiedAt { get; set; }

        public byte[] Content { get; set; }
    }
}
