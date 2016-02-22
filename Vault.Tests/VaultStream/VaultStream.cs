using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using EasyAssertions;
using NUnit.Framework;
using Vault.Core.Data;
using Vault.Core.Tools;

namespace Vault.Tests.VaultStream
{
    [TestFixture]
    public class VaultStreamTests
    {
        // test initilalize methods

        [SetUp]
        public void TestSetup()
        {
            _backStream = GetVaultStream();
            _stream = new Core.Data.VaultStream(_backStream, 1, VaultConfiguration);
        }

        // tests

        #region VaultStream.Read

        public static IEnumerable Read_TestData()
        {
            var result2 = new byte[VaultConfiguration.BlockContentSize*2].Write(w =>
            {
                w.Write(Gc.GetByteBufferFromPattern(Gc.Pattern1, 55, 55));
                w.Write(Gc.GetByteBufferFromPattern(Gc.Pattern2, 55, 55));
            });

            var result3 = new byte[20].Write(w =>
            {
                w.Write(Gc.GetByteBufferFromPattern(Gc.Pattern1, 5, 5));
                w.Write(Gc.GetByteBufferFromPattern(Gc.Pattern2, 15, 15));
            });

            var result4 = new byte[40].Write(w =>
            {
                w.Write(Gc.GetByteBufferFromPattern(Gc.Pattern2, 5, 5));
                w.Write(Gc.GetByteBufferFromPattern(Gc.Pattern3, 20, 20));
                w.Write(Gc.GetByteBufferFromPattern(new byte[] {0}, 15, 15));
            });

            return new[]
            {
                new TestCaseData(0, 5)
                    .SetName("1. Чтение первых пяти байт")
                    .Returns(Gc.GetByteBufferFromPattern(Gc.Pattern1, 5, 5)),

                new TestCaseData(0, 110)
                    .SetName("2. Чтение первых двух блоков")
                    .Returns(result2),

                new TestCaseData(50, 20)
                    .SetName("3. Чтение конца первого и начала второго блоков.")
                    .Returns(result3),

                new TestCaseData(105, 40)
                    .SetName("4. Чтение конца второго и полностью неполного третьего блока.")
                    .Returns(result4),
            };
        }

        [Test, TestCaseSource(typeof (VaultStreamTests), nameof(Read_TestData))]
        public byte[] Read(int position, int count)
        {
            // affect
            var result = new byte[count];

            // act

            _stream.Position = position;
            _stream.Read(result, 0, count);

            // assert
            return result;
        }

        #endregion

        #region VaultStrream.GetBackRangeForBlock

        public static IEnumerable GetBackRangeForBlock_TestData()
        {
            return new[]
            {
                new TestCaseData(_backStream, 1, VaultConfiguration.BlockContentSize, 0, new Range(0, 5))
                    .SetName("1. Корректный поиск в первом блоке")
                    .Returns(new Range(201, 206)),

                new TestCaseData(_backStream, 1, VaultConfiguration.BlockContentSize, 0, new Range(0, 56))
                    .SetName("2. Выход за пределый правой части границы")
                    .Returns(new Range(201, 256)),

                new TestCaseData(_backStream, 1, VaultConfiguration.BlockContentSize, 0, new Range(0, 55))
                    .SetName("3. Ровно в границах блока")
                    .Returns(new Range(201, 256)),

                new TestCaseData(_backStream, 2, VaultConfiguration.BlockContentSize, 1, new Range(56, 110))
                    .SetName("4. Ровно в границах второго блока")
                    .Returns(new Range(266, 320)),

                new TestCaseData(_backStream, 2, VaultConfiguration.BlockContentSize, 1, new Range(60, 100))
                    .SetName("5. Внутри границ второго блока")
                    .Returns(new Range(270, 310)),

                new TestCaseData(_backStream, 2, VaultConfiguration.BlockContentSize, 1, new Range(60, 120))
                    .SetName("6. Выход за границы второго блока")
                    .Returns(new Range(270, 320)),

                new TestCaseData(_backStream, 3, 20, 2, new Range(110, 130))
                    .SetName("7. Ровно в границах не полного третьего блока")
                    .Returns(new Range(329, 349)),

                new TestCaseData(_backStream, 3, 20, 2, new Range(110, 135))
                    .SetName("8. Выход за границы не полного третьего блока")
                    .Returns(new Range(329, 349)),
            };
        }

