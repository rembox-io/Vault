using System;
using System.Diagnostics.Contracts;
using Vault.Core.Tools;

namespace Vault.Core.Data
{
    internal class Record
    {
        public Record()
        {
            
        }

        public Record(byte[] buffer)
        {
            Contract.Ensures(buffer.Length == FullRecordSize);

            buffer.Read(r =>
            {
                Id = r.ReadUInt16();
                Flags = (RecordFlags)r.ReadByte();
                var contentLength = r.ReadInt16();
                Content = r.ReadBytes(contentLength);
            });
        }

        public ushort Id { get; set; }

        public RecordFlags Flags { get; set; }

        public byte[] Content { get; set; }

        public byte[] ToBinary()
        {
            Contract.Requires(Content.Length <= MaxContentSize);
            var buffer = new byte[FullRecordSize];

            buffer.Write(w =>
            {
                w.Write(Id);
                w.Write((byte)Flags);
                w.Write((short)Content.Length);
                w.Write(Content);
            });

            Contract.Ensures(buffer.Length == FullRecordSize);
            return buffer;
        }

        private const ushort FullRecordSize = 1024;
        private const ushort MetadataSize = 5;
        private const ushort MaxContentSize = FullRecordSize - MetadataSize;
    }

    [Flags]
    public enum RecordFlags : byte
    {
        IsDirectory = 1,
        IsContentAsReference = 2,
        IsFirstRecord = 4,
        IsLastRecord = 8
    }
}
