using System;
using System.Diagnostics.Contracts;
using System.Linq;
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


        public override bool Equals(object obj)
        {
            var record = obj as Record;
            if (obj == null)
                return false;
            return Equals(record);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id.GetHashCode();
                hashCode = (hashCode*397) ^ (int) Flags;
                hashCode = (hashCode*397) ^ (Name?.GetHashCode() ?? 0);
                hashCode = (hashCode*397) ^ (Content?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        public bool Equals(Record record)
        {
            return Id == record.Id
                   && Flags == record.Flags
                   && Name == record.Name
                   && Content.SequenceEqual(record.Content);
        }


        private const int MetadataSize = 5;
        private const int MinimumRecordRawContentSize = 6;
    }

    [Flags]
    public enum RecordFlags
    {
        IsDirectory = 1,
        IsReference = 2
    }
}