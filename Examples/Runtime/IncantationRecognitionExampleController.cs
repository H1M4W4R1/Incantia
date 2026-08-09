using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using H1M4W4R1.Incantia.Database;
using H1M4W4R1.Incantia.Integration.QuinAI;
using H1M4W4R1.Incantia.Matching;
using H1M4W4R1.Incantia.Phonetics;
using H1M4W4R1.Incantia.Phonetics.English;
using H1M4W4R1.Incantia.Recognition;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace H1M4W4R1.Incantia.Examples
{
    /// <summary>Playable Unity UI example for microphone-to-spell recognition with Quin.AI Whisper.</summary>
    [DisallowMultipleComponent]
    public sealed class IncantationRecognitionExampleController : MonoBehaviour
    {
        private readonly struct ExampleSpell
        {
            public ExampleSpell(string spellId, string text, string trigger)
            {
                SpellId = spellId;
                Text = text;
                Trigger = trigger;
            }

            public string SpellId { get; }
            public string Text { get; }
            public string Trigger { get; }
        }

        private static readonly ExampleSpell[] ExampleSpells =
        {
            new ExampleSpell("Meteor", "Stars burning beyond the heavens, fall by my command. Shatter the earth beneath my enemy. Meteor!", "Meteor"),
            new ExampleSpell("Blink", "Space between worlds, bend before my will. Open the path and carry me beyond. Blink!", "Blink"),
            new ExampleSpell("ArcaneBarrier", "By my will, let no blade reach me and no spell pass through. Arcane Barrier!", "Arcane Barrier"),
            new ExampleSpell("DarkSphere", "Shadows beyond the mortal veil, gather at my command. Devour the light. Dark Sphere!", "Dark Sphere"),
            new ExampleSpell("HolyRay", "Radiant light of the heavens, banish the darkness before me. Holy Ray!", "Holy Ray"),
            new ExampleSpell("Heal", "Gentle light, gather in my hands. Mend what was broken and restore what was lost. Heal!", "Heal"),
            new ExampleSpell("StoneWall", "Ancient earth beneath my feet, rise and become my shield. Stone Wall!", "Stone Wall"),
            new ExampleSpell("WindBlade", "Winds of the high sky, gather around me. Tear through all that stands before you. Wind Blade!", "Wind Blade"),
            new ExampleSpell("LightningBolt", "Thunder above, answer my voice. Descend from the heavens and strike. Lightning Bolt!", "Lightning Bolt"),
            new ExampleSpell("IceLance", "Frozen winds, heed my call. Bind the earth in eternal frost. Ice Lance!", "Ice Lance"),
            new ExampleSpell("Fireball", "Flame of the ancient sun, hear my prayer. Gather within my hand, burn away the darkness, and strike my foe. Fireball!", "Fireball")
        };

        [SerializeField] private QuinAiIncantationTranscriber _transcriber;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Button _recordButton;
        [SerializeField] private TMP_Text _recordButtonText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _transcriptText;
        [SerializeField] private TMP_Text _normalizedText;
        [SerializeField] private TMP_Text _phonemesText;
        [SerializeField] private TMP_Text _recognitionText;
        [SerializeField] private TMP_Text _spellsText;
        [SerializeField] private int _maximumRecordingSeconds = 45;

        private EnglishPhonemizer _phonemizer;
        private IncantationRecognizer _recognizer;
        private AudioClip _recordingClip;
        private bool _isRecording;
        private bool _isRecognizing;
        private long _nextSequence;

        /// <summary>Assigns the scene's Quin.AI bridge before play mode.</summary>
        public void SetTranscriber(QuinAiIncantationTranscriber transcriber)
        {
            _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        }

        /// <summary>Builds the complete visible UI with Unity UI and TextMeshPro components.</summary>
        public void BuildUserInterface()
        {
            EnsureCanvas();
            RemoveExistingInterface();

            RectTransform panel = CreatePanel(_canvas.transform, "Recognition Panel", new Color(0.035f, 0.05f, 0.11f, 0.96f));
            _statusText = CreateText(panel, "Status", "Whisper: loading model...", 21f, new Color(0.48f, 0.82f, 1f), 44f);
            CreateText(panel, "Title", "INCANTIA · PHONETIC SPELL RECOGNITION", 32f, Color.white, 55f);
            _recordButton = CreateButton(panel, out _recordButtonText);
            _transcriptText = CreateText(panel, "Transcript", "WHISPER TRANSCRIPT\n—", 18f, Color.white, 90f);
            _normalizedText = CreateText(panel, "Normalized", "NORMALIZED TEXT\n—", 16f, new Color(0.75f, 0.84f, 0.96f), 65f);
            _phonemesText = CreateScrollableText(panel, "Phonemes", "OBSERVED PHONEMES\n—", 15f, 150f);
            _recognitionText = CreateText(panel, "Recognition", "RECOGNIZED SPELL\n—", 20f, new Color(1f, 0.84f, 0.4f), 125f);
            _spellsText = CreateScrollableText(panel, "Spells", CreateSpellListText(), 15f, 165f);
        }

        private void Awake()
        {
            if (ReferenceEquals(_canvas, null) || !_canvas)
            {
                BuildUserInterface();
            }
        }

        private void Start()
        {
            if (ReferenceEquals(_transcriber, null) || !_transcriber)
            {
                SetStatus("Setup error: assign QuinAiIncantationTranscriber.", new Color(1f, 0.35f, 0.35f));
                _recordButton.interactable = false;
                return;
            }

            BuildRecognizer();
            _recordButton.onClick.AddListener(OnRecordButtonClicked);
            _recordButton.interactable = false;
            SetStatus("Whisper: loading model...", new Color(0.48f, 0.82f, 1f));
        }

        private void Update()
        {
            if (!_isRecording && !_isRecognizing && _transcriber && _transcriber.IsReady && !_recordButton.interactable)
            {
                _recordButton.interactable = true;
                SetStatus("Whisper ready. Press RECORD, speak an incantation, then press STOP.", new Color(0.48f, 1f, 0.62f));
            }
        }

        private void OnDestroy()
        {
            if (_isRecording)
            {
                Microphone.End(null);
            }
        }

        private void OnRecordButtonClicked()
        {
            if (_isRecognizing)
            {
                return;
            }

            if (!_isRecording)
            {
                StartRecording();
                return;
            }

            StopRecordingAndRecognize();
        }

        private void StartRecording()
        {
            _recordingClip = Microphone.Start(null, false, _maximumRecordingSeconds, 16000);
            if (ReferenceEquals(_recordingClip, null))
            {
                SetStatus("Microphone could not start.", new Color(1f, 0.35f, 0.35f));
                return;
            }

            _isRecording = true;
            _recordButtonText.text = "STOP";
            SetStatus("Recording… speak one complete incantation.", new Color(1f, 0.82f, 0.35f));
        }

        private void StopRecordingAndRecognize()
        {
            int frameCount = Microphone.GetPosition(null);
            AudioClip clip = _recordingClip;
            Microphone.End(null);
            _isRecording = false;
            _recordButtonText.text = "RECORD";
            if (ReferenceEquals(clip, null) || frameCount <= 0)
            {
                SetStatus("No microphone samples were captured.", new Color(1f, 0.35f, 0.35f));
                return;
            }

            float[] samples = GetMonoSamples(clip, frameCount);
            if (ReferenceEquals(samples, null))
            {
                SetStatus("Microphone samples could not be read.", new Color(1f, 0.35f, 0.35f));
                return;
            }

            RecognizeSamplesAsync(samples);
        }

        private async void RecognizeSamplesAsync(float[] samples)
        {
            _isRecognizing = true;
            _recordButton.interactable = false;
            SetStatus("Whisper is transcribing…", new Color(0.48f, 0.82f, 1f));
            try
            {
                IncantationRecognitionRequest request = new IncantationRecognitionRequest(samples, 16000, "en", _nextSequence++);
                IncantationRecognitionResult result = await _recognizer.RecognizeAsync(request);
                DisplayResult(result);
            }
            catch (Exception exception)
            {
                SetStatus($"Recognition failed: {exception.Message}", new Color(1f, 0.35f, 0.35f));
            }
            finally
            {
                _isRecognizing = false;
                if (_transcriber && _transcriber.IsReady)
                {
                    _recordButton.interactable = true;
                }
            }
        }

        private void BuildRecognizer()
        {
            _phonemizer = new EnglishPhonemizer();
            PhonemeCostModel costModel = EnglishPhonemeProfile.CreateCostModel();
            WeightedPhonemeDistance distance = new WeightedPhonemeDistance(costModel);
            IncantationCompiler compiler = new IncantationCompiler(_phonemizer, distance);
            List<CompiledIncantation> compiledIncantations = new List<CompiledIncantation>(ExampleSpells.Length);
            for (int spellIndex = 0; spellIndex < ExampleSpells.Length; spellIndex++)
            {
                ExampleSpell spell = ExampleSpells[spellIndex];
                _phonemizer.RegisterFallbackPronunciation(spell.Text);
                _phonemizer.RegisterFallbackPronunciation(spell.Trigger);
                IncantationDefinition definition = new IncantationDefinition(spell.SpellId, "en", spell.Text, spell.Trigger);
                compiledIncantations.Add(compiler.Compile(definition));
            }

            IncantationMatcher matcher = new IncantationMatcher(compiledIncantations, distance, new IncantationMatcherConfig());
            _recognizer = new IncantationRecognizer(_transcriber, _phonemizer, costModel.Inventory, matcher);
        }

        private void DisplayResult(in IncantationRecognitionResult result)
        {
            _transcriptText.text = $"WHISPER TRANSCRIPT\n{DisplayOrPlaceholder(result.Transcript)}";
            _normalizedText.text = $"NORMALIZED TEXT\n{DisplayOrPlaceholder(result.NormalizedTranscript)}";
            _phonemesText.text = $"OBSERVED PHONEMES ({result.ObservedPhonemeCount})\n{FormatPhonemes(result.NormalizedTranscript)}";
            if (result.Accepted)
            {
                CandidateScore best = result.Match.Best;
                _recognitionText.text = $"RECOGNIZED SPELL\n{best.Incantation.SpellId}\nScore {best.Total:F3}  ·  Margin {result.Match.Margin:F3}\nFull {best.FullPhoneme:F3}  ·  Consonants {best.ConsonantSkeleton:F3}  ·  Trigger {best.Trigger:F3}";
                SetStatus("Spell accepted.", new Color(0.48f, 1f, 0.62f));
                return;
            }

            CandidateScore rejectedBest = result.Match.Best;
            string bestSpell = rejectedBest.HasCandidate ? rejectedBest.Incantation.SpellId : "—";
            _recognitionText.text = $"RECOGNIZED SPELL\nREJECTED · {result.RejectionReason}\nBest candidate: {bestSpell}\nScore {rejectedBest.Total:F3}  ·  Margin {result.Match.Margin:F3}";
            SetStatus("No spell cast. See rejection reason and candidate score below.", new Color(1f, 0.6f, 0.35f));
        }

        private float[] GetMonoSamples(AudioClip clip, int frameCount)
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

        private string FormatPhonemes(string normalizedText)
        {
            if (string.IsNullOrEmpty(normalizedText))
            {
                return "—";
            }

            PhonemeSequence phonemes = _phonemizer.Phonemize(normalizedText);
            StringBuilder builder = new StringBuilder(phonemes.Length * 4);
            ReadOnlySpan<PhonemeId> source = phonemes.AsSpan();
            for (int phonemeIndex = 0; phonemeIndex < source.Length; phonemeIndex++)
            {
                if (phonemeIndex > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(((EnglishPhoneme)source[phonemeIndex].Value).ToString());
            }

            return builder.ToString();
        }

        private string CreateSpellListText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("EXAMPLE SPELLS (11)\n");
            for (int spellIndex = 0; spellIndex < ExampleSpells.Length; spellIndex++)
            {
                builder.Append(spellIndex + 1);
                builder.Append(". ");
                builder.Append(ExampleSpells[spellIndex].SpellId);
                if (spellIndex < ExampleSpells.Length - 1)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        private void SetStatus(string text, Color color)
        {
            _statusText.text = text;
            _statusText.color = color;
        }

        private static string DisplayOrPlaceholder(string text)
        {
            return string.IsNullOrEmpty(text) ? "—" : text;
        }

        private void EnsureCanvas()
        {
            if (!ReferenceEquals(_canvas, null) && _canvas)
            {
                return;
            }

            GameObject canvasObject = new GameObject("Incantia Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            if (ReferenceEquals(EventSystem.current, null))
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                eventSystem.transform.SetParent(transform, false);
            }
        }

        private void RemoveExistingInterface()
        {
            Transform canvasTransform = _canvas.transform;
            for (int childIndex = canvasTransform.childCount - 1; childIndex >= 0; childIndex--)
            {
                GameObject child = canvasTransform.GetChild(childIndex).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panelObject.transform.SetParent(parent, false);
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.025f, 0.025f);
            panel.anchorMax = new Vector2(0.975f, 0.975f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panelObject.GetComponent<Image>().color = color;
            VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 25, 25);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return panel;
        }

        private static TMP_Text CreateText(Transform parent, string name, string content, float fontSize, Color color, float minimumHeight)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = color;
            text.text = content;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.TopLeft;
            LayoutElement layout = textObject.GetComponent<LayoutElement>();
            layout.minHeight = minimumHeight;
            layout.flexibleHeight = 0f;
            return text;
        }

        private static Button CreateButton(Transform parent, out TMP_Text buttonText)
        {
            GameObject buttonObject = new GameObject("Record Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.46f, 0.8f, 1f);
            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 60f;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            buttonText = CreateText(buttonObject.transform, "Label", "RECORD", 24f, Color.white, 50f);
            buttonText.alignment = TextAlignmentOptions.Center;
            RectTransform textTransform = buttonText.GetComponent<RectTransform>();
            textTransform.anchorMin = Vector2.zero;
            textTransform.anchorMax = Vector2.one;
            textTransform.offsetMin = Vector2.zero;
            textTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static TMP_Text CreateScrollableText(Transform parent, string name, string content, float fontSize, float minimumHeight)
        {
            GameObject scrollObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            scrollObject.transform.SetParent(parent, false);
            scrollObject.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.07f, 0.8f);
            LayoutElement layout = scrollObject.GetComponent<LayoutElement>();
            layout.minHeight = minimumHeight;
            layout.flexibleHeight = 0f;

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(8f, 8f);
            viewport.offsetMax = new Vector2(-8f, -8f);
            viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform contentTransform = contentObject.GetComponent<RectTransform>();
            contentTransform.anchorMin = new Vector2(0f, 1f);
            contentTransform.anchorMax = new Vector2(1f, 1f);
            contentTransform.pivot = new Vector2(0.5f, 1f);
            contentTransform.anchoredPosition = Vector2.zero;
            TextMeshProUGUI text = contentObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = new Color(0.82f, 0.88f, 0.98f);
            text.text = content;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.TopLeft;
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = contentTransform;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            return text;
        }
    }
}
