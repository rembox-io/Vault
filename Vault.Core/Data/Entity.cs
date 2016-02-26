using System;

namespace Vault.Core.Data
{
    public abstract class Entity
    {
        public string Name { get; set; }

        public EntityType Type { get; set; }

        DateTime CreatedAt { get; set; }

        DateTime ModifiedAt { get; set; }
    }

    public enum EntityType
    {
        Directory = 0,
        File = 1
    }
}
