using System;

namespace H1M4W4R1.Incantia.Matching
{
    /// <summary>Score weights and conservative initial acceptance thresholds.</summary>
    [Serializable]
    public sealed class IncantationMatcherConfig
    {
        public float FullPhonemeWeight { get; set; } = 0.65f;
        public float ConsonantSkeletonWeight { get; set; } = 0.20f;
        public float TriggerWeight { get; set; } = 0.15f;
        public float MinimumScore { get; set; } = 0.70f;
        public float MinimumMargin { get; set; } = 0.08f;
        public float MinimumTriggerScore { get; set; } = 0.60f;
        public float MinimumObservedLengthRatio { get; set; } = 0.50f;
        public int MinimumObservedPhonemeCount { get; set; } = 3;

        internal void Validate()
        {
            ValidateNonNegative(FullPhonemeWeight, nameof(FullPhonemeWeight));
            ValidateNonNegative(ConsonantSkeletonWeight, nameof(ConsonantSkeletonWeight));
            ValidateNonNegative(TriggerWeight, nameof(TriggerWeight));
            ValidateUnitInterval(MinimumScore, nameof(MinimumScore));
            ValidateUnitInterval(MinimumMargin, nameof(MinimumMargin));
            ValidateUnitInterval(MinimumTriggerScore, nameof(MinimumTriggerScore));
            ValidateNonNegative(MinimumObservedLengthRatio, nameof(MinimumObservedLengthRatio));
            if (MinimumObservedPhonemeCount < 1)
            {
                throw new InvalidOperationException("Minimum observed phoneme count must be at least one.");
            }

            if ((FullPhonemeWeight + ConsonantSkeletonWeight + TriggerWeight) <= 0f)
            {
                throw new InvalidOperationException("At least one score weight must be positive.");
            }
        }

        private static void ValidateNonNegative(float value, string propertyName)
        {
            if (value < 0f)
            {
                throw new InvalidOperationException($"{propertyName} must not be negative.");
            }
        }

        private static void ValidateUnitInterval(float value, string propertyName)
        {
            if (value < 0f || value > 1f)
            {
                throw new InvalidOperationException($"{propertyName} must be between zero and one.");
            }
        }
    }
}
