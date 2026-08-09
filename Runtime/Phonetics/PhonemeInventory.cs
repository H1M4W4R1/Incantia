using System;
using System.Collections.Generic;

namespace H1M4W4R1.Incantia.Phonetics
{
    /// <summary>Maps canonical phoneme identifiers to the features used for cost calculation.</summary>
    public sealed class PhonemeInventory
    {
        private readonly Dictionary<ushort, PhonemeFeatures> _featuresById = new Dictionary<ushort, PhonemeFeatures>();

        public void Register(PhonemeId phonemeId, in PhonemeFeatures features)
        {
            if (features.IsVowel == features.IsConsonant)
            {
                throw new ArgumentException("A phoneme must be marked as exactly one of vowel or consonant.", nameof(features));
            }

            _featuresById[phonemeId.Value] = features;
        }

        public PhonemeFeatures GetFeatures(PhonemeId phonemeId)
        {
            if (!_featuresById.TryGetValue(phonemeId.Value, out PhonemeFeatures features))
            {
                throw new KeyNotFoundException($"Phoneme '{phonemeId}' is not registered in this inventory.");
            }

            return features;
        }

        public bool IsConsonant(PhonemeId phonemeId)
        {
            return GetFeatures(phonemeId).IsConsonant;
        }
    }
}
