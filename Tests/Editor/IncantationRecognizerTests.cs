using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using H1M4W4R1.Incantia.Database;
using H1M4W4R1.Incantia.Matching;
using H1M4W4R1.Incantia.Phonetics;
using H1M4W4R1.Incantia.Phonetics.English;
using H1M4W4R1.Incantia.Recognition;

namespace H1M4W4R1.Incantia.Tests
{
    public sealed class IncantationRecognizerTests
    {
        private sealed class FixedTranscriber : IIncantationSpeechTranscriber
        {
            private readonly IncantationTranscription _transcription;

            public FixedTranscriber(IncantationTranscription transcription)
            {
                _transcription = transcription;
            }

            public Task<IncantationTranscription> TranscribeAsync(IncantationRecognitionRequest request)
            {
                return Task.FromResult(_transcription);
            }
        }

        [Test]
        public void RecognizeAsync_MatchingTranscript_AcceptsCompiledSpellAndPreservesDiagnostics()
        {
            EnglishPhonemizer phonemizer = new EnglishPhonemizer();
            PhonemeCostModel costModel = EnglishPhonemeProfile.CreateCostModel();
            WeightedPhonemeDistance distance = new WeightedPhonemeDistance(costModel);
            IncantationCompiler compiler = new IncantationCompiler(phonemizer, distance);
            CompiledIncantation incantation = compiler.Compile(new IncantationDefinition(
                "Fireball",
                "en",
                "flame of the ancient sun fireball",
                "fireball"));
            List<CompiledIncantation> incantations = new List<CompiledIncantation> { incantation };
            IncantationMatcher matcher = new IncantationMatcher(incantations, distance, CreateConfig());
            IncantationRecognizer recognizer = new IncantationRecognizer(
                new FixedTranscriber(new IncantationTranscription("Flame of the ancient sun. Fireball!", true)),
                phonemizer,
                costModel.Inventory,
                matcher);
            IncantationRecognitionRequest request = new IncantationRecognitionRequest(new float[16000], 16000, "en", 42);

            IncantationRecognitionResult result = recognizer.RecognizeAsync(request).GetAwaiter().GetResult();

            Assert.That(result.Sequence, Is.EqualTo(42));
            Assert.That(result.NormalizedTranscript, Is.EqualTo("flame of the ancient sun fireball"));
            Assert.That(result.Match.Best.Incantation.SpellId, Is.EqualTo("Fireball"));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.RejectionReason, Is.EqualTo(RecognitionRejectionReason.None));
        }

        [Test]
        public void RecognizeAsync_NoSpeech_ReturnsRejectionWithoutPhonemizing()
        {
            EnglishPhonemizer phonemizer = new EnglishPhonemizer();
            PhonemeCostModel costModel = EnglishPhonemeProfile.CreateCostModel();
            WeightedPhonemeDistance distance = new WeightedPhonemeDistance(costModel);
            IncantationMatcher matcher = new IncantationMatcher(new List<CompiledIncantation>(), distance, CreateConfig());
            IncantationRecognizer recognizer = new IncantationRecognizer(
                new FixedTranscriber(IncantationTranscription.NoSpeech),
                phonemizer,
                costModel.Inventory,
                matcher);
            IncantationRecognitionRequest request = new IncantationRecognitionRequest(new float[16000], 16000, "en", 8);

            IncantationRecognitionResult result = recognizer.RecognizeAsync(request).GetAwaiter().GetResult();

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ObservedPhonemeCount, Is.EqualTo(0));
            Assert.That(result.RejectionReason, Is.EqualTo(RecognitionRejectionReason.NoSpeech));
        }

        private static IncantationMatcherConfig CreateConfig()
        {
            return new IncantationMatcherConfig
            {
                MinimumScore = 0.70f,
                MinimumMargin = 0.08f,
                MinimumTriggerScore = 0.60f,
                MinimumObservedLengthRatio = 0.50f,
                MinimumObservedPhonemeCount = 3
            };
        }
    }
}
