using System;
using H1M4W4R1.Incantia.Matching;
using H1M4W4R1.Incantia.Phonetics;

namespace H1M4W4R1.Incantia.Recognition
{
    /// <summary>
    /// Removes only the leading speech through an accepted spell. It phonemizes each source word once to map the
    /// matcher's phoneme endpoint back to a text boundary, leaving later words available for the next recognition pass.
    /// </summary>
    public static class IncantationTranscriptConsumer
    {
        public static string ConsumeAcceptedTranscript(
            string transcript,
            IPhonemizer phonemizer,
            in IncantationRecognitionResult acceptedResult)
        {
            if (string.IsNullOrEmpty(transcript) || ReferenceEquals(phonemizer, null) || !acceptedResult.Accepted)
            {
                return transcript ?? string.Empty;
            }

            int requiredPhonemeCount = GetConsumedPhonemeCount(acceptedResult);
            if (requiredPhonemeCount <= 0)
            {
                return string.Empty;
            }

            int wordStartIndex = 0;
            int consumedPhonemeCount = 0;
            while (TryGetNextWord(transcript, wordStartIndex, out int wordEndExclusive))
            {
                string word = transcript.Substring(wordStartIndex, wordEndExclusive - wordStartIndex);
                consumedPhonemeCount += phonemizer.Phonemize(word).Length;
                if (consumedPhonemeCount >= requiredPhonemeCount)
                {
                    return TrimLeadingWhitespace(transcript, wordEndExclusive);
                }

                wordStartIndex = wordEndExclusive;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the exclusive observed-phoneme endpoint consumed by an accepted result. This includes any leading
        /// speech before the accepted incantation so stale input is not reconsidered.
        /// </summary>
        public static int GetConsumedPhonemeCount(in IncantationRecognitionResult acceptedResult)
        {
            if (!acceptedResult.Accepted)
            {
                return 0;
            }

            CandidateScore best = acceptedResult.Match.Best;
            return acceptedResult.Match.MatchKind == IncantationMatchKind.FullIncantation
                ? best.FullIncantationEndPhonemeIndex
                : best.TriggerEndPhonemeIndex;
        }

        /// <summary>
        /// Maps an accepted phoneme endpoint to the corresponding exclusive sample offset in the transcribed audio.
        /// Whisper supplies no word timestamps in the Quin.AI backend, so the endpoint is proportional to observed
        /// phoneme progress and is clamped to the submitted sample range.
        /// </summary>
        public static int GetConsumedSampleCount(
            int submittedSampleCount,
            in IncantationRecognitionResult acceptedResult)
        {
            if (submittedSampleCount <= 0 || !acceptedResult.Accepted)
            {
                return 0;
            }

            int observedPhonemeCount = acceptedResult.ObservedPhonemeCount;
            int consumedPhonemeCount = GetConsumedPhonemeCount(acceptedResult);
            if (observedPhonemeCount <= 0 || consumedPhonemeCount >= observedPhonemeCount)
            {
                return submittedSampleCount;
            }

            if (consumedPhonemeCount <= 0)
            {
                return 0;
            }

            double consumedRatio = (double)consumedPhonemeCount / observedPhonemeCount;
            int consumedSampleCount = (int)Math.Ceiling(submittedSampleCount * consumedRatio);
            return Math.Min(submittedSampleCount, consumedSampleCount);
        }

        private static bool TryGetNextWord(string text, int startIndex, out int wordEndExclusive)
        {
            int characterIndex = startIndex;
            while (characterIndex < text.Length && char.IsWhiteSpace(text[characterIndex]))
            {
                characterIndex++;
            }

            int wordStartIndex = characterIndex;
            while (characterIndex < text.Length && !char.IsWhiteSpace(text[characterIndex]))
            {
                characterIndex++;
            }

            wordEndExclusive = characterIndex;
            return wordEndExclusive > wordStartIndex;
        }

        private static string TrimLeadingWhitespace(string text, int startIndex)
        {
            int characterIndex = startIndex;
            while (characterIndex < text.Length && char.IsWhiteSpace(text[characterIndex]))
            {
                characterIndex++;
            }

            return characterIndex >= text.Length ? string.Empty : text.Substring(characterIndex);
        }
    }
}
