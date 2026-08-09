using System;
using System.Collections.Generic;
using H1M4W4R1.Incantia.Phonetics;
using H1M4W4R1.Incantia.Text;

namespace H1M4W4R1.Incantia.Database
{
    /// <summary>Compiles validated authoring data into match-ready phoneme streams.</summary>
    public sealed class IncantationCompiler
    {
        private readonly IPhonemizer _phonemizer;
        private readonly WeightedPhonemeDistance _distance;

        public IncantationCompiler(IPhonemizer phonemizer, WeightedPhonemeDistance distance)
        {
            _phonemizer = phonemizer ?? throw new ArgumentNullException(nameof(phonemizer));
            _distance = distance ?? throw new ArgumentNullException(nameof(distance));
        }

        public CompiledIncantation Compile(IncantationDefinition definition)
        {
            if (ReferenceEquals(definition, null))
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!string.Equals(definition.Language, _phonemizer.Language, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The definition language does not match the phonemizer language.", nameof(definition));
            }

            PhonemeSequence phonemes = PhonemizeRequired(definition.Text, "incantation text");
            PhonemeSequence consonants = CreateConsonantSequence(phonemes);
            PhonemeSequence triggerPhonemes = string.IsNullOrWhiteSpace(definition.TriggerText)
                ? new PhonemeSequence(Array.Empty<PhonemeId>())
                : PhonemizeRequired(definition.TriggerText, "trigger text");

            return new CompiledIncantation(
                definition.SpellId,
                definition.Language,
                phonemes,
                consonants,
                triggerPhonemes,
                _distance.CalculateDeletionCost(phonemes.AsSpan()),
                _distance.CalculateDeletionCost(consonants.AsSpan()),
                _distance.CalculateDeletionCost(triggerPhonemes.AsSpan()));
        }

        private PhonemeSequence PhonemizeRequired(string text, string fieldName)
        {
            string normalizedText = IncantationTextNormalizer.Normalize(text);
            PhonemeSequence phonemes = _phonemizer is IReferencePhonemizer referencePhonemizer
                ? referencePhonemizer.PhonemizeReference(normalizedText)
                : _phonemizer.Phonemize(normalizedText);
            if (phonemes.IsEmpty)
            {
                throw new InvalidOperationException($"The phonemizer produced no phonemes for {fieldName}.");
            }

            return phonemes;
        }

        private PhonemeSequence CreateConsonantSequence(in PhonemeSequence phonemes)
        {
            ReadOnlySpan<PhonemeId> source = phonemes.AsSpan();
            List<PhonemeId> consonants = new List<PhonemeId>(source.Length);
            for (int phonemeIndex = 0; phonemeIndex < source.Length; phonemeIndex++)
            {
                if (_distance.CostModel.Inventory.IsConsonant(source[phonemeIndex]))
                {
                    consonants.Add(source[phonemeIndex]);
                }
            }

            return new PhonemeSequence(consonants.ToArray());
        }
    }
}
