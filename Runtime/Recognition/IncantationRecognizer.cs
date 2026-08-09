using System;
using H1M4W4R1.Incantia.Matching;
using H1M4W4R1.Incantia.Phonetics;
using H1M4W4R1.Incantia.Text;

namespace H1M4W4R1.Incantia.Recognition
{
    /// <summary>
    /// Runs normalization, phonemization, matching, and acceptance for text supplied by a separate transcription provider.
    /// </summary>
    public sealed class IncantationRecognizer
    {
        private readonly IPhonemizer _phonemizer;
        private readonly PhonemeInventory _inventory;
        private readonly IncantationMatcher _matcher;
        private readonly object _recognitionWorkLock = new object();

        public IncantationRecognizer(
            IPhonemizer phonemizer,
            PhonemeInventory inventory,
            IncantationMatcher matcher)
        {
            _phonemizer = phonemizer ?? throw new ArgumentNullException(nameof(phonemizer));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        }

        public IncantationRecognitionResult Recognize(in IncantationRecognitionRequest request)
        {
            if (!string.Equals(request.Language, _phonemizer.Language, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The request language does not match the phonemizer language.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Transcript))
            {
                return CreateRejectedResult(request.Sequence, request.Transcript, string.Empty, 0, RecognitionRejectionReason.NoSpeech);
            }

            lock (_recognitionWorkLock)
            {
                string normalizedTranscript = IncantationTextNormalizer.Normalize(request.Transcript);
                PhonemeSequence phonemes = _phonemizer.Phonemize(normalizedTranscript);
                if (phonemes.IsEmpty)
                {
                    return CreateRejectedResult(request.Sequence, request.Transcript, normalizedTranscript, 0, RecognitionRejectionReason.NoPhonemes);
                }

                PhoneticObservation observation = PhoneticObservation.Create(phonemes, _inventory);
                IncantationMatchResult match = _matcher.Match(request.Language, observation);
                RecognitionRejectionReason rejectionReason = GetRejectionReason(match, observation.Phonemes.Length, _matcher.Config);
                return new IncantationRecognitionResult(
                    request.Sequence,
                    request.Transcript,
                    normalizedTranscript,
                    observation.Phonemes.Length,
                    match,
                    rejectionReason);
            }
        }

        private static IncantationRecognitionResult CreateRejectedResult(
            long sequence,
            string transcript,
            string normalizedTranscript,
            int observedPhonemeCount,
            RecognitionRejectionReason rejectionReason)
        {
            return new IncantationRecognitionResult(
                sequence,
                transcript,
                normalizedTranscript,
                observedPhonemeCount,
                default,
                rejectionReason);
        }

        private static RecognitionRejectionReason GetRejectionReason(
            in IncantationMatchResult match,
            int observedPhonemeCount,
            IncantationMatcherConfig config)
        {
            if (match.Accepted)
            {
                return RecognitionRejectionReason.None;
            }

            if (!match.Best.HasCandidate)
            {
                return RecognitionRejectionReason.NoCandidate;
            }

            if (observedPhonemeCount < config.MinimumObservedPhonemeCount || match.Best.ObservedLengthRatio < config.MinimumObservedLengthRatio)
            {
                return RecognitionRejectionReason.InsufficientLength;
            }

            if (match.Best.Total < config.MinimumScore)
            {
                return RecognitionRejectionReason.BelowMinimumScore;
            }

            if (match.Margin < config.MinimumMargin)
            {
                return RecognitionRejectionReason.InsufficientMargin;
            }

            return match.Best.Incantation.HasTrigger && match.Best.Trigger < config.MinimumTriggerScore
                ? RecognitionRejectionReason.InsufficientTriggerScore
                : RecognitionRejectionReason.BelowMinimumScore;
        }
    }
}
