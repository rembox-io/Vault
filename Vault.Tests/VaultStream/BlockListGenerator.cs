using System.Collections.Generic;
using Vault.Core.Data;

namespace Vault.Tests.VaultStream
{
    public class BlockListGenerator
    {
        public BlockListGenerator Add(ushort index, ushort continuation, int allocated, BlockFlags flags)
        {
            _blocks.Add(new BlockInfo(index, continuation, allocated, flags));
            return this;
        }

        public BlockInfo[] ToArray()
        {
            return _blocks.ToArray();
        }

        private readonly List<BlockInfo> _blocks = new List<BlockInfo>(); 
    }
}
