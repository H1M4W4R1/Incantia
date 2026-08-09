using System;
using System.Collections.Generic;

namespace H1M4W4R1.Incantia.Phonetics
{
    /// <summary>Maps canonical phoneme identifiers to the features used for cost calculation.</summary>
    public sealed class PhonemeInventory
    {
        private PhonemeFeatures[] _featuresById = Array.Empty<PhonemeFeatures>();
        private bool[] _registeredIds = Array.Empty<bool>();

        public void Register(PhonemeId phonemeId, in PhonemeFeatures features)
        {
            if (features.IsVowel == features.IsConsonant)
            {
                throw new ArgumentException("A phoneme must be marked as exactly one of vowel or consonant.", nameof(features));
            }

            EnsureCapacity(phonemeId.Value + 1);
            _featuresById[phonemeId.Value] = features;
            _registeredIds[phonemeId.Value] = true;
        }

        public PhonemeFeatures GetFeatures(PhonemeId phonemeId)
        {
            if (phonemeId.Value >= _registeredIds.Length || !_registeredIds[phonemeId.Value])
            {
                throw new KeyNotFoundException($"Phoneme '{phonemeId}' is not registered in this inventory.");
            }

            return _featuresById[phonemeId.Value];
        }

        public bool IsConsonant(PhonemeId phonemeId)
        {
            return GetFeatures(phonemeId).IsConsonant;
        }

        private void EnsureCapacity(int requiredLength)
        {
            if (_featuresById.Length >= requiredLength)
            {
                return;
            }

            int newLength = _featuresById.Length == 0 ? 16 : _featuresById.Length;
            while (newLength < requiredLength)
            {
                newLength = Math.Min(newLength * 2, ushort.MaxValue + 1);
            }

            Array.Resize(ref _featuresById, newLength);
            Array.Resize(ref _registeredIds, newLength);
        }
    }
}
