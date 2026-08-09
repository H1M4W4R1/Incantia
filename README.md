# Incantia

Incantia provides Unity-independent, closed-set phonetic matching for spell incantations. It accepts a phoneme stream produced by a language-specific `IPhonemizer`, compares it against precompiled spell references, and only accepts a winner when score, score margin, trigger, and length safeguards pass.

## Runtime flow

1. Normalize the Whisper transcript with `IncantationTextNormalizer.Normalize`.
2. Convert it with the language-matched `IPhonemizer`.
3. Build `PhoneticObservation` and call `IncantationMatcher.Match`.

`IncantationCompiler` precomputes reference phonemes. Create a separate `IncantationMatcher` for each worker thread because its rolling alignment workspace is reused.

`EnglishPhonemizer` provides the first offline profile: a built-in spell/ASR pronunciation dictionary plus deterministic spelling-to-sound fallback for unknown Whisper words. Reference compilation is intentionally strict: register every word or phrase outside the built-in lexicon—especially fantasy terms—with `RegisterPronunciation(...)` before compiling it. Create the matching inventory and default English confusion costs with `EnglishPhonemeProfile.CreateCostModel()`.

The package does not provide a Whisper implementation. Speech transcription remains replaceable and must provide the selected language's transcript to the phonemizer.

## Quin.AI Whisper integration

`QuinAiIncantationTranscriber` adapts the existing Quin.AI `SpeechEngine` to `IIncantationSpeechTranscriber`. Add it to a GameObject, assign an already configured `SpeechEngine`, then construct `IncantationRecognizer` with that bridge, the matching `IPhonemizer`, its `PhonemeInventory`, and `IncantationMatcher`.

The adapter queues calls from any thread and invokes Quin.AI's request queue on Unity's main thread. Quin.AI performs native Whisper inference in its own serialized worker task; Incantia uses `ConfigureAwait(false)` so normalization, phonemization, and matching resume outside the Unity synchronization context. Set the engine to the requested language, leave `TranslateToEnglish` disabled, and supply mono 16 kHz PCM samples. Wait for `QuinAiIncantationTranscriber.IsReady` before submitting a request.
