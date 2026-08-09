using System;

namespace H1M4W4R1.Incantia.Recognition
{
    /// <summary>Audio and language data for one ordered incantation-recognition attempt.</summary>
    public readonly struct IncantationRecognitionRequest
    {
        public IncantationRecognitionRequest(float[] samples, int sampleRate, string language, long sequence)
        {
            Samples = samples ?? throw new ArgumentNullException(nameof(samples));
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentException("A language identifier is required.", nameof(language));
            }

            SampleRate = sampleRate;
            Language = language;
            Sequence = sequence;
        }

        /// <summary>Caller-owned mono PCM samples. Do not mutate this array until recognition completes.</summary>
        public float[] Samples { get; }
        public int SampleRate { get; }
        public string Language { get; }
        public long Sequence { get; }
    }
}
