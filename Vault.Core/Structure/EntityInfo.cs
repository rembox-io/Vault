using System;

namespace Vault.Core.Structure
{
    public class EntityInfo
    {
        public string Name { get; set; }

        public EntityType Type { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ModefiedAt { get; set; }

        public int Size { get; set; }

        public string[] Files { get; set; }

        public string[] Direcotries { get; set; }
    }

    public enum EntityType
    {
        File = 1,
        Directory = 2
    }
}
