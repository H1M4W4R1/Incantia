using H1M4W4R1.Incantia.Database;

namespace H1M4W4R1.Incantia.Matching
{
    /// <summary>Component diagnostics for one candidate incantation.</summary>
    public readonly struct CandidateScore
    {
        public CandidateScore(
            CompiledIncantation incantation,
            float total,
            float fullPhoneme,
            float consonantSkeleton,
            float trigger,
            float observedLengthRatio)
        {
            Incantation = incantation;
            Total = total;
            FullPhoneme = fullPhoneme;
            ConsonantSkeleton = consonantSkeleton;
            Trigger = trigger;
            ObservedLengthRatio = observedLengthRatio;
        }

        public CompiledIncantation Incantation { get; }
        public bool HasCandidate => !ReferenceEquals(Incantation, null);
        public float Total { get; }
        public float FullPhoneme { get; }
        public float ConsonantSkeleton { get; }
        public float Trigger { get; }
        public float ObservedLengthRatio { get; }
    }
}
