using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Vault.Core.Data
{
    [DebuggerDisplay("From:{From}, To:{To}")]
    public struct Range
    {
        public Range(int from, int to)
        {
            From = from;
            To = to;
        }

        public int From { get; set; }

        public int To { get; set; }

        public int Length => To - From;

        public override bool Equals(object obj)
        {
            if (!(obj is Range))
                return false;

            return Equals((Range)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (From*397) ^ To;
            }
        }

        public bool Equals(Range obj)
        {
            return obj.From == From && obj.To == To;
        }

        public override string ToString()
        {
            return $"From:{From}, To:{To}";
        }


        public static readonly Range Empty = new Range(int.MinValue, int.MinValue);
    }
}
