using System;
using System.Collections.Generic;
using H1M4W4R1.Incantia.Database;
using H1M4W4R1.Incantia.Matching;
using H1M4W4R1.Incantia.Phonetics;
using H1M4W4R1.Incantia.Phonetics.English;
using H1M4W4R1.Incantia.Recognition;
using UnityEngine;
using UnityEngine.Serialization;

namespace H1M4W4R1.Incantia.Integration.QuinAI
{
    /// <summary>
    /// Reusable English real-time microphone-to-spell behavior backed by Quin.AI Whisper. It retains a bounded cache of
    /// active microphone samples and retranscribes that audio as context grows. Accepted audio is consumed through the
    /// matched phoneme endpoint while trailing and newly captured samples remain available for the next spell.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class EnglishRealtimeIncantationRecognitionBehaviour : MonoBehaviour
    {
        private sealed class BoundedAudioSampleBuffer
        {
            private readonly float[] _samples;
            private int _startIndex;
            private int _count;
            private long _nextSampleIndex;

            public BoundedAudioSampleBuffer(int capacity)
            {
                if (capacity <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(capacity));
                }

                _samples = new float[capacity];
            }

            public int Count => _count;
            public long FirstSampleIndex => _nextSampleIndex - _count;
            public long NextSampleIndex => _nextSampleIndex;

            public void Append(float[] samples)
            {
                if (ReferenceEquals(samples, null))
                {
                    throw new ArgumentNullException(nameof(samples));
                }

                if (samples.Length == 0)
                {
                    return;
                }

                _nextSampleIndex += samples.Length;
                if (samples.Length >= _samples.Length)
                {
                    int sourceOffset = samples.Length - _samples.Length;
                    Array.Copy(samples, sourceOffset, _samples, 0, _samples.Length);
                    _startIndex = 0;
                    _count = _samples.Length;
                    return;
                }

                int overflowCount = Math.Max(0, _count + samples.Length - _samples.Length);
                ConsumeOldest(overflowCount);
                int writeIndex = (_startIndex + _count) % _samples.Length;
                int firstCopyCount = Math.Min(samples.Length, _samples.Length - writeIndex);
                Array.Copy(samples, 0, _samples, writeIndex, firstCopyCount);
                int secondCopyCount = samples.Length - firstCopyCount;
                if (secondCopyCount > 0)
                {
                    Array.Copy(samples, firstCopyCount, _samples, 0, secondCopyCount);
                }

                _count += samples.Length;
            }

            public float[] CopyToArray(out long firstSampleIndex)
            {
                firstSampleIndex = FirstSampleIndex;
                if (_count == 0)
                {
                    return Array.Empty<float>();
                }

                float[] output = new float[_count];
                int firstCopyCount = Math.Min(_count, _samples.Length - _startIndex);
                Array.Copy(_samples, _startIndex, output, 0, firstCopyCount);
                int secondCopyCount = _count - firstCopyCount;
                if (secondCopyCount > 0)
                {
                    Array.Copy(_samples, 0, output, firstCopyCount, secondCopyCount);
                }

                return output;
            }

            public void ConsumeThrough(long exclusiveSampleIndex)
            {
                long samplesToConsume = exclusiveSampleIndex - FirstSampleIndex;
                if (samplesToConsume <= 0)
                {
                    return;
                }

                ConsumeOldest((int)Math.Min(_count, samplesToConsume));
            }

            public void Clear()
            {
                _startIndex = 0;
                _count = 0;
                _nextSampleIndex = 0;
            }

            private void ConsumeOldest(int sampleCount)
            {
                if (sampleCount <= 0)
                {
                    return;
                }

                int consumedSampleCount = Math.Min(_count, sampleCount);
                _startIndex = (_startIndex + consumedSampleCount) % _samples.Length;
                _count -= consumedSampleCount;
            }
        }

        private const int SampleRate = 16000;
        private const int RealtimeWhisperBeamCount = 1;
        private const float HardMaximumCachedAudioDurationInSeconds = 120f;

        [SerializeField] private QuinAiIncantationTranscriber _transcriber;
        [FormerlySerializedAs("_initialStepSizeInSeconds")]
        [SerializeField] private float _captureStepSizeInSeconds = 0.25f;
        [SerializeField] private float _minimumNewAudioDurationForRecognition = 0.75f;
        [FormerlySerializedAs("_whisperBeamCount")]
        [SerializeField] private int _finalWhisperBeamCount = 1;
        [SerializeField] private float _voiceActivityThreshold = 0.004f;
        [SerializeField] private int _voiceActivityWindowCount = 3;
        [SerializeField] private float _maximumCachedAudioDurationInSeconds = 30f;

