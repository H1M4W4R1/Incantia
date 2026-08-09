using System;
using System.Collections.Generic;
using H1M4W4R1.Incantia.Database;
using H1M4W4R1.Incantia.Matching;
using H1M4W4R1.Incantia.Phonetics;
using H1M4W4R1.Incantia.Phonetics.English;
using H1M4W4R1.Incantia.Recognition;
using UnityEngine;

namespace H1M4W4R1.Incantia.Integration.QuinAI
{
    /// <summary>
    /// Reusable English real-time microphone-to-spell behavior backed by Quin.AI Whisper. It uses overlapping microphone
    /// windows and voice activity gating based on Quin.AI's real-time transcription pattern. Derive from this type to add
    /// definitions and react to accepted spells through protected callbacks.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class EnglishRealtimeIncantationRecognitionBehaviour : MonoBehaviour
    {
        private readonly struct PendingRecognitionWindow
        {
            public PendingRecognitionWindow(float[] samples, long endFrame)
            {
                Samples = samples;
                EndFrame = endFrame;
            }

            public float[] Samples { get; }
            public long EndFrame { get; }
        }

        [SerializeField] private QuinAiIncantationTranscriber _transcriber;
        [SerializeField] private float _initialStepSizeInSeconds = 1.5f;
        [SerializeField] private bool _autoAdjustStepSize = true;
        [SerializeField] private float _maximumWindowLengthInSeconds = 12f;
        [SerializeField] private float _voiceActivityThreshold = 0.004f;
        [SerializeField] private int _voiceActivityWindowCount = 3;
        [SerializeField] private int _maximumQueuedRecognitionWindows = 8;
        [SerializeField] private float _sameSpellRepeatGuardSeconds = 3f;

        private readonly List<float> _previousSamples = new List<float>();
        private readonly Queue<PendingRecognitionWindow> _pendingRecognitionWindows = new Queue<PendingRecognitionWindow>();
        private EnglishPhonemizer _phonemizer;
        private IncantationRecognizer _recognizer;
        private AudioClip _recordingClip;
        private float[] _interleavedSamples = Array.Empty<float>();
        private int _stepSizeInFrames;
        private int _maximumWindowSizeInFrames;
        private int _keepSizeInFrames;
        private int _lastSamplePosition;
        private int _remainingVoiceWindows;
        private bool _isListening;
        private bool _isRecognizing;
        private bool _hasReportedReady;
        private long _nextSequence;
        private long _listeningSession;
        private long _capturedFrameCount;
        private long _lastRecognizedSpellEndFrame = -1;
        private long _lastConsumedTriggerEndFrame = -1;
        private string _lastRecognizedSnapshot = string.Empty;
        private string _lastRecognizedSpellId = string.Empty;
        private float _recognitionStartedAt;

        /// <summary>True while this component is collecting microphone samples.</summary>
        public bool IsListening => _isListening;

        /// <summary>True while the current audio window is being transcribed and matched.</summary>
        public bool IsRecognizing => _isRecognizing;

        /// <summary>True when the assigned Quin.AI Whisper model can accept a real-time request.</summary>
        public bool IsReady => _transcriber && _transcriber.IsReady;

        /// <summary>Phonemizer configured during initialization for UI diagnostics in derived components.</summary>
        protected EnglishPhonemizer Phonemizer => _phonemizer;

        /// <summary>Assigns the scene's main-thread Quin.AI bridge during setup.</summary>
        public void SetTranscriber(QuinAiIncantationTranscriber transcriber)
        {
            _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        }

        /// <summary>Begins continuous microphone capture when the Whisper model is ready.</summary>
        public bool BeginListening()
        {
            if (!IsReady || _isListening)
            {
                return false;
            }

            ClearListeningState();
            int recordingLengthInSeconds = GetRecordingLengthInSeconds();
            _recordingClip = Microphone.Start(null, true, recordingLengthInSeconds, 16000);
            if (ReferenceEquals(_recordingClip, null))
            {
                OnListeningFailed("Microphone could not start.");
                return false;
            }

            _isListening = true;
            _listeningSession++;
            OnListeningStarted();
            return true;
        }

        /// <summary>Stops continuous microphone capture. A completed recognition from an earlier listening session is ignored.</summary>
        public bool StopListening()
        {
            if (!_isListening)
            {
                return false;
            }

            _isListening = false;
            _listeningSession++;
            _pendingRecognitionWindows.Clear();
            Microphone.End(null);
            OnListeningStopped();
            return true;
        }

        protected virtual void Awake()
        {
            InitializeRecognizer();
            RecalculateCaptureSettings();
        }

        protected virtual void Update()
        {
            if (!_hasReportedReady && IsReady)
            {
                _hasReportedReady = true;
                OnWhisperReady();
            }

            if (_isListening)
            {
                CaptureAndRecognizeWindow();
            }
        }

        protected virtual void OnDestroy()
        {
            if (_isListening)
            {
                Microphone.End(null);
            }
        }

        /// <summary>Register reviewed pronunciation overrides before the supplied definitions are compiled.</summary>
        protected virtual void ConfigurePhonemizer(EnglishPhonemizer phonemizer)
        {
        }

        /// <summary>Add every spell definition supported by this behavior.</summary>
        protected abstract void AddIncantationDefinitions(List<IncantationDefinition> definitions);

        /// <summary>Override initial scores and acceptance safeguards for this game's data set.</summary>
        protected virtual IncantationMatcherConfig CreateMatcherConfig()
        {
            IncantationMatcherConfig config = new IncantationMatcherConfig();
            config.AllowTrailingSpeech = true;
            return config;
        }

        /// <summary>Called once when the assigned Whisper model has loaded.</summary>
        protected virtual void OnWhisperReady()
        {
        }

        /// <summary>Called after continuous microphone capture begins.</summary>
        protected virtual void OnListeningStarted()
        {
        }

        /// <summary>Called after continuous microphone capture stops.</summary>
        protected virtual void OnListeningStopped()
        {
        }

        /// <summary>Called when microphone capture cannot begin or read the next window.</summary>
        protected virtual void OnListeningFailed(string message)
        {
        }

        /// <summary>Called immediately before an active voice window is submitted to Whisper.</summary>
        protected virtual void OnRecognitionStarted()
        {
        }

        /// <summary>Called for every completed real-time recognition, including rejected snapshots, on Unity's main thread.</summary>
        protected virtual void OnRecognitionUpdated(in IncantationRecognitionResult result)
        {
        }

        /// <summary>Called only once per accepted transcript snapshot. Rejected and ambiguous snapshots are ignored.</summary>
        protected virtual void OnSpellRecognized(in IncantationRecognitionResult result)
        {
        }

        /// <summary>Called when the Whisper bridge or real-time recognition pipeline throws.</summary>
        protected virtual void OnRecognitionFailed(Exception exception)
        {
        }

        private void InitializeRecognizer()
        {
            if (ReferenceEquals(_transcriber, null) || !_transcriber)
            {
                throw new InvalidOperationException("Assign QuinAiIncantationTranscriber before initializing real-time incantation recognition.");
            }

            _phonemizer = new EnglishPhonemizer();
            ConfigurePhonemizer(_phonemizer);
            PhonemeCostModel costModel = EnglishPhonemeProfile.CreateCostModel();
            WeightedPhonemeDistance distance = new WeightedPhonemeDistance(costModel);
            IncantationCompiler compiler = new IncantationCompiler(_phonemizer, distance);
            List<IncantationDefinition> definitions = new List<IncantationDefinition>();
            AddIncantationDefinitions(definitions);
            if (definitions.Count == 0)
            {
                throw new InvalidOperationException("Add at least one incantation definition.");
            }

            List<CompiledIncantation> compiledIncantations = new List<CompiledIncantation>(definitions.Count);
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                IncantationDefinition definition = definitions[definitionIndex];
                if (ReferenceEquals(definition, null))
                {
                    throw new InvalidOperationException("Incantation definitions must not contain null values.");
                }

                _phonemizer.RegisterFallbackPronunciation(definition.Text);
                if (!string.IsNullOrWhiteSpace(definition.TriggerText))
                {
                    _phonemizer.RegisterFallbackPronunciation(definition.TriggerText);
                }

                compiledIncantations.Add(compiler.Compile(definition));
            }

            IncantationMatcher matcher = new IncantationMatcher(compiledIncantations, distance, CreateMatcherConfig());
            _recognizer = new IncantationRecognizer(_transcriber, _phonemizer, costModel.Inventory, matcher);
        }

