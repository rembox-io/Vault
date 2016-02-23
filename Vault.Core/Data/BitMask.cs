using System;
using System.Collections.Generic;
using Vault.Core.Exceptions;

namespace Vault.Core.Data
{
    public class BitMask
    {

        public BitMask(byte[] byteArray, int maskLength = 0)
        {
            if (maskLength == 0 || byteArray.Length*NumberOfBitsInByte > maskLength)
                maskLength = byteArray.Length*NumberOfBitsInByte;
            _byteArray = byteArray;
            _maskLength = maskLength;
        }

        public byte[] Bytes => _byteArray;

        public void SetReserveValueTo(int index, bool value)
        {
            if (_cache.ContainsKey(index))
                _cache[index] = value;
            _cache.Add(index, value);
        }

        public void ApplayReserve()
        {
            foreach (var key in _cache.Keys)
                SetValueTo(key, _cache[key]);
            _cache.Clear();
        }

        public void ResetReserve()
        {
            _cache.Clear();
        }

        public bool GetValueOf(int indexOfBit)
        {
            if (indexOfBit > _maskLength - 1)
                throw new ArgumentException(nameof(indexOfBit));

            if (_cache.ContainsKey(indexOfBit))
                return _cache[indexOfBit];

            var indexOfByte = GetNumberOfByteWithBitIndex(indexOfBit);

            var mask = (byte) (1 << indexOfBit%NumberOfBitsInByte);
            return (_byteArray[indexOfByte] & mask) == mask;
        }

        public BitMask SetValueTo(int indexOfBit, bool value)
        {
            if (indexOfBit > _maskLength - 1)
                throw new ArgumentException(nameof(indexOfBit));

            if (_cache.ContainsKey((ushort) indexOfBit))
            {
                if (_cache.ContainsKey((ushort)indexOfBit) != value)
                    throw new VaultException();
                _cache.Remove((ushort)indexOfBit);
            }

            var indexOfByte = GetNumberOfByteWithBitIndex(indexOfBit);

            var @byte = (byte) (1 << indexOfBit%NumberOfBitsInByte);

            _byteArray[indexOfByte] =
                value
                    ? _byteArray[indexOfByte] |= @byte
                    : _byteArray[indexOfByte] &= (byte) ~@byte;

            return this;
        }

        public BitMask SetValuesTo(bool value, params int[] indexes)
        {
            foreach (var index in indexes)
                SetValueTo(index, value);

            return this;
        }

        public bool this[int index]
        {
            get { return GetValueOf(index); }
            set { SetValueTo(index, value); }
        }

        public int GetFirstIndexOf(bool value)
        {
            int index = 0;
            while (index < _maskLength)
            {
                var isValueNotInCache = (!_cache.ContainsKey((ushort)index)  || _cache[(ushort)index] == value);

                if (this[index] == value && isValueNotInCache)
                    return index;
                index ++;
            }
            return -1;
        }

        // private functions

        private int GetNumberOfByteWithBitIndex(int bitIndex)
        {
            return bitIndex/NumberOfBitsInByte;
        }

        // fields

        private readonly Dictionary<int, bool> _cache = new Dictionary<int, bool>(); 
        private readonly byte[] _byteArray;
        private readonly int _maskLength;

        // constants

        private const int NumberOfBitsInByte = 8;
    }
}
