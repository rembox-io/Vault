using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
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

        public Chunk(ushort id, ushort continuation, ChunkFlags flags, byte[] content)
        {
            Id = id;
            Continuation = continuation;
            Flags = flags;
            Content = content;
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


        public override bool Equals(object obj)
        {
            return Equals(obj as Chunk);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id.GetHashCode();
                hashCode = (hashCode*397) ^ Continuation.GetHashCode();
                hashCode = (hashCode*397) ^ (int) Flags;
                hashCode = (hashCode*397) ^ (Content != null ? Content.GetHashCode() : 0);
                return hashCode;
            }
        }

        public bool Equals(Chunk obj)
        {
            if (obj == null)
                return false;

            var result = obj.Id == Id && obj.Continuation == Continuation && obj.Flags == Flags;
            result = (obj.Content ==null && Content == null) 
                || (obj.Content != null && Content != null && Content.SequenceEqual(obj.Content));

            return result;
        }

        public override string ToString()
        {
            return $"Id:{Id}, Continuation:{Continuation}, Flags:{Flags}, Content:{Content?.Length.ToString() ?? "null"}";
        }

        public const ushort FullRecordSize = 1024;
        public const ushort MetadataSize = 7;
        public const ushort MaxContentSize = FullRecordSize - MetadataSize;
    }

    [Flags]
    public enum ChunkFlags : byte
    {
        None = 0,
        IsFirstRecord = 1,
        IsLastRecord = 2,
    }
}
