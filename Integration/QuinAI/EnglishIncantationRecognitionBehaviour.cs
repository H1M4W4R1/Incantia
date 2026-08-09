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
    /// Reusable English microphone-to-spell behavior backed by Quin.AI Whisper. Derive from this type, add definitions,
    /// and implement protected callbacks to connect game UI and gameplay without exposing event APIs.
    /// </summary>
    public abstract class EnglishIncantationRecognitionBehaviour : MonoBehaviour
    {
        [SerializeField] private QuinAiIncantationTranscriber _transcriber;
        [SerializeField] private int _maximumRecordingSeconds = 45;

        private EnglishPhonemizer _phonemizer;
        private IncantationRecognizer _recognizer;
        private AudioClip _recordingClip;
        private bool _isRecording;
        private bool _isRecognizing;
        private bool _hasReportedReady;
        private long _nextSequence;

        /// <summary>True while microphone input is being recorded.</summary>
        public bool IsRecording => _isRecording;

        /// <summary>True while audio has been submitted for transcription and matching.</summary>
        public bool IsRecognizing => _isRecognizing;

        /// <summary>True when the assigned Quin.AI Whisper model can accept requests.</summary>
        public bool IsReady => _transcriber && _transcriber.IsReady;

        /// <summary>Phonemizer configured during initialization for UI diagnostics in derived components.</summary>
        protected EnglishPhonemizer Phonemizer => _phonemizer;

        /// <summary>Assigns the scene's main-thread Quin.AI bridge during setup.</summary>
        public void SetTranscriber(QuinAiIncantationTranscriber transcriber)
        {
            _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        }

        /// <summary>Starts microphone recording when the Whisper model is ready.</summary>
        public bool BeginRecording()
        {
            if (!IsReady || _isRecording || _isRecognizing)
            {
                return false;
            }

            _recordingClip = Microphone.Start(null, false, _maximumRecordingSeconds, 16000);
            if (ReferenceEquals(_recordingClip, null))
            {
                OnRecordingFailed("Microphone could not start.");
                return false;
            }

            _isRecording = true;
            OnRecordingStarted();
            return true;
        }

        /// <summary>Stops recording and submits the captured mono PCM samples to Whisper.</summary>
        public bool EndRecordingAndRecognize()
        {
            if (!_isRecording)
            {
                return false;
            }

            int frameCount = Microphone.GetPosition(null);
            AudioClip clip = _recordingClip;
            Microphone.End(null);
            _isRecording = false;
            if (ReferenceEquals(clip, null) || frameCount <= 0)
            {
                OnRecordingFailed("No microphone samples were captured.");
                return false;
            }

            float[] samples = GetMonoSamples(clip, frameCount);
            if (ReferenceEquals(samples, null))
            {
                OnRecordingFailed("Microphone samples could not be read.");
                return false;
            }

            RecognizeSamplesAsync(samples);
            return true;
        }

        protected virtual void Awake()
        {
            InitializeRecognizer();
        }

        protected virtual void Update()
        {
            if (!_hasReportedReady && IsReady)
            {
                _hasReportedReady = true;
                OnWhisperReady();
            }
        }

        protected virtual void OnDestroy()
        {
            if (_isRecording)
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
            return new IncantationMatcherConfig();
        }

        /// <summary>Called once when the assigned Whisper model has loaded.</summary>
        protected virtual void OnWhisperReady()
        {
        }

        /// <summary>Called after microphone recording begins.</summary>
        protected virtual void OnRecordingStarted()
        {
        }

        /// <summary>Called after microphone capture fails before Whisper is invoked.</summary>
        protected virtual void OnRecordingFailed(string message)
        {
        }

        /// <summary>Called immediately before audio is submitted for Whisper transcription.</summary>
        protected virtual void OnRecognitionStarted()
        {
        }

        /// <summary>Called on Unity's main thread after transcription and matching finish.</summary>
        protected virtual void OnRecognitionCompleted(in IncantationRecognitionResult result)
        {
        }

        /// <summary>Called when the Whisper bridge or recognition pipeline throws.</summary>
        protected virtual void OnRecognitionFailed(Exception exception)
        {
        }

        private void InitializeRecognizer()
        {
            if (ReferenceEquals(_transcriber, null) || !_transcriber)
            {
                throw new InvalidOperationException("Assign QuinAiIncantationTranscriber before initializing incantation recognition.");
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

        private async void RecognizeSamplesAsync(float[] samples)
        {
            _isRecognizing = true;
            OnRecognitionStarted();
            try
            {
                IncantationRecognitionRequest request = new IncantationRecognitionRequest(samples, 16000, "en", _nextSequence++);
                IncantationRecognitionResult result = await _recognizer.RecognizeAsync(request);
                OnRecognitionCompleted(result);
            }
            catch (Exception exception)
            {
                OnRecognitionFailed(exception);
            }
            finally
            {
                _isRecognizing = false;
            }
        }

        private static float[] GetMonoSamples(AudioClip clip, int frameCount)
        {
            int channelCount = clip.channels;
            float[] interleavedSamples = new float[frameCount * channelCount];
            if (!clip.GetData(interleavedSamples, 0))
            {
                return null;
            }

            if (channelCount == 1)
            {
                return interleavedSamples;
            }

            float[] monoSamples = new float[frameCount];
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameTotal = 0f;
                int offset = frameIndex * channelCount;
                for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
                {
                    frameTotal += interleavedSamples[offset + channelIndex];
                }

                monoSamples[frameIndex] = frameTotal / channelCount;
            }

            return monoSamples;
        }
    }
}