        [Test, TestCaseSource(typeof (VaultStreamTests), nameof(GetBackRangeForBlock_TestData))]
        public Range GetBackRangeForBlock(Stream backStream, int blockIndex, int blockAllocated, int localBlockIndex,
            Range upRange)
        {
            // act
            return _stream.GetBackRangeForBlock(blockIndex, blockAllocated, localBlockIndex, upRange);
        }

        #endregion

        #region VaultStream.GetBackStreaamRangesToCopy

        public static IEnumerable GetBackStreaamRangesToCopy_TestData()
        {
            return new[]
            {
                new TestCaseData(0, 5)
                    .SetName("1. Первый блок, один кусок.")
                    .Returns(new[] {new Range(201, 206)}),

                new TestCaseData(0, 60)
                    .SetName("2. Первый и второй блоки. ")
                    .Returns(new[] {new Range(201, 256), new Range(265, 270)}),

                new TestCaseData(20, 70)
                    .SetName("3. Конец первого и начало торого блока. ")
                    .Returns(new[] {new Range(221, 256), new Range(265, 300)}),

                new TestCaseData(120, 20)
                    .SetName("4. Конец первого и начало торого блока. ")
                    .Returns(new[] {new Range(339, 349)}),
            };
        }

        [Test, TestCaseSource(typeof (VaultStreamTests), nameof(GetBackStreaamRangesToCopy_TestData))]
        public Range[] GetBackStreaamRangesToCopy(int position, int count)
        {
            // act
            _stream.Position = position;
            var result = _stream.GetBackStreaamRangesToCopy(count);

            // assert
            return result;
        }

        #endregion

        #region VaultStream.GetNumberOfBlocksForLength

        public static IEnumerable GetNumberOfBlocksForLength_TestData()
        {
            return new[]
            {
                new TestCaseData(0)
                    .SetName("1. При длине 0 - вернуть 0")
                    .Returns(0),

                new TestCaseData(35)
                    .SetName("2. Не полный один блок")
                    .Returns(1),

                new TestCaseData(55)
                    .SetName("3. Ровно один блок")
                    .Returns(1),

                new TestCaseData(75)
                    .SetName("4. Не полные 2 блока.")
                    .Returns(2),

                new TestCaseData(110)
                    .SetName("4. Ровно два блока.")
                    .Returns(2),

                new TestCaseData(545)
                    .SetName("5. Не полные 10 блоков.")
                    .Returns(10),


            };
        }

        [Test, TestCaseSource(typeof (VaultStreamTests), nameof(GetNumberOfBlocksForLength_TestData))]
        public int GetNumberOfBlocksForLength(long length)
        {
            return _stream.GetNumberOfBlocksForLength(length);
        }

        #endregion

        #region VaultStream.AllocateBlocks

        public static IEnumerable AllocateBlocks_TestData()
        {
            // test 1
            var vaultStream1 = new Core.Data.VaultStream(GetVaultStream(), 0, VaultConfiguration);
            var mask1 = GetVaultMask().SetValueTo(4, true).SetValueTo(5, true);
            var vaultInfo1 = new VaultInfo(TestVaultName, TestVaultInfoFlags, mask1, 6);
            var blocks1 = new BlockListGenerator().Add(4, 0, 0, BlockFlags.None).Add(5, 0, 0, BlockFlags.None).ToArray();
            var expectedStreamContent1 = new VaultGenerator()
                .InitializeVault(VaultConfiguration, vaultInfo1)
                .WriteBlockWithPattern(continuation: 2, pattern: Gc.Pattern1)
                .WriteBlockWithPattern(continuation: 3, pattern: Gc.Pattern2, isLastBlock: false, isFirstBlock: false)
                .WriteBlockWithPattern(allocated: 20, pattern: Gc.Pattern3, isLastBlock: true, isFirstBlock: false)
                .WriteBlockWithPattern(allocated: 0, pattern: Gc.PatternEmpty, isLastBlock: false, isFirstBlock: false)
                .WriteBlockWithPattern(allocated: 0, pattern: Gc.PatternEmpty, isLastBlock: false, isFirstBlock: false)
                .GetContentWithoutVaultInfo();              


            return new[]
            {
                new TestCaseData(2, vaultStream1, mask1, blocks1, expectedStreamContent1)
                    .SetName("1. Расширение хранилища двумя блоками."),
            };
        }

