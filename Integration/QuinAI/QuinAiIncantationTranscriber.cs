using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using LeastSquares.Undertone;
using UnityEngine;

namespace H1M4W4R1.Incantia.Integration.QuinAI
{
    /// <summary>
    /// Main-thread bridge for Quin.AI's <see cref="SpeechEngine"/>. Whisper inference remains queued by Quin.AI on its worker task.
    /// Configure the assigned engine with the same language and transcription mode before sending requests.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuinAiIncantationTranscriber : MonoBehaviour
    {
        private sealed class PendingRequest
        {
            public PendingRequest(float[] samples, int sampleRate, string language, TaskCompletionSource<string> completionSource)
            {
                Samples = samples;
                SampleRate = sampleRate;
                Language = language;
                CompletionSource = completionSource;
            }

            public float[] Samples { get; }
            public int SampleRate { get; }
            public string Language { get; }
            public TaskCompletionSource<string> CompletionSource { get; }
        }

        [SerializeField] private SpeechEngine _engine;
        private readonly ConcurrentQueue<PendingRequest> _pendingRequests = new ConcurrentQueue<PendingRequest>();
        private bool _isDisposed;

        /// <summary>True when the assigned engine has loaded its configured Whisper model.</summary>
        public bool IsReady => _engine && _engine.Loaded;

        /// <summary>Assigns the Quin.AI engine during scene setup, before requests are queued.</summary>
        public void SetEngine(SpeechEngine engine)
        {
            if (ReferenceEquals(engine, null))
            {
                throw new ArgumentNullException(nameof(engine));
            }

            _engine = engine;
        }

        /// <summary>Sets Whisper beam search width. One beam provides the lowest latency for real-time recognition.</summary>
        public void SetBeamCount(int beamCount)
        {
            if (beamCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(beamCount));
            }

            if (ReferenceEquals(_engine, null) || !_engine)
            {
                throw new InvalidOperationException("Assign a Quin.AI SpeechEngine before setting its beam count.");
            }

            _engine.NumOfBeams = beamCount;
        }

        /// <summary>
        /// Enqueues audio for transcription from any thread. The component calls Quin.AI's queue on its next Unity main-thread update.
        /// </summary>
        public Task<string> TranscribeAsync(float[] samples, int sampleRate, string language)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(QuinAiIncantationTranscriber));
            }

            if (ReferenceEquals(samples, null))
            {
                throw new ArgumentNullException(nameof(samples));
            }

            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentException("A language identifier is required.", nameof(language));
            }

            TaskCompletionSource<string> completionSource = new TaskCompletionSource<string>();
            _pendingRequests.Enqueue(new PendingRequest(samples, sampleRate, language, completionSource));
            return completionSource.Task;
        }

        private void Update()
        {
            while (_pendingRequests.TryDequeue(out PendingRequest request))
            {
                StartTranscription(request);
            }
        }

        private async void StartTranscription(PendingRequest pendingRequest)
        {
            try
            {
                ValidateEngine(pendingRequest);
                SpeechSegment[] segments = await _engine.TranscribeSamples(pendingRequest.Samples).ConfigureAwait(false);
                pendingRequest.CompletionSource.TrySetResult(CreateTranscription(segments));
            }
            catch (Exception exception)
            {
                pendingRequest.CompletionSource.TrySetException(exception);
            }
        }

        private void OnDestroy()
        {
            _isDisposed = true;
            while (_pendingRequests.TryDequeue(out PendingRequest request))
            {
                request.CompletionSource.TrySetException(new ObjectDisposedException(nameof(QuinAiIncantationTranscriber)));
            }
        }

        private void ValidateEngine(PendingRequest request)
        {
            if (ReferenceEquals(_engine, null) || !_engine)
            {
                throw new InvalidOperationException("Assign a loaded Quin.AI SpeechEngine before requesting incantation transcription.");
            }

            if (!_engine.Loaded)
            {
                throw new InvalidOperationException("The assigned Quin.AI SpeechEngine has not finished loading its Whisper model.");
            }

            if (_engine.TranslateToEnglish)
            {
                throw new InvalidOperationException("Quin.AI SpeechEngine must use transcription mode, not translation mode, for incantation recognition.");
            }

            if (!string.Equals(_engine.SelectedLanguage, request.Language, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The Quin.AI SpeechEngine language must match the incantation request language.");
            }

            if (request.SampleRate != SpeechEngine.SampleFrequency)
            {
                throw new ArgumentException($"Quin.AI Whisper requires {SpeechEngine.SampleFrequency} Hz mono audio.", nameof(request));
            }
        }

        private static string CreateTranscription(SpeechSegment[] segments)
        {
            if (ReferenceEquals(segments, null) || segments.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                SpeechSegment segment = segments[segmentIndex];
                if (ReferenceEquals(segment, null) || string.IsNullOrWhiteSpace(segment.text))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(segment.text);
            }

            string transcript = builder.ToString();
            return transcript;
        }
    }
}
