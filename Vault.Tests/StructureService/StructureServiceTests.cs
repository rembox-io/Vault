using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Globalization;
using NUnit.Framework;
using System.IO;
using System.Linq;
using EasyAssertions;
using NUnit.Framework.Constraints;
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

            InternalWriteChunk(0, 1, ChunkFlags.IsFirstChunk, Gc.P1(Chunk.MaxContentSize, GetRecordPrefix(0,RecordFlags.IsDirectory, "first record")));
            InternalWriteChunk(1, 2, ChunkFlags.None, Gc.P2(Chunk.MaxContentSize));
            InternalWriteChunk(2, 3, ChunkFlags.None, Gc.P2(Chunk.MaxContentSize));
            InternalWriteChunk(3, 0, ChunkFlags.IsLastChunk, Gc.P3(Chunk.MaxContentSize));

            InternalWriteChunk();

            InternalWriteChunk(5, 6, ChunkFlags.IsFirstChunk, Gc.P1(Chunk.MaxContentSize, GetRecordPrefix(5, RecordFlags.IsDirectory, "second record")));
            InternalWriteChunk(6, 0, ChunkFlags.IsLastChunk, Gc.P3(Chunk.MaxContentSize));

            InternalWriteChunk();

            InternalWriteChunk(8, 0, ChunkFlags.IsLastChunk | ChunkFlags.IsFirstChunk, Gc.P2(Chunk.MaxContentSize, GetRecordPrefix(8, RecordFlags.IsDirectory, "third record")));

            InternalWriteChunk();
            InternalWriteChunk();

            InternalWriteChunk(11, 12, ChunkFlags.IsFirstChunk, Gc.P2(Chunk.MaxContentSize, GetRecordPrefix(11, RecordFlags.IsDirectory, "fourth record")));
            InternalWriteChunk(12, 0, ChunkFlags.IsLastChunk, Gc.P2(Chunk.MaxContentSize));


            _service = new Core.Data.StructureService(_memoryStream);

        }

        #region CreateChunkSequenceForRecordBinary

        public static IEnumerable CreateChunkSequenceForRecordBinary_TestCaseSource()
        {
            var content1 = Gc.GetByteBufferFromPattern(Gc.Pattern1, 500, 500);
            var testCase1 = new TestCaseData(content1)
                .SetName("1. Запись меньше одного чанка.")
                .Returns(new[] {new Chunk(4, 0, ChunkFlags.IsFirstChunk | ChunkFlags.IsLastChunk, content1)});

            var content2 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize, Chunk.MaxContentSize);
            var testCase2 = new TestCaseData(content2)
                .SetName("2. Запись ровно одного чатка.")
                .Returns(new[] {new Chunk(4, 0, ChunkFlags.IsFirstChunk | ChunkFlags.IsLastChunk, content2)});

            var content3 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize*2, Chunk.MaxContentSize*2);
            var testCase3 = new TestCaseData(content3)
                .SetName("3. Запись ровно двух чанков.")
                .Returns(new[]
                {
                    new Chunk(4, 7, ChunkFlags.IsFirstChunk, content3.Split(Chunk.MaxContentSize)[0]),
                    new Chunk(7, 0, ChunkFlags.IsLastChunk, content3.Split(Chunk.MaxContentSize)[1])
                });

            var content4 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize*3, Chunk.MaxContentSize*3);
            var testCase4 = new TestCaseData(content4)
                .SetName("4. Запись ровно трех чанков.")
                .Returns(new[]
                {
                    new Chunk(4, 7, ChunkFlags.IsFirstChunk, content4.Split(Chunk.MaxContentSize)[0]),
                    new Chunk(7, 9, ChunkFlags.None, content4.Split(Chunk.MaxContentSize)[1]),
                    new Chunk(9, 0, ChunkFlags.IsLastChunk, content4.Split(Chunk.MaxContentSize)[2])
                });

            var content5 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize*3 - 10,
                Chunk.MaxContentSize*3 - 10);
            var testCase5 = new TestCaseData(content5)
                .SetName("5. Запись чуть меньше трех чанков.")
                .Returns(new[]
                {
                    new Chunk(4, 7, ChunkFlags.IsFirstChunk, content5.Split(Chunk.MaxContentSize)[0]),
                    new Chunk(7, 9, ChunkFlags.None, content5.Split(Chunk.MaxContentSize)[1]),
                    new Chunk(9, 0, ChunkFlags.IsLastChunk, content5.Split(Chunk.MaxContentSize)[2])
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
                    .Returns(new Chunk(0, 1, ChunkFlags.IsFirstChunk, Gc.P1(Chunk.MaxContentSize, GetRecordPrefix(0,RecordFlags.IsDirectory, "first record")))),

                new TestCaseData((ushort) 1)
                    .SetName("2. Чтение второго чанка из записи.")
                    .Returns(new Chunk(1, 2, ChunkFlags.None, Gc.P2(Chunk.MaxContentSize))),

                new TestCaseData((ushort) 3)
                    .SetName("3. Чтение последнего чанка из записи.")
                    .Returns(new Chunk(3, 0, ChunkFlags.IsLastChunk, Gc.P3(Chunk.MaxContentSize))),

                new TestCaseData((ushort) 4)
                    .SetName("4. Нельзя прочитать удаленный чанк.")
                    .Throws(typeof (VaultException)),

                new TestCaseData((ushort) 8)
                    .SetName("5. Чтение одиночного чатнка.")
                    .Returns(new Chunk(8, 0, ChunkFlags.IsFirstChunk | ChunkFlags.IsLastChunk,
                        Gc.P2(Chunk.MaxContentSize, GetRecordPrefix(8,RecordFlags.IsDirectory, "third record")))),

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
                new TestCaseData(new Chunk(1, 0, ChunkFlags.IsLastChunk, Gc.P2(Chunk.MaxContentSize)))
                .SetName("1. Запись внутри аллоцированной памяти в потоке MFT."),

                new TestCaseData(new Chunk(51, 0, ChunkFlags.IsLastChunk, Gc.P1(Chunk.MaxContentSize)))
                .SetName("2. Запись вне аллоцированной памяти в потоке MFT."),

                new TestCaseData(new Chunk(1020, 0, ChunkFlags.IsLastChunk, Gc.P3(Chunk.MaxContentSize)))
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
                    new Chunk(0, 1, ChunkFlags.IsFirstChunk, Gc.P1(Chunk.MaxContentSize, GetRecordPrefix(0,RecordFlags.IsDirectory, "first record"))),
                    new Chunk(1, 2, ChunkFlags.None, Gc.P2(Chunk.MaxContentSize)),
                    new Chunk(2, 3, ChunkFlags.None, Gc.P2(Chunk.MaxContentSize)),
                    new Chunk(3, 0, ChunkFlags.IsLastChunk, Gc.P3(Chunk.MaxContentSize))
                }), 

                new TestCaseData((ushort)5)
                .SetName("2. Собираем запись из 2 чанков.")
                .Returns(new[]
                {
                    new Chunk(5, 6, ChunkFlags.IsFirstChunk, Gc.P1(Chunk.MaxContentSize, GetRecordPrefix(5,RecordFlags.IsDirectory, "second record"))),
                    new Chunk(6, 0, ChunkFlags.IsLastChunk, Gc.P3(Chunk.MaxContentSize))
                }), 

                new TestCaseData((ushort)8)
                .SetName("3. Собираем запись из одного чанка.")
                .Returns(new[]
                {
                    new Chunk(8, 0, ChunkFlags.IsLastChunk | ChunkFlags.IsFirstChunk, Gc.P2(Chunk.MaxContentSize, GetRecordPrefix(8,RecordFlags.IsDirectory, "third record")))
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

        #region GetRecordFromChunkSequence

        public static IEnumerable GetRecordFromChunkSequence_TestCaseSource()
        {
            var shortRecordName = "Short record name oo";
            var largeRecordName = string.Join(" ", Enumerable.Repeat(1, 52).Select(p => $"Short record name {p}"));
            var shortRecordContent = Gc.P1(50);
            var largeRecordContent = Gc.P1(2010);


            var testCaseData1 = GetTestCaseDataForGetREcordFromChunkSequence(shortRecordName, shortRecordContent, "Short name and short content. One Chunk.");
            var testCaseData2 = GetTestCaseDataForGetREcordFromChunkSequence(largeRecordName, shortRecordContent, "Large name and short content. Two Chunks.");
            var testCaseData3 = GetTestCaseDataForGetREcordFromChunkSequence(shortRecordName, largeRecordContent, "Short name and Large content. Two Chunks.");
            var testCaseData4 = GetTestCaseDataForGetREcordFromChunkSequence(largeRecordName, largeRecordContent, "Large name and large content. Four Chunks.");


            return new[] {testCaseData1, testCaseData2, testCaseData3, testCaseData4 };
        }

        [Test, TestCaseSource(typeof(StructureServiceTests), nameof(GetRecordFromChunkSequence_TestCaseSource))]
        public Record GetRecordFromChunkSequence(Chunk[] chunks)
        {
            var result = _service.GetRecordFromChunkSequence(chunks);
            return result;
        }

        #endregion

        #region GetChunkOffset

        public static IEnumerable GetChunkOffset_TestCaseSource()
        {
            return new[]
            {
                new TestCaseData(0)
                .SetName("First chunk of first RecordBlock.")
                .Returns(127),
                
                new TestCaseData(1)
                .SetName("Second chunk of first RecordBlock.")
                .Returns(1151),

                new TestCaseData(2)
                .SetName("Third chunk of first RecordBlock.")
                .Returns(2175),

                new TestCaseData(1015)
                .SetName("Last chunk of first RecordBlock.")
                .Returns(1039487),

                new TestCaseData(1016)
                .SetName("First chunk of second RecordBlock.")
                .Returns(1040638),

                new TestCaseData(2031)
                .SetName("Last chunk of second RecordBlock.")
                .Returns(2079998),

                new TestCaseData(2032)
                .SetName("First chunk of third RecordBlock.")
                .Returns(2081149),
                
            };
        }

        [Test, TestCaseSource(typeof(StructureServiceTests), nameof(GetChunkOffset_TestCaseSource))]
        public int GetChunkOffset(int recordId)
        {
            return _service.GetChunkOffset((ushort)recordId);
        }

        #endregion

        #region ReadRecord

        public static IEnumerable ReadRecord_TestCaseSource()
        {
            var case1 = new TestCaseData();
            return new[] {case1};
        }

        [Test, TestCaseSource(typeof(StructureServiceTests), nameof(ReadRecord_TestCaseSource))]
        public Record ReadRecord(int startRecordChunkId)
        {
            return _service.ReadRecord((ushort)startRecordChunkId);
        }

        #endregion

        // stuff

        private static TestCaseData GetTestCaseDataForGetREcordFromChunkSequence(string recordName, byte[] recordContent, string testName)
        {
            var record = new Record(5, recordName, RecordFlags.IsDirectory, recordContent);
            var binaryRrecord = record.ToBinary();
            var chunkContent = binaryRrecord.Split(Chunk.MaxContentSize, true);

            Chunk[] chunkArray;
            if (chunkContent.Length == 1)
            {
                var chunk = new Chunk(4, 0, ChunkFlags.IsFirstChunk | ChunkFlags.IsLastChunk, chunkContent.First());
                chunkArray = new[] {chunk};
            }
            else
            {
                chunkArray = new Chunk[chunkContent.Length];
                for (ushort i = 0; i < chunkArray.Length; i++)
                {
                    var chunk = new Chunk();
                    chunk.Id = (ushort)(i + 4);
                    chunk.Continuation = (ushort)(i == chunkArray.Length - 1 ? 0 : chunk.Id + 1);
                    if(i == 0)
                        chunk.Flags = ChunkFlags.IsFirstChunk;
                    if (i == chunkArray.Length - 1)
                        chunk.Flags = ChunkFlags.IsLastChunk;
                    chunk.Content = chunkContent[i];
                    chunkArray[i] = chunk;
                }
            }

            var testCaseData = new TestCaseData((object) chunkArray)
                .SetName(testName)
                .Returns(record);

            return testCaseData;
        }

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

        private static byte[] GetRecordPrefix(ushort recordId, RecordFlags flags, string recordName)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(recordId);
                    writer.Write((byte)flags);
                    writer.WriteString2(recordName);

                    return stream.ToArray();
                }
            }
        }

        // fields

        private MemoryStream _memoryStream;
        private Core.Data.StructureService _service;
        private readonly byte[] _recordNameAsBinary = "record name".AsBinaryWithPrefix();
    }
}