        [Test, TestCaseSource(typeof(VaultStreamTests), nameof(AllocateBlocks_TestData))]
        public void AllocateBlocks(int numberOfBlocksToAllocate,
            Core.Data.VaultStream stream,
            Core.Data.BitMask expectedVaultInfo,
            BlockInfo[] expectedBlockInfo,
            byte[] expectedStreamContent)
        {
            // act
            var resultBlocks = stream.AllocateBlocks(numberOfBlocksToAllocate);

            // assert
            var vaultInfo = stream.GetVaultInfo();
            vaultInfo.Mask.Bytes.ShouldMatch(expectedVaultInfo.Bytes);
            resultBlocks.ShouldMatch(expectedBlockInfo);

            var actualStreamContent = ((MemoryStream) stream.BackStream)
                .ToArray()
                .Skip(VaultConfiguration.VaultMetadataSize)
                .ToArray();
            actualStreamContent.ShouldMatch(expectedStreamContent);
        }

        #endregion

        #region VaultStream.ReleaseBlocks

        public static IEnumerable ReleaseBlock_TestData()
        {
            Func<int[], VaultInfo> getVaultInfoWIthoutBlocksInMask = (idList) =>
            {
                var vaultInfo = new VaultInfo("test vault name", VaultInfoFlags.None,
                    new Core.Data.BitMask(new byte[VaultConfiguration.VaultMaskSize]), 4);
                for (int i = 0; i < 4; i++)
                    vaultInfo.Mask[i] = !idList.Contains(i);
                return vaultInfo;
            };

            // result 1
            var vaultInfo1 = getVaultInfoWIthoutBlocksInMask(new[] { 1 });
            var result1 = new VaultGenerator()
                .InitializeVault(VaultConfiguration, vaultInfo1)
                .WriteBlockWithPattern(continuation: 0, allocated: 0, pattern: new byte[] {0}, isFirstBlock: false,
                    isLastBlock: false)
                .WriteBlockWithPattern(continuation: 3, pattern: Gc.Pattern2, isLastBlock: false, isFirstBlock: false)
                .WriteBlockWithPattern(allocated: 20, pattern: Gc.Pattern3, isLastBlock: true, isFirstBlock: false)
                .GetContentWithoutVaultInfo();

            // result 2
            var vaultInfo2 = getVaultInfoWIthoutBlocksInMask(new[] { 1, 2 });
            var result2 = new VaultGenerator()
                .InitializeVault(VaultConfiguration, vaultInfo1)
                .WriteBlockWithPattern(continuation: 0, allocated: 0, pattern: new byte[] { 0 }, isFirstBlock: false,
                    isLastBlock: false)
                .WriteBlockWithPattern(continuation: 0, allocated: 0, pattern: new byte[] { 0 }, isFirstBlock: false,
                    isLastBlock: false)
                .WriteBlockWithPattern(allocated: 20, pattern: Gc.Pattern3, isFirstBlock: false, isLastBlock: true)
                .GetContentWithoutVaultInfo();

            return new[]
            {
                new TestCaseData(new ushort[] {1}, vaultInfo1.Mask.Bytes, result1)
                    .SetName("1. Освобождение блока #1"),
                new TestCaseData(new ushort[] {1, 2}, vaultInfo2.Mask.Bytes, result2)
                    .SetName("2. Освобождение двух блока"),
                new TestCaseData(new ushort[] {4}, vaultInfo1.Mask.Bytes, result1)
                    .SetName("3. Нельзя освободить несуществующий блок")
                    .Throws(typeof (ArgumentException)),
            };
        }

