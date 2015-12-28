using System.Collections;
using System.IO;
using System.Security.Cryptography.X509Certificates;
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
            _stream = new Core.Data.VaultStream(GetVaultStream(), 1, VaultConfiguration);
        }

        // tests

        #region VaultStream.Read
        public static IEnumerable Read_TestData()
        {
            var result2 = new byte[VaultConfiguration.BlockContentSize * 2].Write(w =>
            {
                w.Write(VaultGenerator.GetByteBufferFromPattern(Pattern1, 55, 55));
                w.Write(VaultGenerator.GetByteBufferFromPattern(Pattern2, 55, 55));
            });

            var result3 = new byte[20].Write(w =>
            {
                w.Write(VaultGenerator.GetByteBufferFromPattern(Pattern1, 5, 5));
                w.Write(VaultGenerator.GetByteBufferFromPattern(Pattern2, 15, 15));
            });

            var result4 = new byte[40].Write(w =>
            {
                w.Write(VaultGenerator.GetByteBufferFromPattern(Pattern2, 5, 5));
                w.Write(VaultGenerator.GetByteBufferFromPattern(Pattern3, 20, 20));
                w.Write(VaultGenerator.GetByteBufferFromPattern(new byte[] { 0 }, 15, 15));
            });

            return new[]
            {
                new TestCaseData(0, 5)
                .SetName("1. Чтение первых пяти байт")
                .Returns(VaultGenerator.GetByteBufferFromPattern(Pattern1, 5, 5)),

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
        
        [Test, TestCaseSource(typeof(VaultStreamTests), nameof(Read_TestData))]
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
                new TestCaseData(GetVaultStream(), 1, VaultConfiguration.BlockContentSize, 0, new Range(0, 5))
                    .SetName("1. Корректный поиск в первом блоке")
                    .Returns(new Range(81, 86)),

                new TestCaseData(GetVaultStream(), 1, VaultConfiguration.BlockContentSize, 0, new Range(0, 56))
                    .SetName("2. Выход за пределый правой части границы")
                    .Returns(new Range(81, 136)),

                new TestCaseData(GetVaultStream(), 1, VaultConfiguration.BlockContentSize, 0, new Range(0, 55))
                    .SetName("3. Ровно в границах блока")
                    .Returns(new Range(81, 136)),

                new TestCaseData(GetVaultStream(), 2, VaultConfiguration.BlockContentSize, 1, new Range(56, 110))
                    .SetName("4. Ровно в границах второго блока")
                    .Returns(new Range(146, 200)),

                new TestCaseData(GetVaultStream(), 2, VaultConfiguration.BlockContentSize, 1, new Range(60, 100))
                    .SetName("5. Внутри границ второго блока")
                    .Returns(new Range(150, 190)),

                new TestCaseData(GetVaultStream(), 2, VaultConfiguration.BlockContentSize, 1, new Range(60, 120))
                    .SetName("6. Выход за границы второго блока")
                    .Returns(new Range(150, 200)),

                new TestCaseData(GetVaultStream(), 3, 20, 2, new Range(110, 130))
                    .SetName("7. Ровно в границах не полного третьего блока")
                    .Returns(new Range(209, 229)),

                new TestCaseData(GetVaultStream(), 3, 20, 2, new Range(110, 135))
                    .SetName("8. Выход за границы не полного третьего блока")
                    .Returns(new Range(209, 229)),
            };
        }

        [Test, TestCaseSource(typeof(VaultStreamTests), nameof(GetBackRangeForBlock_TestData))]
        public Range GetBackRangeForBlock(Stream backStream, int blockIndex, int blockAllocated, int localBlockIndex, Range upRange)
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
                .Returns(new [] {new Range(81, 86)}),

                new TestCaseData(0, 60)
                .SetName("2. Первый и второй блоки. ")
                .Returns(new [] {new Range(81, 136), new Range(145, 150)}),

                new TestCaseData(20, 70)
                .SetName("3. Конец первого и начало торого блока. ")
                .Returns(new [] {new Range(101, 136), new Range(145, 180)}),

                new TestCaseData(120, 20)
                .SetName("4. Конец первого и начало торого блока. ")
                .Returns(new [] {new Range(219, 229)}),
            };
        }

        [Test, TestCaseSource(typeof(VaultStreamTests), nameof(GetBackStreaamRangesToCopy_TestData))]
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

        [Test, TestCaseSource(typeof(VaultStreamTests), nameof(GetNumberOfBlocksForLength_TestData))]
        public int GetNumberOfBlocksForLength(long length)
        {
            return _stream.GetNumberOfBlocksForLength(length);
        }
        #endregion

        // private methods

        private static Stream GetVaultStream()
        {

            return new VaultGenerator()
                .InitializeVault()
                .WriteBlock(continuation: 2, pattern: Pattern1)
                .WriteBlock(continuation: 3, pattern: Pattern2)
                .WriteBlock(allocated: 20, pattern: Pattern3)
                .GetStream();
        }
        
        // fields

        private Core.Data.VaultStream _stream;

        private readonly static VaultConfiguration VaultConfiguration = new VaultConfiguration()
        #region Inititalize variable
        {
                BlockFullSize = 64,
                BlockMetadataSize = 9
            };
        #endregion

        // constatns

        private readonly static byte[] Pattern1 = { 21, 22, 23, 24, 25 };
        private readonly static byte[] Pattern2 = { 31, 32, 33, 34, 35 };
        private readonly static byte[] Pattern3 = { 41, 42, 43, 44, 45 };
    }
}
