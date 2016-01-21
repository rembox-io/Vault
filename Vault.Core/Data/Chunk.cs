using System;
using System.Diagnostics.Contracts;
using Vault.Core.Tools;

namespace Vault.Core.Data
{
    public class Chunk
    {
        public Chunk()
        {
            
        }

        public Chunk(byte[] buffer)
        {
            Contract.Requires(buffer.Length == FullRecordSize);

            buffer.Read(r =>
            {
                Id = r.ReadUInt16();
                Continuation = r.ReadUInt16();
                Flags = (ChunkFlags)r.ReadByte();
                var contentLength = r.ReadInt16();
                Content = r.ReadBytes(contentLength);
            });
        }

        public ushort Id { get; set; }

        public ushort Continuation { get; set; }

        public ChunkFlags Flags { get; set; }

        public byte[] Content { get; set; }

        public byte[] ToBinary()
        {
            Contract.Requires(Content.Length <= MaxContentSize);
            var buffer = new byte[FullRecordSize];

            buffer.Write(w =>
            {
                w.Write(Id);
                w.Write(Continuation);
                w.Write((byte)Flags);
                w.Write((short)Content.Length);
                w.Write(Content);
            });

            Contract.Ensures(buffer.Length == FullRecordSize);
            return buffer;
        }

        public const ushort FullRecordSize = 1024;
        public const ushort MetadataSize = 7;
        public const ushort ContentSize = FullRecordSize - MetadataSize;
        public const ushort MaxContentSize = FullRecordSize - MetadataSize;
    }

    [Flags]
    public enum ChunkFlags : byte
    {
        IsFirstRecord = 1,
        IsLastRecord = 2
    }
}
