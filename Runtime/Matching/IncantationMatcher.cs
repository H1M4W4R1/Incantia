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
            CandidateScore triggerBest = default;
            CandidateScore triggerSecond = default;
            CandidateScore partialBest = default;
            CandidateScore partialSecond = default;
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

                CandidateScore partialCandidate = EvaluatePartialIncantation(incantation, observation);
                if (partialCandidate.HasCandidate)
                {
                    if (!partialBest.HasCandidate || partialCandidate.Total > partialBest.Total)
                    {
                        partialSecond = partialBest;
                        partialBest = partialCandidate;
                    }
                    else if (!partialSecond.HasCandidate || partialCandidate.Total > partialSecond.Total)
                    {
                        partialSecond = partialCandidate;
                    }
                }

                if (!incantation.HasTrigger)
                {
                    continue;
                }

                if (!triggerBest.HasCandidate || candidate.Trigger > triggerBest.Trigger)
                {
                    triggerSecond = triggerBest;
                    triggerBest = candidate;
                }
                else if (!triggerSecond.HasCandidate || candidate.Trigger > triggerSecond.Trigger)
                {
                    triggerSecond = candidate;
                }
            }

            float margin = best.HasCandidate ? best.Total - (second.HasCandidate ? second.Total : 0f) : 0f;
            if (IsFullIncantationAccepted(best, margin, observation.Phonemes.Length))
            {
                return new IncantationMatchResult(best, second, margin, true, IncantationMatchKind.FullIncantation);
            }

            float triggerMargin = triggerBest.HasCandidate ? triggerBest.Trigger - (triggerSecond.HasCandidate ? triggerSecond.Trigger : 0f) : 0f;
            float partialMargin = partialBest.HasCandidate ? partialBest.Total - (partialSecond.HasCandidate ? partialSecond.Total : 0f) : 0f;
            bool hasValidPartialIncantation = IsPartialIncantationAccepted(partialBest, partialMargin, observation.Phonemes.Length);
            if (IsTriggerOnlyAccepted(triggerBest, triggerMargin, observation.Phonemes.Length) &&
                (!Config.SuppressTriggerOnlyRecognitionDuringPartialIncantation || !hasValidPartialIncantation))
            {
                CandidateScore triggerOnlyBest = CreateTriggerOnlyCandidate(triggerBest);
                CandidateScore triggerOnlySecond = triggerSecond.HasCandidate ? CreateTriggerOnlyCandidate(triggerSecond) : default;
                return new IncantationMatchResult(triggerOnlyBest, triggerOnlySecond, triggerMargin, true, IncantationMatchKind.TriggerOnly);
            }

            return new IncantationMatchResult(best, second, margin, false, IncantationMatchKind.None);
        }

        private CandidateScore Evaluate(CompiledIncantation incantation, in PhoneticObservation observation)
        {
            float fullScore;
            int fullIncantationEndPhonemeIndex;
            if (Config.AllowTrailingSpeech)
            {
                fullScore = _distance.CalculateSubsequenceSimilarity(
                    incantation.Phonemes.AsSpan(),
                    observation.Phonemes.AsSpan(),
                    _workspace,
                    out fullIncantationEndPhonemeIndex);
            }
            else
            {
                fullScore = _distance.CalculateTerminalSimilarity(incantation.Phonemes.AsSpan(), observation.Phonemes.AsSpan(), _workspace);
                fullIncantationEndPhonemeIndex = observation.Phonemes.Length;
            }
            float consonantScore = CalculateConsonantScore(incantation, observation);
            int triggerEndPhonemeIndex = 0;
            float triggerScore = 0f;
            if (incantation.HasTrigger)
            {
                triggerScore = CalculateTriggerScore(incantation.TriggerPhonemes, observation.Phonemes, out triggerEndPhonemeIndex);
            }

            float total = CalculateCompositeScore(incantation.HasTrigger, fullScore, consonantScore, triggerScore);
            float observedInsertionCost = _distance.CalculateInsertionCost(observation.Phonemes.AsSpan());
            float observedLengthRatio = incantation.FullReferenceDeletionCost <= 0f
                ? 0f
                : observedInsertionCost / incantation.FullReferenceDeletionCost;
            return new CandidateScore(
                incantation,
                total,
                fullScore,
                consonantScore,
                triggerScore,
                observedLengthRatio,
                fullIncantationEndPhonemeIndex,
                triggerEndPhonemeIndex);
        }

        private CandidateScore EvaluatePartialIncantation(CompiledIncantation incantation, in PhoneticObservation observation)
        {
            if (!Config.SuppressTriggerOnlyRecognitionDuringPartialIncantation || incantation.Phonemes.Length <= 1)
            {
                return default;
            }

            CandidateScore bestPartial = default;
            ReadOnlySpan<PhonemeId> fullPhonemes = incantation.Phonemes.AsSpan();
            ReadOnlySpan<PhonemeId> fullConsonants = incantation.Consonants.AsSpan();
            int consonantCount = 0;
            for (int partialLength = 1; partialLength < fullPhonemes.Length; partialLength++)
            {
                if (_distance.CostModel.Inventory.IsConsonant(fullPhonemes[partialLength - 1]))
                {
                    consonantCount++;
                }

                ReadOnlySpan<PhonemeId> partialPhonemes = fullPhonemes.Slice(0, partialLength);
                float fullScore = _distance.CalculateTerminalSimilarity(partialPhonemes, observation.Phonemes.AsSpan(), _workspace);
                float consonantScore = CalculatePartialConsonantScore(fullConsonants, consonantCount, observation);
                float total = CalculateCompositeScore(false, fullScore, consonantScore, 0f);
                float partialReferenceDeletionCost = _distance.CalculateDeletionCost(partialPhonemes);
                float observedLengthRatio = partialReferenceDeletionCost <= 0f
                    ? 0f
                    : _distance.CalculateInsertionCost(observation.Phonemes.AsSpan()) / partialReferenceDeletionCost;
                CandidateScore partial = new CandidateScore(
                    incantation,
                    total,
                    fullScore,
                    consonantScore,
                    0f,
                    observedLengthRatio,
                    observation.Phonemes.Length,
                    0);
                if (!bestPartial.HasCandidate || partial.Total > bestPartial.Total)
                {
                    bestPartial = partial;
                }
            }

            return bestPartial;
        }

        private float CalculatePartialConsonantScore(
            ReadOnlySpan<PhonemeId> fullConsonants,
            int consonantCount,
            in PhoneticObservation observation)
        {
            if (consonantCount == 0 && observation.Consonants.IsEmpty)
            {
                return 1f;
            }

            if (consonantCount == 0 || observation.Consonants.IsEmpty)
            {
                return 0f;
            }

            return _distance.CalculateTerminalSimilarity(fullConsonants.Slice(0, consonantCount), observation.Consonants.AsSpan(), _workspace);
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

            if (Config.AllowTrailingSpeech)
            {
                int consonantEndPhonemeIndex;
                return _distance.CalculateSubsequenceSimilarity(incantation.Consonants.AsSpan(), observation.Consonants.AsSpan(), _workspace, out consonantEndPhonemeIndex);
            }

            return _distance.CalculateTerminalSimilarity(incantation.Consonants.AsSpan(), observation.Consonants.AsSpan(), _workspace);
        }

        private float CalculateTriggerScore(in PhonemeSequence trigger, in PhonemeSequence observed, out int triggerEndPhonemeIndex)
        {
            if (Config.AllowTrailingSpeech)
            {
                return _distance.CalculateSubsequenceSimilarity(trigger.AsSpan(), observed.AsSpan(), _workspace, out triggerEndPhonemeIndex);
            }

            triggerEndPhonemeIndex = observed.Length;
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

        private bool IsFullIncantationAccepted(in CandidateScore best, float margin, int observedPhonemeCount)
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

            return !best.Incantation.HasTrigger ||
                (best.Trigger >= Config.MinimumTriggerScore && best.TriggerEndPhonemeIndex >= best.FullIncantationEndPhonemeIndex);
        }

        private bool IsPartialIncantationAccepted(in CandidateScore best, float margin, int observedPhonemeCount)
        {
            if (!best.HasCandidate || observedPhonemeCount < Config.MinimumObservedPhonemeCount)
            {
                return false;
            }

            return best.Total >= Config.MinimumScore &&
                margin >= Config.MinimumMargin &&
                best.ObservedLengthRatio >= Config.MinimumObservedLengthRatio;
        }

        private bool IsTriggerOnlyAccepted(in CandidateScore best, float margin, int observedPhonemeCount)
        {
            if (!Config.AllowTriggerOnlyRecognition || !best.HasCandidate || !best.Incantation.HasTrigger)
            {
                return false;
            }

            if (observedPhonemeCount < best.Incantation.TriggerPhonemes.Length)
            {
                return false;
            }

            return best.Trigger >= Config.MinimumTriggerOnlyScore && margin >= Config.MinimumTriggerOnlyMargin;
        }

        private static CandidateScore CreateTriggerOnlyCandidate(in CandidateScore candidate)
        {
            return new CandidateScore(
                candidate.Incantation,
                candidate.Trigger,
                candidate.FullPhoneme,
                candidate.ConsonantSkeleton,
                candidate.Trigger,
                candidate.ObservedLengthRatio,
                candidate.TriggerEndPhonemeIndex,
                candidate.TriggerEndPhonemeIndex);
        }
    }
}
