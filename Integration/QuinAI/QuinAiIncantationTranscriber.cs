using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using H1M4W4R1.Incantia.Recognition;
using LeastSquares.Undertone;
using UnityEngine;

namespace H1M4W4R1.Incantia.Integration.QuinAI
{
    /// <summary>
    /// Main-thread bridge for Quin.AI's <see cref="SpeechEngine"/>. Whisper inference remains queued by Quin.AI on its worker task.
    /// Configure the assigned engine with the same language and transcription mode before sending requests.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuinAiIncantationTranscriber : MonoBehaviour, IIncantationSpeechTranscriber
    {
        private sealed class PendingRequest
        {
            public PendingRequest(IncantationRecognitionRequest request, TaskCompletionSource<IncantationTranscription> completionSource)
            {
                Request = request;
                CompletionSource = completionSource;
            }

            public IncantationRecognitionRequest Request { get; }
            public TaskCompletionSource<IncantationTranscription> CompletionSource { get; }
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

        /// <summary>
        /// Enqueues a request from any thread. The component calls Quin.AI's queue on its next Unity main-thread update.
        /// Cancellation deliberately discards results at the recognizer layer because Quin.AI inference cannot be interrupted safely.
        /// </summary>
        public Task<IncantationTranscription> TranscribeAsync(IncantationRecognitionRequest request)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(QuinAiIncantationTranscriber));
            }

            TaskCompletionSource<IncantationTranscription> completionSource = new TaskCompletionSource<IncantationTranscription>();
            _pendingRequests.Enqueue(new PendingRequest(request, completionSource));
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
                ValidateEngine(pendingRequest.Request);
                SpeechSegment[] segments = await _engine.TranscribeSamples(pendingRequest.Request.Samples).ConfigureAwait(false);
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

        private void ValidateEngine(in IncantationRecognitionRequest request)
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

        private static IncantationTranscription CreateTranscription(SpeechSegment[] segments)
        {
            if (ReferenceEquals(segments, null) || segments.Length == 0)
            {
                return IncantationTranscription.NoSpeech;
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
            return new IncantationTranscription(transcript, transcript.Length > 0);
        }
    }
}
