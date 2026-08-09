using System.Threading.Tasks;

namespace H1M4W4R1.Incantia.Recognition
{
    /// <summary>Transcribes audio for an explicitly selected incantation language.</summary>
    public interface IIncantationSpeechTranscriber
    {
        Task<IncantationTranscription> TranscribeAsync(IncantationRecognitionRequest request);
    }
}
