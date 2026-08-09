using System;

namespace H1M4W4R1.Incantia.Phonetics
{
    [Flags]
    public enum PhonemeClass : byte
    {
        None = 0,
        Vowel = 1 << 0,
        Consonant = 1 << 1,
        Voiced = 1 << 2,
        Syllabic = 1 << 3
    }

    /// <summary>
    /// Articulatory metadata used by <see cref="PhonemeCostModel"/>. Numeric feature axes use the full 0-255 range.
    /// </summary>
    public readonly struct PhonemeFeatures
    {
        public PhonemeFeatures(
            PhonemeClass phonemeClass,
            byte place = 0,
            byte manner = 0,
            byte vowelHeight = 0,
            byte vowelBackness = 0,
            bool rounded = false)
        {
            Class = phonemeClass;
            Place = place;
            Manner = manner;
            VowelHeight = vowelHeight;
            VowelBackness = vowelBackness;
            Rounded = rounded;
        }

        public PhonemeClass Class { get; }
        public byte Place { get; }
        public byte Manner { get; }
        public byte VowelHeight { get; }
        public byte VowelBackness { get; }
        public bool Rounded { get; }
        public bool IsVowel => (Class & PhonemeClass.Vowel) != 0;
        public bool IsConsonant => (Class & PhonemeClass.Consonant) != 0;
        public bool IsVoiced => (Class & PhonemeClass.Voiced) != 0;
    }
}
