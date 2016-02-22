using System;
using System.Diagnostics.Contracts;
using System.Text;
using Vault.Core.Tools;

namespace Vault.Core.Data
{
    public class Record
    {
        public Record()
        {
            
        }

        public Record(byte[] binary)
        {
            Contract.Requires(binary != null);
            Contract.Requires(binary.Length > MinimumRecordRawContentSize);

            binary.Read(r =>
            {
                Id = r.ReadUInt16();
                Flags = (RecordFlags) r.ReadByte();
                Name = r.ReadString2();
                Content = r.ReadBytes(binary.Length - MetadataSize - Encoding.UTF8.GetByteCount(Name));
            });
        }

        public Record(ushort id, string name, RecordFlags flags, byte[] content)
        {
            Id = id;
            Name = name;
            Flags = flags;
            Content = content;
        }

        public ushort Id { get; set; }

        public RecordFlags Flags { get; set; }

        public string Name { get; set; }

        public byte[] Content { get; set; }

        public byte[] ToBinary()
        {
            var size = MetadataSize + Encoding.UTF8.GetByteCount(Name) + Content.Length;
            var buffer = new byte[size];
            buffer.Write(w =>
            {
                w.Write(Id);
                w.Write((byte)Flags);
                w.WriteString2(Name);
                w.Write(Content);
            });

            Contract.Ensures(buffer != null);
            Contract.Ensures(buffer.Length > MinimumRecordRawContentSize);
            return buffer;
        }

        private const int MetadataSize = 3;
        private const int MinimumRecordRawContentSize = 6;
    }

    [Flags]
    public enum RecordFlags
    {
        IsDirectory = 1,
        IsReference = 2
    }
}