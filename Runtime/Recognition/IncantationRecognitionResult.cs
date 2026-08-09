using H1M4W4R1.Incantia.Matching;

namespace H1M4W4R1.Incantia.Recognition
{
    /// <summary>Complete diagnostic output for one recognition request.</summary>
    public readonly struct IncantationRecognitionResult
    {
        public IncantationRecognitionResult(
            long sequence,
            string transcript,
            string normalizedTranscript,
            int observedPhonemeCount,
            IncantationMatchResult match,
            RecognitionRejectionReason rejectionReason)
        {
            Sequence = sequence;
            Transcript = transcript;
            NormalizedTranscript = normalizedTranscript;
            ObservedPhonemeCount = observedPhonemeCount;
            Match = match;
            RejectionReason = rejectionReason;
        }

        public long Sequence { get; }
        public string Transcript { get; }
        public string NormalizedTranscript { get; }
        public int ObservedPhonemeCount { get; }
        public IncantationMatchResult Match { get; }
        public RecognitionRejectionReason RejectionReason { get; }
        public bool Accepted => Match.Accepted;
    }
}
