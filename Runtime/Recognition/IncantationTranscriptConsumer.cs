using System;

namespace H1M4W4R1.Incantia.Recognition
{
    /// <summary>
    /// Removes only the leading speech through an accepted spell. Later words remain available for the next real-time
    /// recognition pass when an ASR provider returns more than one phrase in a single transcript.
    /// </summary>
    public static class IncantationTranscriptConsumer
    {
        public static string ConsumeAcceptedTranscript(
            string transcript,
            string language,
            IncantationRecognizer recognizer,
            in IncantationRecognitionResult acceptedResult)
        {
            if (string.IsNullOrEmpty(transcript) || string.IsNullOrWhiteSpace(language) || ReferenceEquals(recognizer, null) || !acceptedResult.Accepted)
            {
                return transcript ?? string.Empty;
            }

            int wordEndExclusive = 0;
            while (TryGetNextWordEnd(transcript, wordEndExclusive, out wordEndExclusive))
            {
                string candidateTranscript = transcript.Substring(0, wordEndExclusive);
                IncantationRecognitionRequest request = new IncantationRecognitionRequest(candidateTranscript, language, acceptedResult.Sequence);
                IncantationRecognitionResult candidateResult = recognizer.Recognize(request);
                if (IsSameAcceptedSpell(candidateResult, acceptedResult))
                {
                    return TrimLeadingWhitespace(transcript, wordEndExclusive);
                }
            }

            return string.Empty;
        }

        private static bool TryGetNextWordEnd(string text, int startIndex, out int wordEndExclusive)
        {
            int characterIndex = startIndex;
            while (characterIndex < text.Length && char.IsWhiteSpace(text[characterIndex]))
            {
                characterIndex++;
            }

            while (characterIndex < text.Length && !char.IsWhiteSpace(text[characterIndex]))
            {
                characterIndex++;
            }

            wordEndExclusive = characterIndex;
            return wordEndExclusive > startIndex;
        }

        private static bool IsSameAcceptedSpell(
            in IncantationRecognitionResult candidateResult,
            in IncantationRecognitionResult acceptedResult)
        {
            return candidateResult.Accepted &&
                candidateResult.Match.MatchKind == acceptedResult.Match.MatchKind &&
                string.Equals(
                    candidateResult.Match.Best.Incantation.SpellId,
                    acceptedResult.Match.Best.Incantation.SpellId,
                    StringComparison.Ordinal);
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
