using System;
using System.Collections.Generic;
using H1M4W4R1.Incantia.Phonetics;

namespace H1M4W4R1.Incantia.Matching
{
    /// <summary>Compiled player transcript features passed to the closed-set matcher.</summary>
    public readonly struct PhoneticObservation
    {
        public PhoneticObservation(PhonemeSequence phonemes, PhonemeSequence consonants)
        {
            Phonemes = phonemes;
            Consonants = consonants;
        }

        public PhonemeSequence Phonemes { get; }
        public PhonemeSequence Consonants { get; }

        public static PhoneticObservation Create(PhonemeSequence phonemes, PhonemeInventory inventory)
        {
            if (ReferenceEquals(inventory, null))
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            ReadOnlySpan<PhonemeId> source = phonemes.AsSpan();
            List<PhonemeId> consonants = new List<PhonemeId>(source.Length);
            for (int phonemeIndex = 0; phonemeIndex < source.Length; phonemeIndex++)
            {
                if (inventory.IsConsonant(source[phonemeIndex]))
                {
                    consonants.Add(source[phonemeIndex]);
                }
            }

            return new PhoneticObservation(phonemes, new PhonemeSequence(consonants.ToArray()));
        }
    }
}
