using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using System.Collections.Generic;

namespace Vault.Tests.VaultStream
{
    [TestFixture]
    public class BitMask
    {
        // test initialize

        [SetUp]
        public void TestSetup()
        {
            _maskAsBytes = Enumerable.Repeat((byte) 143, 10).ToArray();
            _bitMask = new Core.Data.BitMask(_maskAsBytes);
        }

        // tests

        public static IEnumerable GetValueOf_TestData()
        {
            var bitMaskOf143 = new[] {true, true, true, true, false, false, false, true};
            var testCaseList = new List<TestCaseData> ();

            for (int i = 0; i < 8; i++)
                testCaseList.Add(new TestCaseData(Enumerable.Range(0, 8).ToArray()).SetName($"Байт #{i}").Returns(bitMaskOf143));

            testCaseList.Add(new TestCaseData(new[] {80})
                .SetName("При обращении за правую границу маски.")
                .Throws(typeof(ArgumentException)));

            return testCaseList;
        }

        [Test, TestCaseSource(typeof(BitMask), nameof(GetValueOf_TestData))]
        public bool[] GetValueOf(int[] index)
        {
            var result = index.Select(i => _bitMask.GetValueOf(i)).ToArray();
            return result;
        }


        public static IEnumerable SetValueOf_TestData()
        {
            Func<int, byte, byte[]> f = (index, value) =>
            {
                var result = Enumerable.Repeat((byte) 143, 10).ToArray();
                result[index] = value;
                return result;
            };

            return new[]
            {
                new TestCaseData(0, false).SetName("1.1 Первый бит первого байта => false").Returns(f(0, 142)),
                new TestCaseData(0, true).SetName("1.2 Первый бит первого байта без изменений").Returns(f(0, 143)),

                new TestCaseData(1, false).SetName("2.1 Второй бит первого байта => false").Returns(f(0, 141)),
                new TestCaseData(1, true).SetName("2.2 Второй бит первого байта без изменений").Returns(f(0, 143)),

                new TestCaseData(4, false).SetName("3.1 Четвертый бит первого байта без изменений").Returns(f(0, 143)),
                new TestCaseData(4, true).SetName("3.2 Четвертый бит первого байта => true").Returns(f(0, 159)),

                new TestCaseData(9, false).SetName("4.1 Второй бит второго байта => false").Returns(f(1, 141)),
                new TestCaseData(9, true).SetName("4.2 Второй бит второго байта без изменений").Returns(f(1, 143)),

                new TestCaseData(12, false).SetName("4.1 Четвертый бит второго байта без изменений").Returns(f(1, 143)),
                new TestCaseData(12, true).SetName("4.2 Четвертый бит второго байта => true").Returns(f(1, 159)),
            };
        }

        [Test, TestCaseSource(typeof(BitMask), nameof(SetValueOf_TestData))]
        public byte[] SetValueOf(int index, bool value)
        {
            _bitMask.SetValueTo(index, value);
            return _maskAsBytes;
        }

        // fields

        private Core.Data.BitMask _bitMask;
        private byte[] _maskAsBytes;
    }
}
