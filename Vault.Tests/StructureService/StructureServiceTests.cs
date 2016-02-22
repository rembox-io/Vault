using System;
using System.Collections;
using System.Globalization;
using NUnit.Framework;
using System.IO;
using System.Linq;
using EasyAssertions;
using Vault.Core.Data;
using Vault.Core.Exceptions;
using Vault.Core.Tools;

namespace Vault.Tests.StructureService
{
    [TestFixture]
    public class StructureServiceTests
    {
        [SetUp]
        public void Setup()
        {
            var blockMask = new BitMask(new byte[127]);
            blockMask.SetValuesTo(true, 0, 1, 2, 3, 5, 6, 8);

            _memoryStream = new MemoryStream();
            _memoryStream.Write(new byte[Core.Data.StructureService._recordsBlockSize], 0, Core.Data.StructureService._recordsBlockSize );

            _memoryStream.Seek(0, SeekOrigin.Begin);
            _memoryStream.Write(blockMask.Bytes, 0, blockMask.Bytes.Length);

            InternalWriteChunk(0, 1, ChunkFlags.IsFirstRecord, Gc.P1(Chunk.MaxContentSize));
            InternalWriteChunk(1, 2, ChunkFlags.None, Gc.P2(Chunk.MaxContentSize));
            InternalWriteChunk(2, 3, ChunkFlags.None, Gc.P2(Chunk.MaxContentSize));
            InternalWriteChunk(3, 0, ChunkFlags.IsLastRecord, Gc.P3(Chunk.MaxContentSize));

            InternalWriteChunk();

            InternalWriteChunk(5, 6, ChunkFlags.IsFirstRecord, Gc.P1(Chunk.MaxContentSize));
            InternalWriteChunk(6, 0, ChunkFlags.IsLastRecord, Gc.P3(Chunk.MaxContentSize));

            InternalWriteChunk();

            InternalWriteChunk(8, 0, ChunkFlags.IsLastRecord | ChunkFlags.IsFirstRecord, Gc.P2(Chunk.MaxContentSize));

            InternalWriteChunk();
            InternalWriteChunk();

            InternalWriteChunk(11, 0, ChunkFlags.IsLastRecord | ChunkFlags.IsFirstRecord, Gc.P2(Chunk.MaxContentSize));
            InternalWriteChunk(12, 0, ChunkFlags.IsLastRecord | ChunkFlags.None, Gc.P2(Chunk.MaxContentSize));


            _service = new Core.Data.StructureService(_memoryStream);

        }

        #region CreateChunkSequenceForRecordBinary

        public static IEnumerable CreateChunkSequenceForRecordBinary_TestCaseSource()
        {
            var content1 = Gc.GetByteBufferFromPattern(Gc.Pattern1, 500, 500);
            var testCase1 = new TestCaseData(content1)
                .SetName("1. Запись меньше одного чанка.")
                .Returns(new[] {new Chunk(4, 0, ChunkFlags.IsFirstRecord | ChunkFlags.IsLastRecord, content1)});

            var content2 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize, Chunk.MaxContentSize);
            var testCase2 = new TestCaseData(content2)
                .SetName("2. Запись ровно одного чатка.")
                .Returns(new[] {new Chunk(4, 0, ChunkFlags.IsFirstRecord | ChunkFlags.IsLastRecord, content2)});

            var content3 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize*2, Chunk.MaxContentSize*2);
            var testCase3 = new TestCaseData(content3)
                .SetName("3. Запись ровно двух чанков.")
                .Returns(new[]
                {
                    new Chunk(4, 0, ChunkFlags.IsFirstRecord, content3.Split(Chunk.MaxContentSize)[0]),
                    new Chunk(4, 0, ChunkFlags.IsFirstRecord, content3.Split(Chunk.MaxContentSize)[1])
                });

