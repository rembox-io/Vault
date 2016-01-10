using System;
using Vault.Core.Tools;

namespace Vault.Core.Data
{
    public class VaultInfo
    {
        public VaultInfo(byte[] bytes, VaultConfiguration configuration)
        {
            if(bytes.Length != configuration.VaultMetadataSize)
                throw new ArgumentException($"Bytes length should be equals configuration.VaultMetadataSize");

            bytes.Read(r =>
            {
                Flags = (VaultInfoFlags)r.ReadByte();
                NumbersOfAllocatedBlocks = r.ReadInt16();
                Mask = new BitMask(r.ReadBytes(configuration.VaultMaskSize));
                Name = r.ReadString2();
            });
        }

        public VaultInfo(string name, VaultInfoFlags flags, BitMask mask, short numbersOfAllocatedBlocks)
        {
            Name = name;
            Flags = flags;
            Mask = mask;
            NumbersOfAllocatedBlocks = numbersOfAllocatedBlocks;
        }

        public string Name { get; set; }
        public short NumbersOfAllocatedBlocks { get; set; }
        public VaultInfoFlags Flags { get; set; }
        public BitMask Mask { get; private set; }

        public byte[] ToBinary(short vaultInfoSize)
        {
            var buffer = new byte[vaultInfoSize];

            buffer.Write(w =>
            {
                w.Write((byte)Flags);
                w.Write(NumbersOfAllocatedBlocks);
                w.Write(Mask.Bytes);
                w.WriteString2(Name);
            });

            return buffer;
        }
    }

    [Flags]
    public enum VaultInfoFlags : byte
    {
        None = 0,
        Encryptable = 1,
        Archivable = 2,
        Versionable = 4
    }
}
