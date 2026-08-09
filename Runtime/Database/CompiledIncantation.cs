using System;
using H1M4W4R1.Incantia.Phonetics;

namespace H1M4W4R1.Incantia.Database
{
    /// <summary>Runtime-ready incantation data. Reference phonemes are compiled once, never per match.</summary>
    public sealed class CompiledIncantation
    {
        public CompiledIncantation(
            string spellId,
            string language,
            PhonemeSequence phonemes,
            PhonemeSequence consonants,
            PhonemeSequence triggerPhonemes,
            float fullReferenceDeletionCost,
            float consonantReferenceDeletionCost,
            float triggerReferenceDeletionCost)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                throw new ArgumentException("A spell identifier is required.", nameof(spellId));
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentException("A language identifier is required.", nameof(language));
            }

            if (phonemes.IsEmpty)
            {
                throw new ArgumentException("A compiled incantation must contain phonemes.", nameof(phonemes));
            }

            SpellId = spellId;
            Language = language;
            Phonemes = phonemes;
            Consonants = consonants;
            TriggerPhonemes = triggerPhonemes;
            FullReferenceDeletionCost = fullReferenceDeletionCost;
            ConsonantReferenceDeletionCost = consonantReferenceDeletionCost;
            TriggerReferenceDeletionCost = triggerReferenceDeletionCost;
        }

        public string SpellId { get; }
        public string Language { get; }
        public PhonemeSequence Phonemes { get; }
        public PhonemeSequence Consonants { get; }
        public PhonemeSequence TriggerPhonemes { get; }
        public bool HasTrigger => !TriggerPhonemes.IsEmpty;
        public float FullReferenceDeletionCost { get; }
        public float ConsonantReferenceDeletionCost { get; }
        public float TriggerReferenceDeletionCost { get; }
    }
}