            var content4 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize*3, Chunk.MaxContentSize*3);
            var testCase4 = new TestCaseData(content4)
                .SetName("4. Запись ровно трех чанков.")
                .Returns(new[]
                {
                    new Chunk(4, 0, ChunkFlags.IsFirstRecord, content4.Split(Chunk.MaxContentSize)[0]),
                    new Chunk(4, 0, ChunkFlags.None, content4.Split(Chunk.MaxContentSize)[1]),
                    new Chunk(4, 0, ChunkFlags.IsFirstRecord, content4.Split(Chunk.MaxContentSize)[2])
                });

            var content5 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize*3 - 10,
                Chunk.MaxContentSize*3 - 10);
            var testCase5 = new TestCaseData(content5)
                .SetName("5. Запись чуть меньше трех чанков.")
                .Returns(new[]
                {
                    new Chunk(4, 0, ChunkFlags.IsFirstRecord, content5.Split(Chunk.MaxContentSize)[0]),
                    new Chunk(4, 0, ChunkFlags.None, content5.Split(Chunk.MaxContentSize)[1]),
                    new Chunk(4, 0, ChunkFlags.IsFirstRecord, content5.Split(Chunk.MaxContentSize)[2])
                });


            return new[] {testCase1, testCase2, testCase3, testCase4, testCase5};
        }

        [Test, TestCaseSource(typeof (StructureServiceTests), nameof(CreateChunkSequenceForRecordBinary_TestCaseSource))
        ]
        public Chunk[] CreateChunkSequenceForRecordBinary(byte[] binary)
        {
            return _service.CreateChunkSequenceForRecordBinary(binary);
        }

        #endregion

        #region ReadChunk

        public static IEnumerable ReadChunk_TestCaseSource()
        {
            return new[]
            {
                new TestCaseData((ushort) 0)
                    .SetName("1. Чтение самого первого чанка.")
                    .Returns(new Chunk(0, 1, ChunkFlags.IsFirstRecord, Gc.P1(Chunk.MaxContentSize))),

                new TestCaseData((ushort) 1)
                    .SetName("2. Чтение второго чанка из записи.")
                    .Returns(new Chunk(1, 2, ChunkFlags.None, Gc.P2(Chunk.MaxContentSize))),

                new TestCaseData((ushort) 3)
                    .SetName("3. Чтение последнего чанка из записи.")
                    .Returns(new Chunk(3, 0, ChunkFlags.IsLastRecord, Gc.P3(Chunk.MaxContentSize))),

                new TestCaseData((ushort) 4)
                    .SetName("4. Нельзя прочитать удаленный чанк.")
                    .Throws(typeof (VaultException)),

                new TestCaseData((ushort) 8)
                    .SetName("5. Чтение одиночного чатнка.")
                    .Returns(new Chunk(8, 0, ChunkFlags.IsFirstRecord | ChunkFlags.IsLastRecord,
                        Gc.P2(Chunk.MaxContentSize))),

                new TestCaseData((ushort) 10)
                    .SetName("6. Чтение ни разу не аллоцированного чатнка.")
                    .Throws(typeof (VaultException)),

                new TestCaseData((ushort) 1020)
                    .SetName("7. Чтение ни разу не аллоцированного чатнка.")
                    .Throws(typeof (VaultException)),
            };
        }

        [Test, TestCaseSource(typeof (StructureServiceTests), nameof(ReadChunk_TestCaseSource))]
        public Chunk ReadChunk(ushort chunkId)
        {
            return _service.ReadChunk(chunkId);
        }

        #endregion

        #region WriteChunks

        public static IEnumerable WriteChunk_TestCaseSource()
        {
            return new[]
            {
                new TestCaseData(new Chunk(1, 0, ChunkFlags.IsLastRecord, Gc.P2(Chunk.MaxContentSize)))
                .SetName("1. Запись внутри аллоцированной памяти в потоке MFT."),

                new TestCaseData(new Chunk(51, 0, ChunkFlags.IsLastRecord, Gc.P1(Chunk.MaxContentSize)))
                .SetName("2. Запись вне аллоцированной памяти в потоке MFT."),

                new TestCaseData(new Chunk(1020, 0, ChunkFlags.IsLastRecord, Gc.P3(Chunk.MaxContentSize)))
                .SetName("3. Запись во втором блоке памяти в потоке MFT."),


            };
        }

        [Test, TestCaseSource(typeof(StructureServiceTests), nameof(WriteChunk_TestCaseSource))]
        public void WriteChunk(Chunk writeingChunk)
        {   
            // act
            _service.WriteChunk(writeingChunk);
            var readedChunk = _service.ReadChunk(writeingChunk.Id);
            
            // assert
            writeingChunk.ShouldBe(readedChunk);
            _service.GetChunkOccupatedValue(writeingChunk.Id).ShouldBe(true);
        }

        #endregion

        #region ReadChunkSequence

        public static IEnumerable ReadChunkSequence_TestCaseSource()
        {
            return new[]
            {
                new TestCaseData((ushort)0)
                .SetName("1. Собираем запись из 4 чанков.")
                .Returns(new[]
                {
                    new Chunk(0, 1, ChunkFlags.IsFirstRecord, Gc.P1(Chunk.MaxContentSize)),
                    new Chunk(1, 2, ChunkFlags.None, Gc.P2(Chunk.MaxContentSize)),
                    new Chunk(2, 3, ChunkFlags.None, Gc.P2(Chunk.MaxContentSize)),
                    new Chunk(3, 0, ChunkFlags.IsLastRecord, Gc.P3(Chunk.MaxContentSize))
                }), 

                new TestCaseData((ushort)5)
                .SetName("2. Собираем запись из 2 чанков.")
                .Returns(new[]
                {
                    new Chunk(5, 6, ChunkFlags.IsFirstRecord, Gc.P1(Chunk.MaxContentSize)),
                    new Chunk(6, 0, ChunkFlags.IsLastRecord, Gc.P3(Chunk.MaxContentSize))
                }), 

                new TestCaseData((ushort)8)
                .SetName("3. Собираем запись из одного чанка.")
                .Returns(new[]
                {
                    new Chunk(8, 0, ChunkFlags.IsLastRecord | ChunkFlags.IsFirstRecord, Gc.P2(Chunk.MaxContentSize))
                }), 

                new TestCaseData((ushort)4)
                .SetName("4. Не может прочетать последовательность с неалоццированого индекса записи.")
                .Throws(typeof(VaultException)), 

                new TestCaseData((ushort)1)
                .SetName("5. Не может прочетать последовательность с не головной записи.")
                .Throws(typeof(VaultException)), 

                new TestCaseData((ushort)1)
                .SetName("6. Не может прочетать последовательность с окончанием не на чанк с флагом IsLastChunk.")
                .Throws(typeof(VaultException)), 

            };
        }

        [Test, TestCaseSource(typeof(StructureServiceTests), nameof(ReadChunkSequence_TestCaseSource))]
        public Chunk[] ReadChunkSequence(ushort headChunkIndex)
        {
            return _service.ReadChunkSequence(headChunkIndex);
        }

        #endregion


        // stuff

        private static byte[] RecordFromNameAndContent(string name, params byte[][] contentArray)
        {
            var resultArrayLength = contentArray.Sum(p => p.Length);
            var buffer = new byte[resultArrayLength];

            buffer.Write(w =>
            {
                w.WriteString2(name);
                foreach (var content in contentArray)
                    w.Write(content);
            });

            return buffer.ToArray();
        }

        private static Record GetRecord(ushort recordId, string name, RecordFlags flags, params byte[][] contentArray)
        {
            var content = ArrayExtentions.Join(contentArray);
            return new Record(recordId, name, flags, content);
        }

        private void InternalWriteChunk()
        {
            _memoryStream.Write(Gc.Empty(Chunk.FullRecordSize), 0, Chunk.FullRecordSize);
        }

        private void InternalWriteChunk(ushort chunkId, ushort continuation, ChunkFlags flags, byte[] content)
        {
            var chunk = new Chunk(chunkId, continuation, flags, content);
            var chunkBinary = chunk.ToBinary();
            _memoryStream.Write(chunkBinary, 0, chunkBinary.Length);
        }

        // fields

        private MemoryStream _memoryStream;
        private Core.Data.StructureService _service;
    }
}
