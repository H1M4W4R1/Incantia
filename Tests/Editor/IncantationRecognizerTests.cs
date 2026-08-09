using System.Collections.Generic;
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
        [Test]
        public void Recognize_MatchingTranscript_AcceptsCompiledSpellAndPreservesDiagnostics()
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
                phonemizer,
                costModel.Inventory,
                matcher);
            IncantationRecognitionRequest request = new IncantationRecognitionRequest("Flame of the ancient sun. Fireball!", "en", 42);

            IncantationRecognitionResult result = recognizer.Recognize(request);

            Assert.That(result.Sequence, Is.EqualTo(42));
            Assert.That(result.NormalizedTranscript, Is.EqualTo("flame of the ancient sun fireball"));
            Assert.That(result.Match.Best.Incantation.SpellId, Is.EqualTo("Fireball"));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.RejectionReason, Is.EqualTo(RecognitionRejectionReason.None));
        }

        [Test]
        public void Recognize_EmptyTranscript_ReturnsRejectionWithoutPhonemizing()
        {
            EnglishPhonemizer phonemizer = new EnglishPhonemizer();
            PhonemeCostModel costModel = EnglishPhonemeProfile.CreateCostModel();
            WeightedPhonemeDistance distance = new WeightedPhonemeDistance(costModel);
            IncantationMatcher matcher = new IncantationMatcher(new List<CompiledIncantation>(), distance, CreateConfig());
            IncantationRecognizer recognizer = new IncantationRecognizer(
                phonemizer,
                costModel.Inventory,
                matcher);
            IncantationRecognitionRequest request = new IncantationRecognitionRequest(string.Empty, "en", 8);

            IncantationRecognitionResult result = recognizer.Recognize(request);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ObservedPhonemeCount, Is.EqualTo(0));
            Assert.That(result.RejectionReason, Is.EqualTo(RecognitionRejectionReason.NoSpeech));
        }

        [Test]
        public void ConsumeAcceptedTranscript_PreservesSpeechAfterFirstIncantation()
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
            IncantationMatcherConfig config = CreateConfig();
            config.AllowTrailingSpeech = true;
            IncantationMatcher matcher = new IncantationMatcher(incantations, distance, config);
            IncantationRecognizer recognizer = new IncantationRecognizer(phonemizer, costModel.Inventory, matcher);
            string transcript = "Flame of the ancient sun. Fireball! Flame of the ancient sun";
            IncantationRecognitionRequest request = new IncantationRecognitionRequest(transcript, "en", 84);

            IncantationRecognitionResult acceptedResult = recognizer.Recognize(request);
            string remainingTranscript = IncantationTranscriptConsumer.ConsumeAcceptedTranscript(
                transcript,
                phonemizer,
                acceptedResult);

            Assert.That(acceptedResult.Accepted, Is.True);
            Assert.That(remainingTranscript, Is.EqualTo("Flame of the ancient sun"));
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
