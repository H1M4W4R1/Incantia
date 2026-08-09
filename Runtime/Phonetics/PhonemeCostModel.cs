using System;
using System.Collections.Generic;

namespace H1M4W4R1.Incantia.Phonetics
{
    /// <summary>Configurable weighted edit costs for one phoneme inventory and language profile.</summary>
    public sealed class PhonemeCostModel
    {
        private readonly Dictionary<uint, float> _substitutionOverrides = new Dictionary<uint, float>();

        public PhonemeCostModel(PhonemeInventory inventory)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public PhonemeInventory Inventory { get; }
        public float VowelInsertionCost { get; set; } = 0.25f;
        public float VowelDeletionCost { get; set; } = 0.30f;
        public float ConsonantInsertionCost { get; set; } = 0.70f;
        public float ConsonantDeletionCost { get; set; } = 0.80f;
        public float VowelSubstitutionMultiplier { get; set; } = 0.40f;
        public float ConsonantSubstitutionMultiplier { get; set; } = 1.00f;
        public float CrossClassSubstitutionCost { get; set; } = 1.00f;

        public void SetSubstitutionOverride(PhonemeId first, PhonemeId second, float cost)
        {
            if (cost < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cost));
            }

            _substitutionOverrides[CreatePairKey(first, second)] = cost;
        }

        public float GetInsertionCost(PhonemeId phonemeId)
        {
            return GetCost(Inventory.GetFeatures(phonemeId), VowelInsertionCost, ConsonantInsertionCost);
        }

        public float GetDeletionCost(PhonemeId phonemeId)
        {
            return GetCost(Inventory.GetFeatures(phonemeId), VowelDeletionCost, ConsonantDeletionCost);
        }

        public float GetSubstitutionCost(PhonemeId reference, PhonemeId observed)
        {
            if (reference == observed)
            {
                return 0f;
            }

            if (_substitutionOverrides.TryGetValue(CreatePairKey(reference, observed), out float overrideCost))
            {
                return overrideCost;
            }

            PhonemeFeatures referenceFeatures = Inventory.GetFeatures(reference);
            PhonemeFeatures observedFeatures = Inventory.GetFeatures(observed);
            if (referenceFeatures.IsVowel != observedFeatures.IsVowel)
            {
                return CrossClassSubstitutionCost;
            }

            if (referenceFeatures.IsVowel)
            {
                float distance = GetVowelFeatureDistance(referenceFeatures, observedFeatures);
                return distance * VowelSubstitutionMultiplier;
            }

            float consonantDistance = GetConsonantFeatureDistance(referenceFeatures, observedFeatures);
            return consonantDistance * ConsonantSubstitutionMultiplier;
        }

        private static float GetCost(in PhonemeFeatures features, float vowelCost, float consonantCost)
        {
            if (features.IsVowel)
            {
                return vowelCost;
            }

            if (features.IsConsonant)
            {
                return consonantCost;
            }

            throw new ArgumentException("A phoneme must be marked as a vowel or consonant.", nameof(features));
        }

        private static float GetConsonantFeatureDistance(in PhonemeFeatures first, in PhonemeFeatures second)
        {
            float voiceDifference = first.IsVoiced == second.IsVoiced ? 0f : 1f;
            float placeDifference = GetNormalizedDifference(first.Place, second.Place);
            float mannerDifference = GetNormalizedDifference(first.Manner, second.Manner);
            return Clamp01((voiceDifference * 0.15f) + (placeDifference * 0.35f) + (mannerDifference * 0.50f));
        }

        private static float GetVowelFeatureDistance(in PhonemeFeatures first, in PhonemeFeatures second)
        {
            float heightDifference = GetNormalizedDifference(first.VowelHeight, second.VowelHeight);
            float backnessDifference = GetNormalizedDifference(first.VowelBackness, second.VowelBackness);
            float roundingDifference = first.Rounded == second.Rounded ? 0f : 1f;
            return Clamp01((heightDifference * 0.40f) + (backnessDifference * 0.40f) + (roundingDifference * 0.20f));
        }

        private static float GetNormalizedDifference(byte first, byte second)
        {
            return Math.Abs(first - second) / (float)byte.MaxValue;
        }

        private static uint CreatePairKey(PhonemeId first, PhonemeId second)
        {
            ushort smaller = first.Value <= second.Value ? first.Value : second.Value;
            ushort larger = first.Value <= second.Value ? second.Value : first.Value;
            return ((uint)smaller << 16) | larger;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }
}