        private void RecalculateCaptureSettings()
        {
            if (_initialStepSizeInSeconds <= 0f)
            {
                throw new InvalidOperationException("Initial step size must be positive.");
            }

            if (_maximumWindowLengthInSeconds < _initialStepSizeInSeconds)
            {
                throw new InvalidOperationException("Maximum window length must be at least the initial step size.");
            }

            if (_voiceActivityThreshold < 0f)
            {
                throw new InvalidOperationException("Voice activity threshold must not be negative.");
            }

            if (_voiceActivityWindowCount < 1)
            {
                throw new InvalidOperationException("Voice activity window count must be at least one.");
            }

            if (_maximumQueuedRecognitionWindows < 1)
            {
                throw new InvalidOperationException("Maximum queued recognition windows must be at least one.");
            }

            if (_sameSpellRepeatGuardSeconds < 0f)
            {
                throw new InvalidOperationException("Same-spell repeat guard must not be negative.");
            }

            _maximumWindowSizeInFrames = Mathf.CeilToInt(_maximumWindowLengthInSeconds * 16000f);
            _keepSizeInFrames = Mathf.CeilToInt(0.5f * 16000f);
            SetStepSizeInFrames(Mathf.CeilToInt(_initialStepSizeInSeconds * 16000f));
        }

