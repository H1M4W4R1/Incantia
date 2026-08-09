# Incantia

Incantia provides Unity-independent, closed-set phonetic matching for spell incantations. It accepts a phoneme stream produced by a language-specific `IPhonemizer`, compares each spell against the terminal portion of the transcript, and only accepts a winner when score, score margin, trigger, and length safeguards pass. Unrelated speech before a complete, terminal incantation does not lower its full or consonant score.

For mastered quick spells, set `IncantationMatcherConfig.AllowTriggerOnlyRecognition` to `true`. This is a secondary path used only when full-incantation acceptance fails; it requires a complete terminal trigger, `MinimumTriggerOnlyScore` (default `0.92`), and `MinimumTriggerOnlyMargin` (default `0.12`). Give every quick spell a distinctive trigger. Set `SuppressTriggerOnlyRecognitionDuringPartialIncantation` to prevent a quick trigger from firing while the terminal transcript is a valid unfinished prefix of any configured incantation; the partial check uses the same score, margin, and length safeguards as full recognition.

## Runtime flow

1. Normalize the Whisper transcript with `IncantationTextNormalizer.Normalize`. ASR annotations in square brackets or regular parentheses, such as `[noise]`, `[BLANK_AUDIO]`, and `(background noise)`, are removed with their contents before phonemization.
2. Convert it with the language-matched `IPhonemizer`.
3. Build `PhoneticObservation` and call `IncantationMatcher.Match`.

`IncantationCompiler` precomputes reference phonemes. Create a separate `IncantationMatcher` for each worker thread because its rolling alignment workspace is reused.

`EnglishPhonemizer` provides the first offline profile: a built-in spell/ASR pronunciation dictionary plus deterministic spelling-to-sound fallback for unknown Whisper words. Reference compilation is intentionally strict: register every word or phrase outside the built-in lexicon—especially fantasy terms—with `RegisterPronunciation(...)` before compiling it. Create the matching inventory and default English confusion costs with `EnglishPhonemeProfile.CreateCostModel()`.

The package does not provide a Whisper implementation. Speech transcription remains replaceable and must provide the selected language's transcript to the phonemizer.

## Quin.AI Whisper integration

`QuinAiIncantationTranscriber` adapts the existing Quin.AI `SpeechEngine` to a text transcription provider. Add it to a GameObject, assign an already configured `SpeechEngine`, then pass its transcript to `IncantationRecognizer.Recognize(...)` with the matching `IPhonemizer`, its `PhonemeInventory`, and `IncantationMatcher`.

The adapter queues audio calls from any thread and invokes Quin.AI's request queue on Unity's main thread. Quin.AI performs native Whisper inference in its own serialized worker task, then returns a transcript. Incantia does not receive audio samples or depend on Whisper; it synchronously normalizes, phonemizes, matches, and accepts that text. Set the engine to the requested language, leave `TranslateToEnglish` disabled, and supply mono 16 kHz PCM samples only to the transcriber. Wait for `QuinAiIncantationTranscriber.IsReady` before submitting a request.

## Playable example scene

Create `Assets/H1M4W4R1/Incantia/Examples/Scenes/IncantationRecognitionExample.unity` from **Incantia → Create Recognition Example Scene**. It uses only Unity UI/TextMeshPro—not IMGUI—and displays the Whisper transcript, normalized text, observed phonemes, best spell, component scores, margin, and rejection reason.

The scene includes Meteor, Blink, Arcane Barrier, Dark Sphere, Holy Ray, Heal, Stone Wall, Wind Blade, Lightning Bolt, Ice Lance, and Fireball. Press **RECORD**, speak one full incantation, then press **STOP**. The supplied `whisper-tiny.en` model loads locally; it may take several seconds before the record button becomes available.

## Reusable game behavior

Derive a component from `EnglishIncantationRecognitionBehaviour`, assign a `QuinAiIncantationTranscriber`, and override `AddIncantationDefinitions(...)`. Use `ConfigurePhonemizer(...)` for reviewed fantasy-word pronunciations. Connect UI and gameplay by overriding `OnWhisperReady`, `OnRecordingStarted`, `OnRecognitionStarted`, `OnRecognitionCompleted`, and failure callbacks; no public C# events or UnityEvents are required.

Call `BeginRecording()` and `EndRecordingAndRecognize()` from your Unity UI. The base behavior handles 16 kHz microphone capture, stereo-to-mono conversion, reference compilation, Whisper submission, and matching. [IncantationRecognitionExampleController.cs](Examples/Runtime/IncantationRecognitionExampleController.cs) is the working reference implementation.

## Realtime spells

Derive from `EnglishRealtimeIncantationRecognitionBehaviour` for continuous spells. Call `BeginListening()` and `StopListening()` from Unity UI. It captures non-overlapping 16 kHz microphone blocks and uses voice-activity gating. **Capture Step Size In Seconds** defaults to `0.25` seconds so phrase endings are observed quickly; it no longer grows with Whisper inference time. Active samples are retained and the complete cache is retranscribed as new context arrives, including samples captured while Whisper is busy. **Minimum New Audio Duration For Recognition** defaults to `0.75` seconds and prevents redundant submissions when inference is faster than capture. Set **Maximum Cached Audio Duration In Seconds** for the desired context window (default `30` seconds); the implementation clamps it to a hard `120`-second ceiling and discards the oldest samples on overflow. After a cast, the matcher's phoneme endpoint is mapped proportionally to the submitted samples because the Quin.AI backend does not provide word timestamps. Incantia consumes the preceding audio through that accepted phrase while preserving later and newly captured samples for the next cast. The default real-time matcher also suppresses quick triggers while a valid partial incantation is in progress. Override `OnRecognitionUpdated(...)` for live transcript/phoneme UI and `OnSpellRecognized(...)` for gameplay; rejected or ambiguous snapshots never call `OnSpellRecognized(...)`.

Override `CreateMatcherConfig()` and enable `AllowTriggerOnlyRecognition` only for deliberately distinct quick-spell trigger words. The default high quick-spell threshold is `0.92`; tune it with real transcript samples before release.

The matcher uses direct-indexed phoneme features, reuses compiled deletion costs, grows its rolling workspace geometrically, and calculates partial-prefix suppression only after a quick trigger otherwise qualifies. Growing-cache passes always use one Whisper beam for minimum latency. At the end of a phrase, Incantia first performs the same fast one-beam pass; only a rejected result is retried with the configured **Final Whisper Beam Count**. This keeps successful detection responsive while preserving an opt-in multi-beam accuracy fallback.

Create `Assets/H1M4W4R1/Incantia/Examples/Scenes/RealtimeIncantationRecognitionExample.unity` from **Incantia - Create Realtime Recognition Example Scene**. The scene uses Unity UI/TextMeshPro to show the live Whisper text, normalized transcript, live phonemes, accepted spell and match kind, plus all eleven example spell triggers. It enables quick-spell recognition for demonstration purposes.
