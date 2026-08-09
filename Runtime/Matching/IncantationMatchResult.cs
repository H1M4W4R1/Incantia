namespace H1M4W4R1.Incantia.Matching
{
    /// <summary>Best and second-best closed-set candidates, including the acceptance decision.</summary>
    public readonly struct IncantationMatchResult
    {
        public IncantationMatchResult(CandidateScore best, CandidateScore second, float margin, bool accepted)
        {
            Best = best;
            Second = second;
            Margin = margin;
            Accepted = accepted;
        }

        public CandidateScore Best { get; }
        public CandidateScore Second { get; }
        public float Margin { get; }
        public bool Accepted { get; }
    }
}