        private void CaptureAndRecognizeWindow()
        {
            if (ReferenceEquals(_recordingClip, null) || !_recordingClip)
            {
                StopListening();
                OnListeningFailed("Microphone recording clip is unavailable.");
                return;
            }

            int currentPosition = Microphone.GetPosition(null);
            int availableFrames = GetAvailableFrames(currentPosition);
            if (availableFrames < _stepSizeInFrames)
            {
                return;
            }

            float[] currentSamples = ReadCurrentMonoSamples();
            if (ReferenceEquals(currentSamples, null))
            {
                StopListening();
                OnListeningFailed("Microphone samples could not be read.");
                return;
            }

            float[] windowSamples = CreateWindowSamples(currentSamples);
            AppendPreviousSamples(currentSamples);
            _lastSamplePosition = (_lastSamplePosition + _stepSizeInFrames) % _recordingClip.samples;
            _capturedFrameCount += currentSamples.Length;
            if (IsVoiceActive(windowSamples))
            {
                _remainingVoiceWindows = _voiceActivityWindowCount;
            }
            else if (_remainingVoiceWindows > 0)
            {
                _remainingVoiceWindows--;
            }

            if (_remainingVoiceWindows <= 0)
            {
                return;
            }

            if (_isRecognizing)
            {
                QueueRecognitionWindow(windowSamples, _capturedFrameCount);
                return;
            }

            StartRecognition(windowSamples, _listeningSession, _capturedFrameCount);
        }

        private int GetAvailableFrames(int currentPosition)
        {
            if (currentPosition >= _lastSamplePosition)
            {
                return currentPosition - _lastSamplePosition;
            }

            return currentPosition + _recordingClip.samples - _lastSamplePosition;
        }

        private float[] ReadCurrentMonoSamples()
        {
            int channelCount = _recordingClip.channels;
            int interleavedLength = _stepSizeInFrames * channelCount;
            if (_interleavedSamples.Length != interleavedLength)
            {
                _interleavedSamples = new float[interleavedLength];
            }

            if (!_recordingClip.GetData(_interleavedSamples, _lastSamplePosition))
            {
                return null;
            }

            if (channelCount == 1)
            {
                float[] monoSamples = new float[_stepSizeInFrames];
                Array.Copy(_interleavedSamples, monoSamples, monoSamples.Length);
                return monoSamples;
            }

            float[] downmixedSamples = new float[_stepSizeInFrames];
            for (int frameIndex = 0; frameIndex < _stepSizeInFrames; frameIndex++)
            {
                float frameTotal = 0f;
                int interleavedOffset = frameIndex * channelCount;
                for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
                {
                    frameTotal += _interleavedSamples[interleavedOffset + channelIndex];
                }

                downmixedSamples[frameIndex] = frameTotal / channelCount;
            }

            return downmixedSamples;
        }