        [Test, TestCaseSource(typeof(VaultStreamTests), nameof(ReleaseBlock_TestData))]
        public void ReleaseBlock(ushort[] blocksToRelease, byte[] resultBitMask, byte[] blocksContent)
        {
            // act
            _stream.ReleaseBlocks(blocksToRelease);

            // assert prepeare
            var vaultInfo = _stream.GetVaultInfo();
            _backStream.Seek(VaultConfiguration.VaultMetadataSize, SeekOrigin.Begin);
            var buffer = new byte[VaultConfiguration.BlockFullSize * vaultInfo.NumbersOfAllocatedBlocks];
            _backStream.Read(buffer, 0, buffer.Length);

            // assert
            vaultInfo.Mask.Bytes.ShouldMatch(resultBitMask);
            buffer.ShouldMatch(blocksContent);
        }

        #endregion

        #region VaultStream.GetLength And SetLength

        [Test]
        public void GetLength()
        {
            // act
            var length = _stream.Length;

            // assert
            length.ShouldBe(130L);
        }

        public static IEnumerable SetLength_TestData()
        {
            return new[]
            {
                new TestCaseData(55)
                    .SetName("1. Сократили до одного блока.")
                    .Returns(new[] {new BlockInfo(1, 0, 55, BlockFlags.IsFirstBlock | BlockFlags.IsLastBlock)}),

                new TestCaseData(15)
                    .SetName("2. Сократили до не полного одного блока.")
                    .Returns(new[] {new BlockInfo(1, 0, 15, BlockFlags.IsFirstBlock | BlockFlags.IsLastBlock)}),

                new TestCaseData(75)
                    .SetName("3. Сократили до не полных двух блоков.")
                    .Returns(new[]
                    {new BlockInfo(1, 2, 55, BlockFlags.IsFirstBlock), new BlockInfo(2, 0, 20, BlockFlags.IsLastBlock)}),

                new TestCaseData(120)
                    .SetName("3. Сократили до не полных трех блоков.")
                    .Returns(new[]
                    {
                        new BlockInfo(1, 2, 55, BlockFlags.IsFirstBlock), new BlockInfo(2, 3, 55, BlockFlags.None),
                        new BlockInfo(3, 0, 10, BlockFlags.IsLastBlock)
                    }),

                new TestCaseData(170)
                    .SetName("4. Расширили до неполных 4 блоков.")
                    .Returns(new BlockListGenerator()
                        .Add(1, 2, 55, BlockFlags.IsFirstBlock)
                        .Add(2, 3, 55, BlockFlags.None)
                        .Add(3, 4, 55, BlockFlags.None)
                        .Add(4, 0, 5, BlockFlags.IsLastBlock).ToArray()),

                new TestCaseData(275)
                    .SetName("4. Расширили до полнных 5 блоков.")
                    .Returns(new BlockListGenerator()
                        .Add(1, 2, 55, BlockFlags.IsFirstBlock)
                        .Add(2, 3, 55, BlockFlags.None)
                        .Add(3, 4, 55, BlockFlags.None)
                        .Add(4, 5, 55, BlockFlags.None)
                        .Add(5, 0, 55, BlockFlags.IsLastBlock)
                        .ToArray())

            };
        }

        [Test, TestCaseSource(typeof(VaultStreamTests), nameof(SetLength_TestData))]
        public BlockInfo[] SetLength(long newLengthValue)
        {
            // act
            _stream.SetLength(newLengthValue);

            // assert
            _stream.Length.ShouldBe(newLengthValue);
            var result = _stream.Blocks;
            return result;
        }

        #endregion

        #region VaultStream.Write

