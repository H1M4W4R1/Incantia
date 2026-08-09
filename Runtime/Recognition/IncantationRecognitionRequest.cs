using System;

namespace H1M4W4R1.Incantia.Recognition
{
    /// <summary>Transcribed text and language data for one ordered incantation-recognition attempt.</summary>
    public readonly struct IncantationRecognitionRequest
    {
        public IncantationRecognitionRequest(string transcript, string language, long sequence)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentException("A language identifier is required.", nameof(language));
            }

            Transcript = transcript ?? string.Empty;
            Language = language;
            Sequence = sequence;
        }

        /// <summary>Transcript returned by the selected speech-transcription provider.</summary>
        public string Transcript { get; }
        public string Language { get; }
        public long Sequence { get; }
    }
}
