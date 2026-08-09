using System;

namespace H1M4W4R1.Incantia.Recognition
{
    /// <summary>Text evidence returned by a replaceable speech-transcription backend.</summary>
    public readonly struct IncantationTranscription
    {
        public IncantationTranscription(string transcript, bool containsSpeech)
        {
            Transcript = transcript ?? string.Empty;
            ContainsSpeech = containsSpeech;
        }

        public string Transcript { get; }
        public bool ContainsSpeech { get; }
        public static IncantationTranscription NoSpeech => new IncantationTranscription(string.Empty, false);
    }
}
