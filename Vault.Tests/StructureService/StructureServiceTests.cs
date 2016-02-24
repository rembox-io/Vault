using System;
using System.Collections;
using System.Collections.Generic;
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
            blockMask.SetValuesTo(true, 0, 1, 2, 3, 5, 6, 8, 11, 12);

            _memoryStream = new MemoryStream();
            _memoryStream.Write(new byte[Core.Data.StructureService._recordsBlockSize], 0, Core.Data.StructureService._recordsBlockSize);

            _memoryStream.Seek(0, SeekOrigin.Begin);
            _memoryStream.Write(blockMask.Bytes, 0, blockMask.Bytes.Length);

            InternalWriteChunk(0, 1, ChunkFlags.IsFirstChunk, Gc.P1(prefix: GetRecordPrefix(0, RecordFlags.IsDirectory, "first record")));
            InternalWriteChunk(1, 2, ChunkFlags.None, Gc.P2());
            InternalWriteChunk(2, 3, ChunkFlags.None, Gc.P2());
            InternalWriteChunk(3, 0, ChunkFlags.IsLastChunk, Gc.P3());

            InternalWriteChunk();

            InternalWriteChunk(5, 6, ChunkFlags.IsFirstChunk, Gc.P1(prefix: GetRecordPrefix(5, RecordFlags.IsReference, "second record")));
            InternalWriteChunk(6, 0, ChunkFlags.IsLastChunk, Gc.P3());

            InternalWriteChunk();

            InternalWriteChunk(8, 0, ChunkFlags.IsLastChunk | ChunkFlags.IsFirstChunk, Gc.P2(prefix: GetRecordPrefix(8, RecordFlags.IsReference, "third record")));

            InternalWriteChunk();
            InternalWriteChunk();

            InternalWriteChunk(11, 15, ChunkFlags.IsFirstChunk, Gc.P2(prefix: GetRecordPrefix(11, RecordFlags.IsDirectory, "fourth record")));
            InternalWriteChunk(12, 0, ChunkFlags.IsLastChunk, Gc.P2());


            _service = new Core.Data.StructureService(_memoryStream);

        }

        #region CreateChunkSequenceForRecordBinary

        public static IEnumerable CreateChunkSequenceForRecordBinary_TestCaseSource()
        {
            var content1 = Gc.GetByteBufferFromPattern(Gc.Pattern1, 500, 500);
            var testCase1 = new TestCaseData(content1)
                .SetName("1. Запись меньше одного чанка.")
                .Returns(new[] { new Chunk(4, 0, ChunkFlags.IsFirstChunk | ChunkFlags.IsLastChunk, content1) });

            var content2 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize, Chunk.MaxContentSize);
            var testCase2 = new TestCaseData(content2)
                .SetName("2. Запись ровно одного чатка.")
                .Returns(new[] { new Chunk(4, 0, ChunkFlags.IsFirstChunk | ChunkFlags.IsLastChunk, content2) });

            var content3 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize * 2, Chunk.MaxContentSize * 2);
            var testCase3 = new TestCaseData(content3)
                .SetName("3. Запись ровно двух чанков.")
                .Returns(new[]
                {
                    new Chunk(4, 7, ChunkFlags.IsFirstChunk, content3.Split(Chunk.MaxContentSize)[0]),
                    new Chunk(7, 0, ChunkFlags.IsLastChunk, content3.Split(Chunk.MaxContentSize)[1])
                });

            var content4 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize * 3, Chunk.MaxContentSize * 3);
            var testCase4 = new TestCaseData(content4)
                .SetName("4. Запись ровно трех чанков.")
                .Returns(new[]
                {
                    new Chunk(4, 7, ChunkFlags.IsFirstChunk, content4.Split(Chunk.MaxContentSize)[0]),
                    new Chunk(7, 9, ChunkFlags.None, content4.Split(Chunk.MaxContentSize)[1]),
                    new Chunk(9, 0, ChunkFlags.IsLastChunk, content4.Split(Chunk.MaxContentSize)[2])
                });

            var content5 = Gc.GetByteBufferFromPattern(Gc.Pattern1, Chunk.MaxContentSize * 3 - 10,
                Chunk.MaxContentSize * 3 - 10);
            var testCase5 = new TestCaseData(content5)
                .SetName("5. Запись чуть меньше трех чанков.")
                .Returns(new[]
                {
                    new Chunk(4, 7, ChunkFlags.IsFirstChunk, content5.Split(Chunk.MaxContentSize)[0]),
                    new Chunk(7, 9, ChunkFlags.None, content5.Split(Chunk.MaxContentSize)[1]),
                    new Chunk(9, 0, ChunkFlags.IsLastChunk, content5.Split(Chunk.MaxContentSize)[2])
                });


            return new[] { testCase1, testCase2, testCase3, testCase4, testCase5 };
        }

        [Test, TestCaseSource(typeof(StructureServiceTests), nameof(CreateChunkSequenceForRecordBinary_TestCaseSource))
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
                        Gc.P2(Chunk.MaxContentSize, GetRecordPrefix(8,RecordFlags.IsReference, "third record")))),

                new TestCaseData((ushort) 10)
                    .SetName("6. Чтение ни разу не аллоцированного чатнка.")
                    .Throws(typeof (VaultException)),

                new TestCaseData((ushort) 1020)
                    .SetName("7. Чтение ни разу не аллоцированного чатнка.")
                    .Throws(typeof (VaultException)),
            };
        }

        [Test, TestCaseSource(typeof(StructureServiceTests), nameof(ReadChunk_TestCaseSource))]
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
                    new Chunk(5, 6, ChunkFlags.IsFirstChunk, Gc.P1(Chunk.MaxContentSize, GetRecordPrefix(5,RecordFlags.IsReference, "second record"))),
                    new Chunk(6, 0, ChunkFlags.IsLastChunk, Gc.P3(Chunk.MaxContentSize))
                }),

                new TestCaseData((ushort)8)
                .SetName("3. Собираем запись из одного чанка.")
                .Returns(new[]
                {
                    new Chunk(8, 0, ChunkFlags.IsLastChunk | ChunkFlags.IsFirstChunk, Gc.P2(Chunk.MaxContentSize, GetRecordPrefix(8,RecordFlags.IsReference, "third record")))
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


            return new[] { testCaseData1, testCaseData2, testCaseData3, testCaseData4 };
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
            var prefixCase1 = GetRecordPrefix(0, RecordFlags.IsDirectory, "first record");
            var firstChunkContent1 = Gc.P1(Chunk.MaxContentSize, prefixCase1).Skip(prefixCase1.Length).ToArray();
            var content1 = ArrayExtentions.Join(firstChunkContent1, Gc.P2(), Gc.P2(), Gc.P3());
            var case1 = new TestCaseData(0)
                .Returns(new Record(0, "first record", RecordFlags.IsDirectory, content1))
                .SetName("1. Read first record with with 4 chunks.");

            var prefixCase2 = GetRecordPrefix(5, RecordFlags.IsReference, "second record");
            var firstChunkContent2 = Gc.P1(prefix: prefixCase2).Skip(prefixCase2.Length).ToArray();
            var content2 = ArrayExtentions.Join(firstChunkContent2, Gc.P3());
            var case2 = new TestCaseData(5)
                .Returns(new Record(5, "second record", RecordFlags.IsReference, content2))
                .SetName("2. Read second record with with 2 chunks.");

            var prefixCase3 = GetRecordPrefix(8, RecordFlags.IsReference, "third record");
            var firstChunkContent3 = Gc.P2(prefix: prefixCase3).Skip(prefixCase3.Length).ToArray();
            var content3 = ArrayExtentions.Join(firstChunkContent3);
            var case3 = new TestCaseData(8)
                .Returns(new Record(8, "third record", RecordFlags.IsReference, content3))
                .SetName("3. Read third record with with 1 chunk.");

            var case4 = new TestCaseData(2)
                .Throws(typeof(VaultException))
                .SetName("4. Can't read record form chunk, wich not marked as IsFirstChunk.");

            var case5 = new TestCaseData(11)
                .Throws(typeof(VaultException))
                .SetName("5. Can't read corrupted record.");

            return new[] { case1, case2, case3, case4, case5 };
        }

        [Test, TestCaseSource(typeof(StructureServiceTests), nameof(ReadRecord_TestCaseSource))]
        public Record ReadRecord(int startRecordChunkId)
        {
            var result = _service.ReadRecord((ushort)startRecordChunkId);
            return result;
        }

        #endregion

        #region WriteRecord

        public static IEnumerable WriteRecord_TestCaseSource()
        {
            var blockMask = new BitMask(new byte[127]);
            blockMask.SetValuesTo(true, 0, 1, 2, 3, 4, 5, 6, 8, 11, 12);

            var recordPrefix1 = GetRecordPrefix(0, RecordFlags.IsDirectory, "first new record name");
            var record1 = new Record(0, "first new record name", RecordFlags.IsDirectory, Gc.P1(100));
            var writeRecordTestResult1 = new WriteRecordTestResult();
            writeRecordTestResult1.ResultRecordId = 4;
            writeRecordTestResult1.Masks = new Dictionary<int, BitMask> {{0, blockMask}};
            writeRecordTestResult1.AddChunkToCompare(new Chunk(4, 0, ChunkFlags.IsFirstChunk | ChunkFlags.IsLastChunk, Gc.P1(100, recordPrefix1, 126).ToArray()));

            var case1 = new TestCaseData(record1, new [] {4}, (Action<Core.Data.StructureService>)(p => { }))
                .SetName("1. Write record from one chunk on first avialable place.")
                .Returns(writeRecordTestResult1);

            return new[] {case1};
        }

        [Test, TestCaseSource(typeof(StructureServiceTests), nameof(WriteRecord_TestCaseSource))]
        public WriteRecordTestResult WriteRecord(Record record, int[] chunksToCompare, Action<Core.Data.StructureService> prepareAction)
        {
            prepareAction(_service);

            var result = new WriteRecordTestResult();


            result.ResultRecordId = _service.WriteRecord(record);
            result.ComparatedChunks = chunksToCompare.Select(id => _service.ReadChunk((ushort) id)).ToList();
            result.Masks = _service.Masks;

            return result;
        }

        public class WriteRecordTestResult
        {
            public WriteRecordTestResult()
            {
                ComparatedChunks = new List<Chunk>();
            }

            public int ResultRecordId { get; set; }

            public Dictionary<int,BitMask> Masks { get; set; }

            public List<Chunk> ComparatedChunks { get; set; }

            public void AddChunkToCompare(Chunk chunk)
            {
                ComparatedChunks.Add(chunk);
            }

            public override bool Equals(object obj)
            {
                var result = obj as WriteRecordTestResult;
                if (result == null)
                    return false;
                return Equals(result);
            }

            public bool Equals(WriteRecordTestResult result)
            {
                if (result.ResultRecordId != ResultRecordId)
                    return false;

                foreach (var key in Masks.Keys)
                {
                    var mask = Masks[key];
                    if (!mask.Bytes.SequenceEqual(result.Masks[key].Bytes))
                        return false;
                }

                foreach (var chunk in ComparatedChunks)
                {
                    var expectedChunk = result.ComparatedChunks.SingleOrDefault(p => p.Id == chunk.Id);
                    if (!chunk.Equals(expectedChunk))
                        return false;
                }

                return true;
            }
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
                chunkArray = new[] { chunk };
            }
            else
            {
                chunkArray = new Chunk[chunkContent.Length];
                for (ushort i = 0; i < chunkArray.Length; i++)
                {
                    var chunk = new Chunk();
                    chunk.Id = (ushort)(i + 4);
                    chunk.Continuation = (ushort)(i == chunkArray.Length - 1 ? 0 : chunk.Id + 1);
                    if (i == 0)
                        chunk.Flags = ChunkFlags.IsFirstChunk;
                    if (i == chunkArray.Length - 1)
                        chunk.Flags = ChunkFlags.IsLastChunk;
                    chunk.Content = chunkContent[i];
                    chunkArray[i] = chunk;
                }
            }

            var testCaseData = new TestCaseData((object)chunkArray)
                .SetName(testName)
                .Returns(record);

            return testCaseData;
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
    }
}