        private EnglishPhonemizer _phonemizer;
        private IncantationRecognizer _recognizer;
        private BoundedAudioSampleBuffer _cachedAudio;
        private AudioClip _recordingClip;
        private float[] _interleavedSamples = Array.Empty<float>();
        private int _stepSizeInFrames;
        private int _minimumNewSamplesForRecognition;
        private int _lastSamplePosition;
        private int _remainingVoiceWindows;
        private bool _isListening;
        private bool _isRecognizing;
        private bool _finalRecognitionPending;
        private bool _highAccuracyRecognitionPending;
        private bool _hasReportedReady;
        private long _lastSubmittedEndSampleIndex;
        private long _nextSequence;
        private long _listeningSession;

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
            _recordingClip = Microphone.Start(null, true, recordingLengthInSeconds, SampleRate);
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
            _cachedAudio.Clear();
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
            config.SuppressTriggerOnlyRecognitionDuringPartialIncantation = true;
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

            _transcriber.SetBeamCount(RealtimeWhisperBeamCount);

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
            _recognizer = new IncantationRecognizer(_phonemizer, costModel.Inventory, matcher);
        }

        private void RecalculateCaptureSettings()
        {
            if (float.IsNaN(_captureStepSizeInSeconds) ||
                float.IsInfinity(_captureStepSizeInSeconds) ||
                _captureStepSizeInSeconds <= 0f)
            {
                throw new InvalidOperationException("Capture step size must be finite and positive.");
            }

            if (float.IsNaN(_minimumNewAudioDurationForRecognition) ||
                float.IsInfinity(_minimumNewAudioDurationForRecognition) ||
                _minimumNewAudioDurationForRecognition < _captureStepSizeInSeconds)
            {
                throw new InvalidOperationException(
                    "Minimum new audio duration for recognition must be finite and at least the capture step size.");
            }

            if (_voiceActivityThreshold < 0f)
            {
                throw new InvalidOperationException("Voice activity threshold must not be negative.");
            }

            if (_finalWhisperBeamCount < 1)
            {
                throw new InvalidOperationException("Final Whisper beam count must be at least one.");
            }

            if (_voiceActivityWindowCount < 1)
            {
                throw new InvalidOperationException("Voice activity window count must be at least one.");
            }

            if (float.IsNaN(_maximumCachedAudioDurationInSeconds) ||
                float.IsInfinity(_maximumCachedAudioDurationInSeconds))
            {
                throw new InvalidOperationException("Maximum cached audio duration must be finite.");
            }

            if (_captureStepSizeInSeconds > HardMaximumCachedAudioDurationInSeconds)
            {
                throw new InvalidOperationException(
                    $"Capture step size must not exceed the hard {HardMaximumCachedAudioDurationInSeconds}-second audio cache limit.");
            }

            if (_maximumCachedAudioDurationInSeconds < _captureStepSizeInSeconds)
            {
                throw new InvalidOperationException("Maximum cached audio duration must be at least the capture step size.");
            }

            float effectiveCacheDuration = Mathf.Min(
                _maximumCachedAudioDurationInSeconds,
                HardMaximumCachedAudioDurationInSeconds);
            int maximumCachedSampleCount = Mathf.CeilToInt(effectiveCacheDuration * SampleRate);
            _cachedAudio = new BoundedAudioSampleBuffer(maximumCachedSampleCount);
            _minimumNewSamplesForRecognition = Mathf.CeilToInt(
                _minimumNewAudioDurationForRecognition * SampleRate);
            SetStepSizeInFrames(Mathf.CeilToInt(_captureStepSizeInSeconds * SampleRate));
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

            _lastSamplePosition = (_lastSamplePosition + _stepSizeInFrames) % _recordingClip.samples;
            bool isVoiceActive = IsVoiceActive(currentSamples);
            bool shouldCacheSamples = isVoiceActive || _remainingVoiceWindows > 0;
            if (!shouldCacheSamples)
            {
                return;
            }

            if (isVoiceActive)
            {
                _remainingVoiceWindows = _voiceActivityWindowCount;
                _finalRecognitionPending = false;
                _highAccuracyRecognitionPending = false;
            }
            else
            {
                _remainingVoiceWindows--;
            }

            _cachedAudio.Append(currentSamples);
            if (!isVoiceActive && _remainingVoiceWindows == 0)
            {
                _finalRecognitionPending = true;
                _highAccuracyRecognitionPending = false;
            }

            TryStartRecognition(_listeningSession, false);
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

        private void TryStartRecognition(long listeningSession, bool force)
        {
            if (_isRecognizing || _cachedAudio.Count == 0)
            {
                return;
            }

            long newSampleCount = _cachedAudio.NextSampleIndex - _lastSubmittedEndSampleIndex;
            if (!force &&
                !_finalRecognitionPending &&
                !_highAccuracyRecognitionPending &&
                newSampleCount < _minimumNewSamplesForRecognition)
            {
                return;
            }

            float[] samples = _cachedAudio.CopyToArray(out long firstSampleIndex);
            if (samples.Length == 0)
            {
                return;
            }

            bool isHighAccuracyRecognition = _highAccuracyRecognitionPending;
            bool isFinalRecognition = _finalRecognitionPending || isHighAccuracyRecognition;
            _finalRecognitionPending = false;
            _highAccuracyRecognitionPending = false;
            _lastSubmittedEndSampleIndex = firstSampleIndex + samples.Length;
            int beamCount = isHighAccuracyRecognition ? _finalWhisperBeamCount : RealtimeWhisperBeamCount;
            _transcriber.SetBeamCount(beamCount);
            _isRecognizing = true;
            OnRecognitionStarted();
            RecognizeSamplesAsync(
                samples,
                firstSampleIndex,
                listeningSession,
                _nextSequence++,
                isFinalRecognition,
                isHighAccuracyRecognition);
        }

        private async void RecognizeSamplesAsync(
            float[] samples,
            long firstSampleIndex,
            long listeningSession,
            long sequence,
            bool isFinalRecognition,
            bool isHighAccuracyRecognition)
        {
            bool accepted = false;
            bool recognitionCompleted = false;
            long submittedEndSampleIndex = firstSampleIndex + samples.Length;
            try
            {
                string transcript = await _transcriber.TranscribeAsync(samples, SampleRate, "en");
                if (!_isListening || listeningSession != _listeningSession)
                {
                    return;
                }

                IncantationRecognitionRequest request = new IncantationRecognitionRequest(transcript, "en", sequence);
                IncantationRecognitionResult result = _recognizer.Recognize(request);
                recognitionCompleted = true;
                OnRecognitionUpdated(result);
                if (result.Accepted)
                {
                    accepted = true;
                    ConsumeCachedAudioAfterSpell(samples.Length, firstSampleIndex, result);
                    OnSpellRecognized(result);
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
                    bool capturedMoreAudio = _cachedAudio.NextSampleIndex > submittedEndSampleIndex;
                    if (accepted && _cachedAudio.Count > 0)
                    {
                        TryStartRecognition(listeningSession, true);
                    }
                    else if (recognitionCompleted &&
                             isFinalRecognition &&
                             !isHighAccuracyRecognition &&
                             _finalWhisperBeamCount > RealtimeWhisperBeamCount &&
                             !capturedMoreAudio &&
                             !_finalRecognitionPending)
                    {
                        _highAccuracyRecognitionPending = true;
                        TryStartRecognition(listeningSession, false);
                    }
                    else if (_finalRecognitionPending ||
                             _highAccuracyRecognitionPending ||
                             capturedMoreAudio)
                    {
                        TryStartRecognition(listeningSession, false);
                    }
                }
            }
        }

        private void SetStepSizeInFrames(int requestedStepSize)
        {
            _stepSizeInFrames = Math.Max(1, requestedStepSize);
            _interleavedSamples = Array.Empty<float>();
        }

        private int GetRecordingLengthInSeconds()
        {
            int requiredFrames = _stepSizeInFrames * 2;
            return Mathf.Max(2, Mathf.CeilToInt(requiredFrames / (float)SampleRate));
        }

        private void ClearListeningState()
        {
            _cachedAudio.Clear();
            _lastSamplePosition = 0;
            _remainingVoiceWindows = 0;
            _finalRecognitionPending = false;
            _highAccuracyRecognitionPending = false;
            _lastSubmittedEndSampleIndex = 0;
            SetStepSizeInFrames(Mathf.CeilToInt(_captureStepSizeInSeconds * SampleRate));
        }

        private void ConsumeCachedAudioAfterSpell(
            int submittedSampleCount,
            long firstSampleIndex,
            in IncantationRecognitionResult result)
        {
            int consumedSampleCount = IncantationTranscriptConsumer.GetConsumedSampleCount(
                submittedSampleCount,
                result);
            _cachedAudio.ConsumeThrough(firstSampleIndex + consumedSampleCount);
        }
    }
}
