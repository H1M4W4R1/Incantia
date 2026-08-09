# Incantia

Incantia is a closed-set, phonetic spell recognizer for Unity. It turns a speech transcript into phonemes, compares the end of that transcript with a known list of incantations, and accepts a spell only when its score and lead over competing spells are strong enough.

Incantia provides:

- Offline English phonemization and configurable phoneme-distance matching.
- Full-incantation and optional trigger-only recognition.
- Transcript cleanup for annotations such as `[noise]` and `(background noise)`.
- A reusable real-time microphone integration for Quin.AI Whisper.
- A generated example scene with live recognition diagnostics.

## Quick start

1. Download the latest release of [Unity Realtime Voice Transcription](https://github.com/InboraStudio/Unity-Realtime-voice-transcription/releases) and import it into the Unity project. Skip this step if the project already contains `Assets/Quin.AI`.
2. Import the `Assets/H1M4W4R1/Incantia` folder.
3. Install the `whisper-tiny.en` model through the transcription package. In this repository, the model is stored at `Assets/StreamingAssets/Undertone/whisper-tiny.en.bytes`.
4. Save the current scene, then select **Incantia > Create Realtime Recognition Example Scene**. The command creates `Assets/H1M4W4R1/Incantia/Examples/Scenes/RealtimeIncantationRecognitionExample.unity`.
5. Enter Play mode, wait for **Whisper ready**, press **LISTEN**, and speak one of the incantations shown in the scene.

The example keeps listening after a successful cast. Press **STOP** to end microphone capture.

## Dependencies

| Dependency | Required for | Notes |
| --- | --- | --- |
| [Unity Realtime Voice Transcription](https://github.com/InboraStudio/Unity-Realtime-voice-transcription) | Microphone-to-text recognition and the supplied Quin.AI integration | Import its Quin.AI/Undertone runtime and install an English Whisper model. |
| Unity microphone access | Real-time and recorded speech input | The player must have permission to use a microphone. |
| Unity UI (`com.unity.ugui`) | Generated example scene | Not required by the core matcher. |
| TextMesh Pro | Generated example scene | Not required by the core matcher. |

This repository is developed with Unity `6000.5.2f1`. The core assembly, `H1M4W4R1.Incantia.Runtime`, has no Unity Engine dependency. You can use the matcher with another transcription provider by passing its text into `IncantationRecognizer`.

## Add Incantia to a game

Create a component derived from `EnglishRealtimeIncantationRecognitionBehaviour`. Define the supported spells and handle accepted results through the protected callbacks:

```csharp
using System.Collections.Generic;
using H1M4W4R1.Incantia.Database;
using H1M4W4R1.Incantia.Integration.QuinAI;
using H1M4W4R1.Incantia.Recognition;
using UnityEngine;

namespace MyGame.Spells
{
    public sealed class PlayerSpellRecognizer : EnglishRealtimeIncantationRecognitionBehaviour
    {
        protected override void AddIncantationDefinitions(List<IncantationDefinition> definitions)
        {
            definitions.Add(new IncantationDefinition(
                "Fireball",
                "en",
                "Flame of the ancient sun, gather in my hand. Fireball!",
                "Fireball"));
        }

        protected override void OnSpellRecognized(in IncantationRecognitionResult result)
        {
            string spellId = result.Match.Best.Incantation.SpellId;
            Debug.Log($"Cast {spellId}");
        }
    }
}
```

Then configure the scene:

1. Add a Quin.AI `SpeechEngine` and select `whisper-tiny.en`, language `en`, with **Translate To English** disabled.
2. Add `QuinAiIncantationTranscriber`, then assign the `SpeechEngine` to it.
3. Add the derived recognition component, then assign the transcriber to it.
4. Call `BeginListening()` and `StopListening()` from the game's UI or input code.

Use `OnWhisperReady()`, `OnListeningStarted()`, `OnListeningStopped()`, `OnRecognitionUpdated(...)`, and `OnRecognitionFailed(...)` for status and diagnostic UI. Rejected or ambiguous transcripts never invoke `OnSpellRecognized(...)`.

The complete working implementation is [RealtimeIncantationRecognitionExampleController.cs](Examples/Runtime/RealtimeIncantationRecognitionExampleController.cs).

## Incantation definitions and pronunciations

Each `IncantationDefinition` contains:

| Value | Purpose |
| --- | --- |
| `SpellId` | Stable identifier returned to gameplay code. |
| `Language` | Language identifier; the supplied profile uses `en`. |
| `Text` | Full spoken incantation. |
| `TriggerText` | Optional distinctive ending used for trigger scoring and quick-spell recognition. |

`EnglishPhonemizer` includes a small built-in pronunciation dictionary and a deterministic spelling-to-sound fallback. Override `ConfigurePhonemizer(...)` and call `RegisterPronunciation(...)` for fantasy names or other words whose generated pronunciation is not correct.

## Recognition modes

### Full incantations

Full-incantation recognition is enabled by default. Unrelated speech before a complete incantation does not lower the match score because Incantia aligns candidates against the terminal portion of the transcript.

### Quick trigger words

Trigger-only recognition is opt-in. Enable it only for short, distinctive trigger phrases:

```csharp
protected override IncantationMatcherConfig CreateMatcherConfig()
{
    IncantationMatcherConfig config = base.CreateMatcherConfig();
    config.AllowTriggerOnlyRecognition = true;
    config.MinimumTriggerOnlyScore = 0.92f;
    config.MinimumTriggerOnlyMargin = 0.12f;
    return config;
}
```

The default real-time configuration suppresses a trigger-only cast while the transcript is still a valid unfinished prefix of a longer incantation. Tune trigger thresholds with recordings from the microphones and environments used by the game.

## Real-time behavior

`EnglishRealtimeIncantationRecognitionBehaviour` captures non-overlapping 16 kHz microphone blocks, applies voice-activity gating, and retranscribes a bounded audio cache as new context arrives.

Important inspector settings:

| Setting | Default | Effect |
| --- | ---: | --- |
| Capture Step Size In Seconds | `0.25` | How often new microphone samples are collected. |
| Minimum New Audio Duration For Recognition | `0.75` | Minimum new cached audio before another Whisper request. |
| Maximum Cached Audio Duration In Seconds | `30` | Context window; hard-limited to 120 seconds. |
| Final Whisper Beam Count | `1` | Beam count for the optional retry after a rejected end-of-phrase pass. |

Growing-cache recognition always uses one Whisper beam for lower latency. At the end of a phrase, a rejected one-beam result can be retried with **Final Whisper Beam Count**. After an accepted match, Incantia removes the audio through that phrase and preserves later samples for the next cast.

## Core recognition flow

For a custom transcription provider:

1. Normalize its transcript with `IncantationTextNormalizer.Normalize(...)`.
2. Convert the normalized text with a language-matched `IPhonemizer`.
3. Build a `PhoneticObservation` and call `IncantationMatcher.Match(...)`, or use `IncantationRecognizer.Recognize(...)` to run the complete text pipeline.

`IncantationCompiler` precomputes reference phonemes. Create one `IncantationMatcher` per worker thread because each matcher reuses its own rolling alignment workspace.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| **LISTEN** stays disabled | Confirm the selected Whisper model exists and wait for `QuinAiIncantationTranscriber.IsReady`. |
| No microphone audio | Confirm OS microphone permission and that Unity can see the intended input device. |
| Fantasy words match poorly | Register a reviewed pronunciation in `ConfigurePhonemizer(...)`. |
| Short words cast the wrong spell | Disable trigger-only recognition or increase its score and margin thresholds. |
