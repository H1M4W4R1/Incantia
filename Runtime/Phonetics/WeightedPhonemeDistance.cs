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
            Span<float> insertionCosts = workspace.InsertionCosts;
            previous[0] = 0f;
            for (int observedIndex = 1; observedIndex <= observed.Length; observedIndex++)
            {
                float insertionCost = CostModel.GetInsertionCost(observed[observedIndex - 1]);
                insertionCosts[observedIndex] = insertionCost;
                previous[observedIndex] = previous[observedIndex - 1] + insertionCost;
            }

            for (int referenceIndex = 1; referenceIndex <= reference.Length; referenceIndex++)
            {
                Span<float> current = workspace.Current;
                PhonemeId referencePhoneme = reference[referenceIndex - 1];
                float deletionCost = CostModel.GetDeletionCost(referencePhoneme);
                current[0] = previous[0] + deletionCost;
                for (int observedIndex = 1; observedIndex <= observed.Length; observedIndex++)
                {
                    float deletion = previous[observedIndex] + deletionCost;
                    float insertion = current[observedIndex - 1] + insertionCosts[observedIndex];
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

            float referenceDeletionCost = CalculateDeletionCost(reference);
            float observedInsertionCost = CalculateInsertionCost(observed);
            return CalculateSimilarity(reference, observed, referenceDeletionCost, observedInsertionCost, workspace);
        }

        internal float CalculateSimilarity(
            ReadOnlySpan<PhonemeId> reference,
            ReadOnlySpan<PhonemeId> observed,
            float referenceDeletionCost,
            float observedInsertionCost,
            PhonemeDistanceWorkspace workspace)
        {
            float distance = CalculateDistance(reference, observed, workspace);
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

            float normalizer = CalculateDeletionCost(reference);
            return CalculateTerminalSimilarity(reference, observed, normalizer, workspace);
        }

        internal float CalculateTerminalSimilarity(
            ReadOnlySpan<PhonemeId> reference,
            ReadOnlySpan<PhonemeId> observed,
            float referenceDeletionCost,
            PhonemeDistanceWorkspace workspace)
        {
            float distance = CalculateTerminalDistance(reference, observed, workspace);
            float normalizer = referenceDeletionCost;
            if (normalizer <= 0f)
            {
                return 1f;
            }

            return Clamp01(1f - (distance / normalizer));
        }

        /// <summary>
        /// Calculates similarity between a reference and any contiguous portion of an observation.
        /// The returned end index is exclusive and identifies where the best observed portion ends.
        /// </summary>
        public float CalculateSubsequenceSimilarity(
            ReadOnlySpan<PhonemeId> reference,
            ReadOnlySpan<PhonemeId> observed,
            PhonemeDistanceWorkspace workspace,
            out int matchedEndIndex)
        {
            matchedEndIndex = 0;
            if (reference.Length == 0 && observed.Length == 0)
            {
                return 1f;
            }

            if (reference.Length == 0 || observed.Length == 0)
            {
                return 0f;
            }

            float normalizer = CalculateDeletionCost(reference);
            return CalculateSubsequenceSimilarity(reference, observed, normalizer, workspace, out matchedEndIndex);
        }

        internal float CalculateSubsequenceSimilarity(
            ReadOnlySpan<PhonemeId> reference,
            ReadOnlySpan<PhonemeId> observed,
            float referenceDeletionCost,
            PhonemeDistanceWorkspace workspace,
            out int matchedEndIndex)
        {
            float distance = CalculateSubsequenceDistance(reference, observed, workspace, out matchedEndIndex);
            float normalizer = referenceDeletionCost;
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
            Span<float> insertionCosts = workspace.InsertionCosts;
            for (int observedIndex = 0; observedIndex <= observed.Length; observedIndex++)
            {
                previous[observedIndex] = 0f;
                if (observedIndex > 0)
                {
                    insertionCosts[observedIndex] = CostModel.GetInsertionCost(observed[observedIndex - 1]);
                }
            }

            for (int referenceIndex = 1; referenceIndex <= reference.Length; referenceIndex++)
            {
                Span<float> current = workspace.Current;
                PhonemeId referencePhoneme = reference[referenceIndex - 1];
                float deletionCost = CostModel.GetDeletionCost(referencePhoneme);
                current[0] = previous[0] + deletionCost;
                for (int observedIndex = 1; observedIndex <= observed.Length; observedIndex++)
                {
                    float deletion = previous[observedIndex] + deletionCost;
                    float insertion = current[observedIndex - 1] + insertionCosts[observedIndex];
                    float substitution = previous[observedIndex - 1] + CostModel.GetSubstitutionCost(referencePhoneme, observed[observedIndex - 1]);
                    current[observedIndex] = GetMinimum(deletion, insertion, substitution);
                }

                workspace.SwapRows();
                previous = workspace.Previous;
            }

            return previous[observed.Length];
        }

        private float CalculateSubsequenceDistance(
            ReadOnlySpan<PhonemeId> reference,
            ReadOnlySpan<PhonemeId> observed,
            PhonemeDistanceWorkspace workspace,
            out int matchedEndIndex)
        {
            if (ReferenceEquals(workspace, null))
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            workspace.EnsureCapacity(observed.Length + 1);
            Span<float> previous = workspace.Previous;
            Span<float> insertionCosts = workspace.InsertionCosts;
            for (int observedIndex = 0; observedIndex <= observed.Length; observedIndex++)
            {
                previous[observedIndex] = 0f;
                if (observedIndex > 0)
                {
                    insertionCosts[observedIndex] = CostModel.GetInsertionCost(observed[observedIndex - 1]);
                }
            }

            for (int referenceIndex = 1; referenceIndex <= reference.Length; referenceIndex++)
            {
                Span<float> current = workspace.Current;
                PhonemeId referencePhoneme = reference[referenceIndex - 1];
                float deletionCost = CostModel.GetDeletionCost(referencePhoneme);
                current[0] = previous[0] + deletionCost;
                for (int observedIndex = 1; observedIndex <= observed.Length; observedIndex++)
                {
                    float deletion = previous[observedIndex] + deletionCost;
                    float insertion = current[observedIndex - 1] + insertionCosts[observedIndex];
                    float substitution = previous[observedIndex - 1] + CostModel.GetSubstitutionCost(referencePhoneme, observed[observedIndex - 1]);
                    current[observedIndex] = GetMinimum(deletion, insertion, substitution);
                }

                workspace.SwapRows();
                previous = workspace.Previous;
            }

            int bestEndIndex = 1;
            float bestDistance = previous[bestEndIndex];
            for (int observedIndex = 2; observedIndex <= observed.Length; observedIndex++)
            {
                if (previous[observedIndex] <= bestDistance)
                {
                    bestDistance = previous[observedIndex];
                    bestEndIndex = observedIndex;
                }
            }

            matchedEndIndex = bestEndIndex;
            return bestDistance;
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
