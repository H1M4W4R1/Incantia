using System;

namespace H1M4W4R1.Incantia.Database
{
    /// <summary>Human-readable authoring input used to compile a spell incantation.</summary>
    [Serializable]
    public sealed class IncantationDefinition
    {
        public IncantationDefinition(string spellId, string language, string text, string triggerText = null)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                throw new ArgumentException("A spell identifier is required.", nameof(spellId));
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentException("A language identifier is required.", nameof(language));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Incantation text is required.", nameof(text));
            }

            SpellId = spellId;
            Language = language;
            Text = text;
            TriggerText = triggerText;
        }

        public string SpellId { get; }
        public string Language { get; }
        public string Text { get; }
        public string TriggerText { get; }
    }
}