        public static IEnumerable Write_TestData()
        {
            Func<VaultInfo> vaultInfo = () => new VaultInfo(TestVaultName, TestVaultInfoFlags, GetVaultMask(), 4);

            // result 1
            var block1Content = Gc.GetByteBufferFromPattern(Gc.Pattern1, VaultConfiguration.BlockContentSize, VaultConfiguration.BlockContentSize);
            var localPattern = new byte[] {101, 102, 103, 104, 105};
            Array.Copy(localPattern, 0, block1Content, 0, 5);

            var stream1 = new VaultGenerator()
                .InitializeVault(VaultConfiguration, vaultInfo())
                .WriteBlockWithContent(continuation: 2, flags: BlockFlags.IsFirstBlock, content: block1Content)
                .WriteBlockWithPattern(continuation: 3, pattern: Gc.Pattern2, isFirstBlock: false)
                .WriteBlockWithPattern(allocated: 20, pattern: Gc.Pattern3, isFirstBlock: false)
                .GetStream();

            // result 2
            var block2Content = Gc.GetByteBufferFromPattern(localPattern, VaultConfiguration.BlockContentSize, VaultConfiguration.BlockContentSize);
            var stream2 = new VaultGenerator()
                .InitializeVault(VaultConfiguration, vaultInfo())
                .WriteBlockWithContent(continuation: 2, flags: BlockFlags.IsFirstBlock, content: block2Content)
                .WriteBlockWithPattern(continuation: 3, pattern: Gc.Pattern2, isFirstBlock: false)
                .WriteBlockWithPattern(allocated: 20, pattern: Gc.Pattern3, isFirstBlock: false)
                .GetStream();

            // result 3
            var writeContent3 = Gc.GetByteBufferFromPattern(localPattern, VaultConfiguration.BlockContentSize * 2, VaultConfiguration.BlockContentSize * 2);
            var singleBlock3 = Gc.GetByteBufferFromPattern(localPattern, VaultConfiguration.BlockContentSize, VaultConfiguration.BlockContentSize);
            var stream3 = new VaultGenerator()
                .InitializeVault(VaultConfiguration, vaultInfo())
                .WriteBlockWithContent(continuation: 2, flags: BlockFlags.IsFirstBlock, content: singleBlock3)
                .WriteBlockWithContent(continuation: 3, flags: BlockFlags.None, content: singleBlock3)
                .WriteBlockWithPattern(allocated: 20, pattern: Gc.Pattern3, isFirstBlock: false)
                .GetStream();

            // result 4                                                                     
            var writeContent4 = Gc.GetByteBufferFromPattern(localPattern, 10, 10);

            var block1 = Gc.GetByteBufferFromPattern(Gc.Pattern1, VaultConfiguration.BlockContentSize, VaultConfiguration.BlockContentSize);
            Array.Copy(localPattern, 0, block1, 50, 5);

            var block2 = Gc.GetByteBufferFromPattern(Gc.Pattern2, VaultConfiguration.BlockContentSize, VaultConfiguration.BlockContentSize);
            Array.Copy(localPattern, 0, block2, 0, 5);

            var stream4 = new VaultGenerator()
                .InitializeVault(VaultConfiguration, vaultInfo())
                .WriteBlockWithContent(continuation: 2, flags: BlockFlags.IsFirstBlock, content: block1)
                .WriteBlockWithContent(continuation: 3, flags: BlockFlags.None, content: block2)
                .WriteBlockWithPattern(allocated: 20, pattern: Gc.Pattern3, isFirstBlock: false)
                .GetStream();

            // result 5
            var writeContent5 = Gc.GetByteBufferFromPattern(localPattern, 10, 10);
            var block3Content5 = Gc.GetByteBufferFromPattern(Gc.Pattern3, 30, 30);
            Array.Copy(localPattern, 0, block3Content5, 20, 5);
            Array.Copy(localPattern, 0, block3Content5, 25, 5);

            var stream5 = new VaultGenerator()
                .InitializeVault(VaultConfiguration, vaultInfo())
                .WriteBlockWithPattern(continuation: 2, pattern: Gc.Pattern1)
                .WriteBlockWithPattern(continuation: 3, pattern: Gc.Pattern2, isFirstBlock: false)
                .WriteBlockWithContent(0, 30, BlockFlags.IsLastBlock, block3Content5)
                .GetStream();

            // result 6
            var writeContent6 = Gc.GetByteBufferFromPattern(localPattern, 55, 55);
            var block3Content6 = Gc.GetByteBufferFromPattern(Gc.Pattern3, 55, 55);

            Array.Copy(writeContent6, 0, block3Content6, 20, 35);
            var block4Content6 = Gc.GetByteBufferFromPattern(new byte[] {0}, 55, 55);
            Array.Copy(writeContent6, 0, block4Content6, 0, 20);
            

            var stream6 = new VaultGenerator()
                .InitializeVault(VaultConfiguration, new VaultInfo(TestVaultName, TestVaultInfoFlags, GetVaultMask().SetValueTo(4, true), 5))
                .WriteBlockWithPattern(continuation: 2, pattern: Gc.Pattern1)
                .WriteBlockWithPattern(continuation: 3, pattern: Gc.Pattern2, isFirstBlock: false)
                .WriteBlockWithContent(4, 55, BlockFlags.None, block3Content6)
                .WriteBlockWithContent(0, 20, BlockFlags.IsLastBlock, block4Content6)
                .GetStream();


            return new[]
            {
                new TestCaseData(0, localPattern, 0, 5)
                    .SetName("1. Перезапись первых пыти байт в самом налаче файла.")
                    .Returns(stream1.ToArray()), 

                new TestCaseData(0, block2Content, 0, block2Content.Length)
                    .SetName("2. Перезапись полного первого блока.")
                    .Returns(stream2.ToArray()),

                new TestCaseData(0, writeContent3, 0, writeContent3.Length)
                    .SetName("3. Перезапись двух первых блоков.")
                    .Returns(stream3.ToArray()),

                new TestCaseData(50, writeContent4, 0, writeContent4.Length)
                    .SetName("4. Последние 5 байт первого блока и первые 5 байт второго блока.")
                    .Returns(stream4.ToArray()),

                new TestCaseData(130, writeContent5, 0, writeContent5.Length)
                    .SetName("5. Дописывание в конец файла без выделения нового блока.")
                    .Returns(stream5.ToArray()),

                new TestCaseData(130, writeContent6, 0, writeContent6.Length)
                    .SetName("6. Дописывание в конец файла с выделением нового блока.")
                    .Returns(stream6.ToArray())
            };
        }

