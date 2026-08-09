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
            if (IsFullIncantationAccepted(best, margin, observation))
            {
                return new IncantationMatchResult(best, second, margin, true, IncantationMatchKind.FullIncantation);
            }

            float triggerMargin = triggerBest.HasCandidate ? triggerBest.Trigger - (triggerSecond.HasCandidate ? triggerSecond.Trigger : 0f) : 0f;
            if (IsTriggerOnlyAccepted(triggerBest, triggerMargin, observation))
            {
                CandidateScore triggerOnlyBest = CreateTriggerOnlyCandidate(triggerBest);
                CandidateScore triggerOnlySecond = triggerSecond.HasCandidate ? CreateTriggerOnlyCandidate(triggerSecond) : default;
                return new IncantationMatchResult(triggerOnlyBest, triggerOnlySecond, triggerMargin, true, IncantationMatchKind.TriggerOnly);
            }

            return new IncantationMatchResult(best, second, margin, false, IncantationMatchKind.None);
        }

        private CandidateScore Evaluate(CompiledIncantation incantation, in PhoneticObservation observation)
        {
            int fullEndPhonemeIndex = observation.Phonemes.Length;
            float fullScore;
            if (Config.AllowTrailingSpeech)
            {
                fullScore = _distance.CalculateSubsequenceSimilarity(incantation.Phonemes.AsSpan(), observation.Phonemes.AsSpan(), _workspace, out fullEndPhonemeIndex);
            }
            else
            {
                fullScore = _distance.CalculateTerminalSimilarity(incantation.Phonemes.AsSpan(), observation.Phonemes.AsSpan(), _workspace);
            }
            float consonantScore = CalculateConsonantScore(incantation, observation);
            int triggerEndPhonemeIndex = 0;
            float triggerScore = 0f;
            if (incantation.HasTrigger)
            {
                triggerEndPhonemeIndex = FindLastExactTriggerEndIndex(incantation.TriggerPhonemes, observation.Phonemes);
                triggerScore = triggerEndPhonemeIndex > 0 ? 1f : 0f;
            }

            float total = CalculateCompositeScore(incantation.HasTrigger, fullScore, consonantScore, triggerScore);
            float observedInsertionCost = _distance.CalculateInsertionCost(observation.Phonemes.AsSpan());
            float observedLengthRatio = incantation.FullReferenceDeletionCost <= 0f
                ? 0f
                : observedInsertionCost / incantation.FullReferenceDeletionCost;
            return new CandidateScore(incantation, total, fullScore, consonantScore, triggerScore, observedLengthRatio, fullEndPhonemeIndex, triggerEndPhonemeIndex);
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

        private bool IsFullIncantationAccepted(in CandidateScore best, float margin, in PhoneticObservation observation)
        {
            if (!best.HasCandidate || observation.Phonemes.Length < Config.MinimumObservedPhonemeCount)
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
                   (best.Trigger >= Config.MinimumTriggerScore && HasExactTriggerEndingAt(best.Incantation.TriggerPhonemes, observation.Phonemes, best.FullEndPhonemeIndex));
        }

        private bool IsTriggerOnlyAccepted(in CandidateScore best, float margin, in PhoneticObservation observation)
        {
            if (!Config.AllowTriggerOnlyRecognition || !best.HasCandidate || !best.Incantation.HasTrigger)
            {
                return false;
            }

            if (best.TriggerEndPhonemeIndex <= 0)
            {
                return false;
            }

            return best.Trigger >= Config.MinimumTriggerOnlyScore && margin >= Config.MinimumTriggerOnlyMargin;
        }

        private static bool HasExactTriggerEndingAt(in PhonemeSequence trigger, in PhonemeSequence observed, int triggerEndPhonemeIndex)
        {
            if (triggerEndPhonemeIndex < trigger.Length || triggerEndPhonemeIndex > observed.Length)
            {
                return false;
            }

            ReadOnlySpan<PhonemeId> observedPhonemes = observed.AsSpan();
            ReadOnlySpan<PhonemeId> terminalPhonemes = observedPhonemes.Slice(triggerEndPhonemeIndex - trigger.Length, trigger.Length);
            return HasExactPhonemeMatch(trigger.AsSpan(), terminalPhonemes);
        }

        private static int FindLastExactTriggerEndIndex(in PhonemeSequence trigger, in PhonemeSequence observed)
        {
            if (trigger.Length > observed.Length)
            {
                return 0;
            }

            ReadOnlySpan<PhonemeId> observedPhonemes = observed.AsSpan();
            int maximumStartIndex = observedPhonemes.Length - trigger.Length;
            for (int startIndex = maximumStartIndex; startIndex >= 0; startIndex--)
            {
                ReadOnlySpan<PhonemeId> candidate = observedPhonemes.Slice(startIndex, trigger.Length);
                if (HasExactPhonemeMatch(trigger.AsSpan(), candidate))
                {
                    return startIndex + trigger.Length;
                }
            }

            return 0;
        }

        private static bool HasExactPhonemeMatch(in PhonemeSequence expected, in PhonemeSequence observed)
        {
            return HasExactPhonemeMatch(expected.AsSpan(), observed.AsSpan());
        }

        private static bool HasExactPhonemeMatch(ReadOnlySpan<PhonemeId> expected, ReadOnlySpan<PhonemeId> observed)
        {
            if (expected.Length != observed.Length)
            {
                return false;
            }

            for (int phonemeIndex = 0; phonemeIndex < expected.Length; phonemeIndex++)
            {
                if (expected[phonemeIndex] != observed[phonemeIndex])
                {
                    return false;
                }
            }

            return true;
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
                candidate.FullEndPhonemeIndex,
                candidate.TriggerEndPhonemeIndex);
        }
    }
}
