using System;
using System.Threading;
using System.Threading.Tasks;
using H1M4W4R1.Incantia.Matching;
using H1M4W4R1.Incantia.Phonetics;
using H1M4W4R1.Incantia.Text;

namespace H1M4W4R1.Incantia.Recognition
{
    /// <summary>
    /// Runs transcription, normalization, phonemization, matching, and acceptance as separate responsibilities.
    /// The continuation after transcription does not capture Unity's synchronization context.
    /// </summary>
    public sealed class IncantationRecognizer
    {
        private readonly IIncantationSpeechTranscriber _transcriber;
        private readonly IPhonemizer _phonemizer;
        private readonly PhonemeInventory _inventory;
        private readonly IncantationMatcher _matcher;
        private readonly SemaphoreSlim _recognitionWorkLock = new SemaphoreSlim(1, 1);

        public IncantationRecognizer(
            IIncantationSpeechTranscriber transcriber,
            IPhonemizer phonemizer,
            PhonemeInventory inventory,
            IncantationMatcher matcher)
        {
            _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
            _phonemizer = phonemizer ?? throw new ArgumentNullException(nameof(phonemizer));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        }

        public async Task<IncantationRecognitionResult> RecognizeAsync(
            IncantationRecognitionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(request.Language, _phonemizer.Language, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The request language does not match the phonemizer language.", nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            IncantationTranscription transcription = await _transcriber.TranscribeAsync(request).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!transcription.ContainsSpeech || string.IsNullOrWhiteSpace(transcription.Transcript))
            {
                return CreateRejectedResult(request.Sequence, transcription.Transcript, string.Empty, 0, RecognitionRejectionReason.NoSpeech);
            }

            await _recognitionWorkLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string normalizedTranscript = IncantationTextNormalizer.Normalize(transcription.Transcript);
                PhonemeSequence phonemes = _phonemizer.Phonemize(normalizedTranscript);
                if (phonemes.IsEmpty)
                {
                    return CreateRejectedResult(request.Sequence, transcription.Transcript, normalizedTranscript, 0, RecognitionRejectionReason.NoPhonemes);
                }

                PhoneticObservation observation = PhoneticObservation.Create(phonemes, _inventory);
                IncantationMatchResult match = _matcher.Match(request.Language, observation);
                RecognitionRejectionReason rejectionReason = GetRejectionReason(match, observation.Phonemes.Length, _matcher.Config);
                return new IncantationRecognitionResult(
                    request.Sequence,
                    transcription.Transcript,
                    normalizedTranscript,
                    observation.Phonemes.Length,
                    match,
                    rejectionReason);
            }
            finally
            {
                _recognitionWorkLock.Release();
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