        [Test]
        [TestCaseSource(typeof(VaultStreamTests), nameof(Write_TestData))]
        public byte[] WriteTests(int position, byte[] binary, int offset, int count)
        {
            _stream.Position = position;
            _stream.Write(binary, offset, count);
            var result = _backStream.ToArray();
            return result;
        }

        #endregion

        // private methods

        private static MemoryStream GetVaultStream()
        {
            var vaultInfo = new VaultInfo(TestVaultName, TestVaultInfoFlags, GetVaultMask(), 4);

            return new VaultGenerator()
                .InitializeVault(VaultConfiguration, vaultInfo)
                .WriteBlockWithPattern(continuation: 2, pattern: Gc.Pattern1)
                .WriteBlockWithPattern(continuation: 3, pattern: Gc.Pattern2, isFirstBlock: false)
                .WriteBlockWithPattern(allocated: 20, pattern: Gc.Pattern3, isFirstBlock: false)
                .GetStream();
        }

        private static Core.Data.BitMask GetVaultMask()
        {
            Core.Data.BitMask mask = new Core.Data.BitMask(new byte[64]);
            for (int i = 0; i < 4; i++)
                mask[i] = true;
            return mask;
        }

        // fields

        private static Core.Data.VaultStream _stream;
        private static MemoryStream _backStream;

        private static readonly VaultConfiguration VaultConfiguration = new VaultConfiguration()
            #region Inititalize variable
        {
            BlockFullSize = 64,
            BlockMetadataSize = 9,

            VaultMetadataSize = 128,
            VaultMaskSize = 64
        };

        #endregion

        // constatns

        private const VaultInfoFlags TestVaultInfoFlags = VaultInfoFlags.Encryptable | VaultInfoFlags.Versionable;
        private const string TestVaultName = "test vault name";
    }
}
