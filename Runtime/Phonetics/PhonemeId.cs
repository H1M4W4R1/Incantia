using System;

namespace H1M4W4R1.Incantia.Phonetics
{
    /// <summary>Identifies a phoneme in a language profile's canonical inventory.</summary>
    public readonly struct PhonemeId : IEquatable<PhonemeId>, IComparable<PhonemeId>
    {
        public PhonemeId(ushort value)
        {
            Value = value;
        }

        public ushort Value { get; }

        public int CompareTo(PhonemeId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(PhonemeId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object objectValue)
        {
            return objectValue is PhonemeId phonemeId && Equals(phonemeId);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(PhonemeId left, PhonemeId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PhonemeId left, PhonemeId right)
        {
            return !left.Equals(right);
        }
    }
}
