namespace H1M4W4R1.Incantia.Recognition
{
    /// <summary>Explains why a recognition attempt did not produce an accepted spell.</summary>
    public enum RecognitionRejectionReason : byte
    {
        None = 0,
        NoSpeech = 1,
        NoPhonemes = 2,
        NoCandidate = 3,
        BelowMinimumScore = 4,
        InsufficientMargin = 5,
        InsufficientLength = 6,
        InsufficientTriggerScore = 7
    }
}
