# Incantia

Incantia provides Unity-independent, closed-set phonetic matching for spell incantations. It accepts a phoneme stream produced by a language-specific `IPhonemizer`, compares it against precompiled spell references, and only accepts a winner when score, score margin, trigger, and length safeguards pass.

## Runtime flow

1. Normalize the Whisper transcript with `IncantationTextNormalizer.Normalize`.
2. Convert it with the language-matched `IPhonemizer`.
3. Build `PhoneticObservation` and call `IncantationMatcher.Match`.

`IncantationCompiler` precomputes reference phonemes. Create a separate `IncantationMatcher` for each worker thread because its rolling alignment workspace is reused.

The package deliberately does not provide a grapheme-to-phoneme or Whisper implementation. Those backends remain replaceable so each supported language can supply its own pronunciation data and transcription integration.