        private float[] CreateWindowSamples(float[] currentSamples)
        {
            int maximumPreviousLength = _maximumWindowSizeInFrames - currentSamples.Length;
            int previousLength = Mathf.Min(_previousSamples.Count, maximumPreviousLength);
            int previousStartIndex = _previousSamples.Count - previousLength;
            float[] windowSamples = new float[previousLength + currentSamples.Length];
            for (int sampleIndex = 0; sampleIndex < previousLength; sampleIndex++)
            {
                windowSamples[sampleIndex] = _previousSamples[previousStartIndex + sampleIndex];
            }

            Array.Copy(currentSamples, 0, windowSamples, previousLength, currentSamples.Length);
            return windowSamples;
        }

        private void AppendPreviousSamples(float[] currentSamples)
        {
            _previousSamples.AddRange(currentSamples);
            int retainedLength = _maximumWindowSizeInFrames - _keepSizeInFrames;
            if (_previousSamples.Count > retainedLength)
            {
                _previousSamples.RemoveRange(0, _previousSamples.Count - retainedLength);
            }
        }

        private bool IsVoiceActive(float[] samples)
        {
            if (ReferenceEquals(samples, null) || samples.Length == 0)
            {
                return false;
            }

            float totalAmplitude = 0f;
            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
            {
                totalAmplitude += Mathf.Abs(samples[sampleIndex]);
            }

            return (totalAmplitude / samples.Length) > _voiceActivityThreshold;
        }

        private void QueueRecognitionWindow(float[] samples, long endFrame)
        {
            if (_pendingRecognitionWindows.Count >= _maximumQueuedRecognitionWindows)
            {
                return;
            }

            _pendingRecognitionWindows.Enqueue(new PendingRecognitionWindow(samples, endFrame));
        }

        private void StartRecognition(float[] samples, long listeningSession, long endFrame)
        {
            _isRecognizing = true;
            _recognitionStartedAt = Time.realtimeSinceStartup;
            OnRecognitionStarted();
            RecognizeSamplesAsync(samples, listeningSession, _nextSequence++, endFrame);
        }

        private async void RecognizeSamplesAsync(float[] samples, long listeningSession, long sequence, long endFrame)
        {
            try
            {
                IncantationRecognitionRequest request = new IncantationRecognitionRequest(samples, 16000, "en", sequence);
                IncantationRecognitionResult result = await _recognizer.RecognizeAsync(request);
                if (_isListening && listeningSession == _listeningSession)
                {
                    OnRecognitionUpdated(result);
                    long triggerEndFrame = CalculateTriggerEndFrame(result, samples, endFrame);
                    if (result.Accepted && IsNewRecognizedSnapshot(result, triggerEndFrame))
                    {
                        ClearBufferedAudioAfterSpell(triggerEndFrame);
                        OnSpellRecognized(result);
                    }
                }
            }
            catch (Exception exception)
            {
                if (_isListening && listeningSession == _listeningSession)
                {
                    OnRecognitionFailed(exception);
                }
            }
            finally
            {
                _isRecognizing = false;
                if (_isListening && listeningSession == _listeningSession)
                {
                    ApplyAutomaticStepSize();
                    if (_pendingRecognitionWindows.Count > 0)
                    {
                        PendingRecognitionWindow pendingWindow = _pendingRecognitionWindows.Dequeue();
                        StartRecognition(pendingWindow.Samples, listeningSession, pendingWindow.EndFrame);
                    }
                }
            }
        }

