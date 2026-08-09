namespace H1M4W4R1.Incantia.Matching
{
    /// <summary>Describes the accepted path used for an incantation match.</summary>
    public enum IncantationMatchKind : byte
    {
        None = 0,
        FullIncantation = 1,
        TriggerOnly = 2
    }

    /// <summary>Best and second-best closed-set candidates, including the acceptance decision.</summary>
    public readonly struct IncantationMatchResult
    {
        public IncantationMatchResult(CandidateScore best, CandidateScore second, float margin, bool accepted, IncantationMatchKind matchKind)
        {
            Best = best;
            Second = second;
            Margin = margin;
            Accepted = accepted;
            MatchKind = matchKind;
        }

        public CandidateScore Best { get; }
        public CandidateScore Second { get; }
        public float Margin { get; }
        public bool Accepted { get; }
        /// <summary>Indicates whether the accepted result used the full incantation or the opt-in trigger-only path.</summary>
        public IncantationMatchKind MatchKind { get; }
    }
}
