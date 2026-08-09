using System;
using System.Collections.Generic;
using System.Text;
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
    /// <summary>Playable Unity UI example for continuous Whisper spell recognition and opt-in quick spells.</summary>
    [DisallowMultipleComponent]
    public sealed class RealtimeIncantationRecognitionExampleController : EnglishRealtimeIncantationRecognitionBehaviour
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

        [SerializeField] private Canvas _canvas;
        [SerializeField] private Button _listenButton;
        [SerializeField] private TMP_Text _listenButtonText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _transcriptText;
        [SerializeField] private TMP_Text _normalizedText;
        [SerializeField] private TMP_Text _phonemesText;
        [SerializeField] private TMP_Text _recognitionText;
        [SerializeField] private TMP_Text _spellsText;

        /// <summary>Builds the complete real-time example UI with Unity UI and TextMeshPro components.</summary>
        public void BuildUserInterface()
        {
            EnsureCanvas();
            RemoveExistingInterface();

            RectTransform panel = CreatePanel(_canvas.transform, "Realtime Recognition Panel", new Color(0.035f, 0.05f, 0.11f, 0.96f));
            _statusText = CreateText(panel, "Status", "Whisper: loading model...", 21f, new Color(0.48f, 0.82f, 1f), 44f);
            CreateText(panel, "Title", "INCANTIA - REALTIME PHONETIC SPELLS", 32f, Color.white, 55f);
            _listenButton = CreateButton(panel, out _listenButtonText);
            _transcriptText = CreateText(panel, "Transcript", "LIVE WHISPER TRANSCRIPT\n-", 18f, Color.white, 90f);
            _normalizedText = CreateText(panel, "Normalized", "NORMALIZED TEXT\n-", 16f, new Color(0.75f, 0.84f, 0.96f), 65f);
            _phonemesText = CreateScrollableText(panel, "Phonemes", "LIVE PHONEMES\n-", 15f, 150f);
            _recognitionText = CreateText(panel, "Recognition", "LAST SPELL\n-", 20f, new Color(1f, 0.84f, 0.4f), 125f);
            _spellsText = CreateScrollableText(panel, "Spells", CreateSpellListText(), 15f, 165f);
        }

        protected override void Awake()
        {
            if (ReferenceEquals(_canvas, null) || !_canvas)
            {
                BuildUserInterface();
            }

            base.Awake();
        }

        private void Start()
        {
            _listenButton.onClick.AddListener(OnListenButtonClicked);
            _listenButton.interactable = false;
            SetStatus("Whisper: loading model...", new Color(0.48f, 0.82f, 1f));
        }

        protected override void AddIncantationDefinitions(List<IncantationDefinition> definitions)
        {
            for (int spellIndex = 0; spellIndex < ExampleSpells.Length; spellIndex++)
            {
                ExampleSpell spell = ExampleSpells[spellIndex];
                definitions.Add(new IncantationDefinition(spell.SpellId, "en", spell.Text, spell.Trigger));
            }
        }

        protected override IncantationMatcherConfig CreateMatcherConfig()
        {
            IncantationMatcherConfig config = base.CreateMatcherConfig();
            config.AllowTriggerOnlyRecognition = false;
            return config;
        }

        protected override void OnWhisperReady()
        {
            _listenButton.interactable = true;
            SetStatus("Whisper ready. Press LISTEN, then speak a full spell or its distinct trigger word.", new Color(0.48f, 1f, 0.62f));
        }

        protected override void OnListeningStarted()
        {
            _listenButtonText.text = "STOP";
            SetStatus("Listening - speak an incantation. Successful casts keep listening.", new Color(1f, 0.82f, 0.35f));
        }

        protected override void OnListeningStopped()
        {
            _listenButtonText.text = "LISTEN";
            SetStatus("Listening stopped.", new Color(0.75f, 0.84f, 0.96f));
        }

        protected override void OnListeningFailed(string message)
        {
            _listenButtonText.text = "LISTEN";
            _listenButton.interactable = IsReady;
            SetStatus(message, new Color(1f, 0.35f, 0.35f));
        }

        protected override void OnRecognitionStarted()
        {
            SetStatus("Whisper is analyzing the latest active window...", new Color(0.48f, 0.82f, 1f));
        }

        protected override void OnRecognitionUpdated(in IncantationRecognitionResult result)
        {
            DisplayResult(result);
        }

        protected override void OnSpellRecognized(in IncantationRecognitionResult result)
        {
            CandidateScore best = result.Match.Best;
            SetStatus($"Spell cast: {best.Incantation.SpellId} ({result.Match.MatchKind}).", new Color(0.48f, 1f, 0.62f));
            Debug.Log($"Spell cast: {best.Incantation.SpellId} ({result.Match.MatchKind}).");
        }

        protected override void OnRecognitionFailed(Exception exception)
        {
            SetStatus($"Recognition failed: {exception.Message}", new Color(1f, 0.35f, 0.35f));
        }

        private void OnListenButtonClicked()
        {
            if (IsListening)
            {
                StopListening();
                return;
            }

            BeginListening();
        }

        private void DisplayResult(in IncantationRecognitionResult result)
        {
            _transcriptText.text = $"LIVE WHISPER TRANSCRIPT\n{DisplayOrPlaceholder(result.Transcript)}";
            _normalizedText.text = $"NORMALIZED TEXT\n{DisplayOrPlaceholder(result.NormalizedTranscript)}";
            SetScrollableText(_phonemesText, $"LIVE PHONEMES ({result.ObservedPhonemeCount})\n{FormatPhonemes(result.NormalizedTranscript)}");
            if (result.Accepted)
            {
                CandidateScore best = result.Match.Best;
                _recognitionText.text = $"LAST SPELL\n{best.Incantation.SpellId} - {result.Match.MatchKind}\nScore {best.Total:F3} - Margin {result.Match.Margin:F3}\nFull {best.FullPhoneme:F3} - Consonants {best.ConsonantSkeleton:F3} - Trigger {best.Trigger:F3}";
                return;
            }

            CandidateScore rejectedBest = result.Match.Best;
            string bestSpell = rejectedBest.HasCandidate ? rejectedBest.Incantation.SpellId : "-";
            _recognitionText.text = $"LAST SPELL\nNo cast - {result.RejectionReason}\nBest candidate: {bestSpell}\nScore {rejectedBest.Total:F3} - Margin {result.Match.Margin:F3}";
        }

        private string FormatPhonemes(string normalizedText)
        {
            if (string.IsNullOrEmpty(normalizedText))
            {
                return "-";
            }

            PhonemeSequence phonemes = Phonemizer.Phonemize(normalizedText);
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

        private static string CreateSpellListText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("EXAMPLE SPELLS - FULL INCANTATION OR QUICK TRIGGER\n");
            for (int spellIndex = 0; spellIndex < ExampleSpells.Length; spellIndex++)
            {
                ExampleSpell spell = ExampleSpells[spellIndex];
                builder.Append(spellIndex + 1);
                builder.Append(". ");
                builder.Append(spell.SpellId);
                builder.Append(" - ");
                builder.Append(spell.Trigger);
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
            return string.IsNullOrEmpty(text) ? "-" : text;
        }

        private static void SetScrollableText(TMP_Text text, string content)
        {
            text.text = content;
            text.rectTransform.sizeDelta = new Vector2(0f, text.preferredHeight);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(text.rectTransform);
            ScrollRect scrollRect = text.GetComponentInParent<ScrollRect>();
            if (scrollRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void EnsureCanvas()
        {
            if (!ReferenceEquals(_canvas, null) && _canvas)
            {
                return;
            }

            GameObject canvasObject = new GameObject("Incantia Realtime Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            GameObject buttonObject = new GameObject("Listen Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.46f, 0.8f, 1f);
            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 60f;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            buttonText = CreateText(buttonObject.transform, "Label", "LISTEN", 24f, Color.white, 50f);
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
            contentTransform.offsetMin = Vector2.zero;
            contentTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI text = contentObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = new Color(0.82f, 0.88f, 0.98f);
            text.text = content;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
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