        private bool IsNewRecognizedSnapshot(in IncantationRecognitionResult result, long triggerEndFrame)
        {
            string spellId = result.Match.Best.Incantation.SpellId;
            string snapshot = $"{spellId}|{result.NormalizedTranscript}";
            if (string.Equals(snapshot, _lastRecognizedSnapshot, StringComparison.Ordinal))
            {
                return false;
            }

            long repeatGuardFrames = (long)(_sameSpellRepeatGuardSeconds * 16000f);
            if (triggerEndFrame <= _lastConsumedTriggerEndFrame)
            {
                return false;
            }

            if (string.Equals(spellId, _lastRecognizedSpellId, StringComparison.Ordinal) && triggerEndFrame - _lastRecognizedSpellEndFrame <= repeatGuardFrames)
            {
                return false;
            }

            _lastRecognizedSnapshot = snapshot;
            _lastRecognizedSpellId = spellId;
            _lastRecognizedSpellEndFrame = triggerEndFrame;
            _lastConsumedTriggerEndFrame = triggerEndFrame;
            return true;
        }

        private void ApplyAutomaticStepSize()
        {
            if (!_autoAdjustStepSize)
            {
                return;
            }

            float recognitionDuration = Time.realtimeSinceStartup - _recognitionStartedAt;
            float currentStepDuration = _stepSizeInFrames / 16000f;
            if (recognitionDuration <= currentStepDuration)
            {
                return;
            }

            int adjustedStepSize = Mathf.CeilToInt((recognitionDuration + 0.1f) * 16000f);
            SetStepSizeInFrames(adjustedStepSize);
        }

        private void SetStepSizeInFrames(int requestedStepSize)
        {
            _stepSizeInFrames = Mathf.Clamp(requestedStepSize, 1, _maximumWindowSizeInFrames);
            _interleavedSamples = Array.Empty<float>();
        }

        private int GetRecordingLengthInSeconds()
        {
            int requiredFrames = _maximumWindowSizeInFrames + _stepSizeInFrames;
            return Mathf.Max(2, Mathf.CeilToInt(requiredFrames / 16000f));
        }

        private void ClearListeningState()
        {
            _previousSamples.Clear();
            _pendingRecognitionWindows.Clear();
            _lastSamplePosition = 0;
            _remainingVoiceWindows = 0;
            _capturedFrameCount = 0;
            _lastRecognizedSpellEndFrame = -1;
            _lastConsumedTriggerEndFrame = -1;
            _lastRecognizedSnapshot = string.Empty;
            _lastRecognizedSpellId = string.Empty;
        }

        private static long CalculateTriggerEndFrame(
            in IncantationRecognitionResult result,
            float[] processedSamples,
            long windowEndFrame)
        {
            int triggerEndPhonemeIndex = result.Match.Best.TriggerEndPhonemeIndex;
            int observedPhonemeCount = result.ObservedPhonemeCount;
            if (triggerEndPhonemeIndex <= 0 || observedPhonemeCount <= 0)
            {
                return windowEndFrame;
            }

            long windowStartFrame = windowEndFrame - processedSamples.Length;
            return windowStartFrame + ((long)processedSamples.Length * triggerEndPhonemeIndex / observedPhonemeCount);
        }

        private void ClearBufferedAudioAfterSpell(long triggerEndFrame)
        {
            RemoveRetainedSamplesBefore(triggerEndFrame);
            RemoveQueuedWindowsBefore(triggerEndFrame);
            _remainingVoiceWindows = 0;
        }

        private void RemoveRetainedSamplesBefore(long triggerEndFrame)
        {
            long retainedStartFrame = _capturedFrameCount - _previousSamples.Count;
            long removedFrameCount = triggerEndFrame - retainedStartFrame;
            if (removedFrameCount <= 0)
            {
                return;
            }

            int removeCount = removedFrameCount >= _previousSamples.Count ? _previousSamples.Count : (int)removedFrameCount;
            _previousSamples.RemoveRange(0, removeCount);
        }

        private void RemoveQueuedWindowsBefore(long triggerEndFrame)
        {
            int pendingWindowCount = _pendingRecognitionWindows.Count;
            for (int windowIndex = 0; windowIndex < pendingWindowCount; windowIndex++)
            {
                PendingRecognitionWindow pendingWindow = _pendingRecognitionWindows.Dequeue();
                if (pendingWindow.EndFrame > triggerEndFrame)
                {
                    _pendingRecognitionWindows.Enqueue(pendingWindow);
                }
            }
        }
    }
}
