using System;

namespace H1M4W4R1.Incantia.Phonetics
{
    /// <summary>A continuous phoneme stream. It intentionally contains no word-boundary markers.</summary>
    public readonly struct PhonemeSequence
    {
        private static readonly PhonemeId[] EmptyPhonemes = Array.Empty<PhonemeId>();
        private readonly PhonemeId[] _phonemes;

        public PhonemeSequence(PhonemeId[] phonemes)
        {
            _phonemes = phonemes ?? throw new ArgumentNullException(nameof(phonemes));
        }

        public int Length => _phonemes?.Length ?? 0;
        public bool IsEmpty => Length == 0;
        public ReadOnlySpan<PhonemeId> AsSpan()
        {
            return _phonemes ?? EmptyPhonemes;
        }
    }

    /// <summary>Converts normalized text to a canonical phoneme stream for one spoken language.</summary>
    public interface IPhonemizer
    {
        string Language { get; }
        PhonemeSequence Phonemize(string text);
    }

    /// <summary>
    /// Adds strict reference phonemization. Unknown reference words must fail compilation instead of being silently discarded.
    /// </summary>
    public interface IReferencePhonemizer : IPhonemizer
    {
        PhonemeSequence PhonemizeReference(string text);
    }
}
