using System;

namespace H1M4W4R1.Incantia.Phonetics
{
    /// <summary>Allocation-free-after-warmup weighted edit-distance calculation.</summary>
    public sealed class WeightedPhonemeDistance
    {
        public WeightedPhonemeDistance(PhonemeCostModel costModel)
        {
            CostModel = costModel ?? throw new ArgumentNullException(nameof(costModel));
        }

        public PhonemeCostModel CostModel { get; }

        public float CalculateDistance(
            ReadOnlySpan<PhonemeId> reference,
            ReadOnlySpan<PhonemeId> observed,
            PhonemeDistanceWorkspace workspace)
        {
            if (ReferenceEquals(workspace, null))
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            workspace.EnsureCapacity(observed.Length + 1);
            Span<float> previous = workspace.Previous;
            previous[0] = 0f;
            for (int observedIndex = 1; observedIndex <= observed.Length; observedIndex++)
            {
                previous[observedIndex] = previous[observedIndex - 1] + CostModel.GetInsertionCost(observed[observedIndex - 1]);
            }

            for (int referenceIndex = 1; referenceIndex <= reference.Length; referenceIndex++)
            {
                Span<float> current = workspace.Current;
                PhonemeId referencePhoneme = reference[referenceIndex - 1];
                current[0] = previous[0] + CostModel.GetDeletionCost(referencePhoneme);
                for (int observedIndex = 1; observedIndex <= observed.Length; observedIndex++)
                {
                    float deletion = previous[observedIndex] + CostModel.GetDeletionCost(referencePhoneme);
                    float insertion = current[observedIndex - 1] + CostModel.GetInsertionCost(observed[observedIndex - 1]);
                    float substitution = previous[observedIndex - 1] + CostModel.GetSubstitutionCost(referencePhoneme, observed[observedIndex - 1]);
                    current[observedIndex] = GetMinimum(deletion, insertion, substitution);
                }

                workspace.SwapRows();
                previous = workspace.Previous;
            }

            return previous[observed.Length];
        }

        public float CalculateSimilarity(
            ReadOnlySpan<PhonemeId> reference,
            ReadOnlySpan<PhonemeId> observed,
            PhonemeDistanceWorkspace workspace)
        {
            if (reference.Length == 0 && observed.Length == 0)
            {
                return 1f;
            }

            if (reference.Length == 0 || observed.Length == 0)
            {
                return 0f;
            }

            float distance = CalculateDistance(reference, observed, workspace);
            float referenceDeletionCost = CalculateDeletionCost(reference);
            float observedInsertionCost = CalculateInsertionCost(observed);
            float normalizer = referenceDeletionCost >= observedInsertionCost ? referenceDeletionCost : observedInsertionCost;
            if (normalizer <= 0f)
            {
                return 1f;
            }

            return Clamp01(1f - (distance / normalizer));
        }

        /// <summary>
        /// Calculates similarity between a reference and a terminal portion of an observation.
        /// Leading observed phonemes are free so a complete incantation remains matchable after unrelated speech.
        /// </summary>
        public float CalculateTerminalSimilarity(
            ReadOnlySpan<PhonemeId> reference,
            ReadOnlySpan<PhonemeId> observed,
            PhonemeDistanceWorkspace workspace)
        {
            if (reference.Length == 0 && observed.Length == 0)
            {
                return 1f;
            }

            if (reference.Length == 0 || observed.Length == 0)
            {
                return 0f;
            }

            float distance = CalculateTerminalDistance(reference, observed, workspace);
            float normalizer = CalculateDeletionCost(reference);
            if (normalizer <= 0f)
            {
                return 1f;
            }

            return Clamp01(1f - (distance / normalizer));
        }

        public float CalculateDeletionCost(ReadOnlySpan<PhonemeId> phonemes)
        {
            float total = 0f;
            for (int phonemeIndex = 0; phonemeIndex < phonemes.Length; phonemeIndex++)
            {
                total += CostModel.GetDeletionCost(phonemes[phonemeIndex]);
            }

            return total;
        }

        private float CalculateTerminalDistance(
            ReadOnlySpan<PhonemeId> reference,
            ReadOnlySpan<PhonemeId> observed,
            PhonemeDistanceWorkspace workspace)
        {
            if (ReferenceEquals(workspace, null))
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            workspace.EnsureCapacity(observed.Length + 1);
            Span<float> previous = workspace.Previous;
            for (int observedIndex = 0; observedIndex <= observed.Length; observedIndex++)
            {
                previous[observedIndex] = 0f;
            }

            for (int referenceIndex = 1; referenceIndex <= reference.Length; referenceIndex++)
            {
                Span<float> current = workspace.Current;
                PhonemeId referencePhoneme = reference[referenceIndex - 1];
                current[0] = previous[0] + CostModel.GetDeletionCost(referencePhoneme);
                for (int observedIndex = 1; observedIndex <= observed.Length; observedIndex++)
                {
                    float deletion = previous[observedIndex] + CostModel.GetDeletionCost(referencePhoneme);
                    float insertion = current[observedIndex - 1] + CostModel.GetInsertionCost(observed[observedIndex - 1]);
                    float substitution = previous[observedIndex - 1] + CostModel.GetSubstitutionCost(referencePhoneme, observed[observedIndex - 1]);
                    current[observedIndex] = GetMinimum(deletion, insertion, substitution);
                }

                workspace.SwapRows();
                previous = workspace.Previous;
            }

            return previous[observed.Length];
        }

        public float CalculateInsertionCost(ReadOnlySpan<PhonemeId> phonemes)
        {
            float total = 0f;
            for (int phonemeIndex = 0; phonemeIndex < phonemes.Length; phonemeIndex++)
            {
                total += CostModel.GetInsertionCost(phonemes[phonemeIndex]);
            }

            return total;
        }

        private static float GetMinimum(float first, float second, float third)
        {
            float minimum = first < second ? first : second;
            return minimum < third ? minimum : third;
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
