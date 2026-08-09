using System;
using System.Collections.Generic;
using H1M4W4R1.Incantia.Database;
using H1M4W4R1.Incantia.Phonetics;

namespace H1M4W4R1.Incantia.Matching
{
    /// <summary>
    /// Scores every compiled incantation in a language. Create one instance per matching worker because its workspace is reused.
    /// </summary>
    public sealed class IncantationMatcher
    {
        private readonly IReadOnlyList<CompiledIncantation> _incantations;
        private readonly WeightedPhonemeDistance _distance;
        private readonly PhonemeDistanceWorkspace _workspace = new PhonemeDistanceWorkspace();

        public IncantationMatcher(
            IReadOnlyList<CompiledIncantation> incantations,
            WeightedPhonemeDistance distance,
            IncantationMatcherConfig config)
        {
            _incantations = incantations ?? throw new ArgumentNullException(nameof(incantations));
            _distance = distance ?? throw new ArgumentNullException(nameof(distance));
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public IncantationMatcherConfig Config { get; }

        public IncantationMatchResult Match(string language, in PhoneticObservation observation)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentException("A language identifier is required.", nameof(language));
            }

            Config.Validate();
            CandidateScore best = default;
            CandidateScore second = default;
            for (int incantationIndex = 0; incantationIndex < _incantations.Count; incantationIndex++)
            {
                CompiledIncantation incantation = _incantations[incantationIndex];
                if (ReferenceEquals(incantation, null) || !string.Equals(incantation.Language, language, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                CandidateScore candidate = Evaluate(incantation, observation);
                if (!best.HasCandidate || candidate.Total > best.Total)
                {
                    second = best;
                    best = candidate;
                }
                else if (!second.HasCandidate || candidate.Total > second.Total)
                {
                    second = candidate;
                }
            }

            float margin = best.HasCandidate ? best.Total - (second.HasCandidate ? second.Total : 0f) : 0f;
            bool accepted = IsAccepted(best, margin, observation.Phonemes.Length);
            return new IncantationMatchResult(best, second, margin, accepted);
        }

        private CandidateScore Evaluate(CompiledIncantation incantation, in PhoneticObservation observation)
        {
            float fullScore = _distance.CalculateSimilarity(incantation.Phonemes.AsSpan(), observation.Phonemes.AsSpan(), _workspace);
            float consonantScore = CalculateConsonantScore(incantation, observation);
            float triggerScore = incantation.HasTrigger
                ? CalculateTerminalTriggerScore(incantation.TriggerPhonemes, observation.Phonemes)
                : 0f;
            float total = CalculateCompositeScore(incantation.HasTrigger, fullScore, consonantScore, triggerScore);
            float observedInsertionCost = _distance.CalculateInsertionCost(observation.Phonemes.AsSpan());
            float observedLengthRatio = incantation.FullReferenceDeletionCost <= 0f
                ? 0f
                : observedInsertionCost / incantation.FullReferenceDeletionCost;
            return new CandidateScore(incantation, total, fullScore, consonantScore, triggerScore, observedLengthRatio);
        }

        private float CalculateConsonantScore(CompiledIncantation incantation, in PhoneticObservation observation)
        {
            if (incantation.Consonants.IsEmpty && observation.Consonants.IsEmpty)
            {
                return 1f;
            }

            if (incantation.Consonants.IsEmpty || observation.Consonants.IsEmpty)
            {
                return 0f;
            }

            return _distance.CalculateSimilarity(incantation.Consonants.AsSpan(), observation.Consonants.AsSpan(), _workspace);
        }

        private float CalculateTerminalTriggerScore(in PhonemeSequence trigger, in PhonemeSequence observed)
        {
            ReadOnlySpan<PhonemeId> observedPhonemes = observed.AsSpan();
            int maximumWindowLength = (trigger.Length * 2) + 4;
            int maximumLength = observedPhonemes.Length < maximumWindowLength ? observedPhonemes.Length : maximumWindowLength;
            float bestScore = 0f;
            for (int windowLength = 1; windowLength <= maximumLength; windowLength++)
            {
                ReadOnlySpan<PhonemeId> terminalWindow = observedPhonemes.Slice(observedPhonemes.Length - windowLength, windowLength);
                float score = _distance.CalculateSimilarity(trigger.AsSpan(), terminalWindow, _workspace);
                if (score > bestScore)
                {
                    bestScore = score;
                }
            }

            return bestScore;
        }

        private float CalculateCompositeScore(bool hasTrigger, float fullScore, float consonantScore, float triggerScore)
        {
            float activeWeight = Config.FullPhonemeWeight + Config.ConsonantSkeletonWeight;
            float weightedScore = (fullScore * Config.FullPhonemeWeight) + (consonantScore * Config.ConsonantSkeletonWeight);
            if (hasTrigger)
            {
                activeWeight += Config.TriggerWeight;
                weightedScore += triggerScore * Config.TriggerWeight;
            }

            return activeWeight <= 0f ? 0f : weightedScore / activeWeight;
        }

        private bool IsAccepted(in CandidateScore best, float margin, int observedPhonemeCount)
        {
            if (!best.HasCandidate || observedPhonemeCount < Config.MinimumObservedPhonemeCount)
            {
                return false;
            }

            if (best.Total < Config.MinimumScore || margin < Config.MinimumMargin)
            {
                return false;
            }

            if (best.ObservedLengthRatio < Config.MinimumObservedLengthRatio)
            {
                return false;
            }

            return !best.Incantation.HasTrigger || best.Trigger >= Config.MinimumTriggerScore;
        }
    }
}
