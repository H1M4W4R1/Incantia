using System;
using NUnit.Framework;
using H1M4W4R1.Incantia.Phonetics;
using H1M4W4R1.Incantia.Phonetics.English;

namespace H1M4W4R1.Incantia.Tests
{
    public sealed class EnglishPhonemizerTests
    {
        [Test]
        public void PhonemizeReference_KnownIncantation_ProducesContinuousExpectedStream()
        {
            EnglishPhonemizer phonemizer = new EnglishPhonemizer();

            PhonemeSequence result = phonemizer.PhonemizeReference("Flame of the ancient sun.");

            AssertSequence(
                result,
                EnglishPhoneme.F,
                EnglishPhoneme.L,
                EnglishPhoneme.EY,
                EnglishPhoneme.M,
                EnglishPhoneme.AH,
                EnglishPhoneme.V,
                EnglishPhoneme.DH,
                EnglishPhoneme.AH,
                EnglishPhoneme.EY,
                EnglishPhoneme.N,
                EnglishPhoneme.SH,
                EnglishPhoneme.AH,
                EnglishPhoneme.N,
                EnglishPhoneme.T,
                EnglishPhoneme.S,
                EnglishPhoneme.AH,
                EnglishPhoneme.N);
        }

        [Test]
        public void Phonemize_PhoneticAsrSubstitution_PreservesHighSimilarity()
        {
            EnglishPhonemizer phonemizer = new EnglishPhonemizer();
            WeightedPhonemeDistance distance = new WeightedPhonemeDistance(EnglishPhonemeProfile.CreateCostModel());

            PhonemeSequence reference = phonemizer.PhonemizeReference("hear my prayer");
            PhonemeSequence observed = phonemizer.Phonemize("here my pear");
            float similarity = distance.CalculateSimilarity(reference.AsSpan(), observed.AsSpan(), new PhonemeDistanceWorkspace());

            Assert.That(similarity, Is.GreaterThan(0.80f));
        }

        [Test]
        public void PhonemizeReference_UnknownWord_RejectsReferenceUntilOverrideRegistered()
        {
            EnglishPhonemizer phonemizer = new EnglishPhonemizer();

            Assert.That(() => phonemizer.PhonemizeReference("zorvax"), Throws.TypeOf<InvalidOperationException>());
            Assert.That(phonemizer.Phonemize("zorvax").IsEmpty, Is.False);

            phonemizer.RegisterPronunciation("zorvax", EnglishPhoneme.Z, EnglishPhoneme.AO, EnglishPhoneme.R, EnglishPhoneme.V, EnglishPhoneme.AE, EnglishPhoneme.K, EnglishPhoneme.S);

            PhonemeSequence result = phonemizer.PhonemizeReference("zorvax");
            AssertSequence(result, EnglishPhoneme.Z, EnglishPhoneme.AO, EnglishPhoneme.R, EnglishPhoneme.V, EnglishPhoneme.AE, EnglishPhoneme.K, EnglishPhoneme.S);
        }

        [Test]
        public void RegisterFallbackPronunciation_ExplicitlyEnablesStrictReferenceCompilation()
        {
            EnglishPhonemizer phonemizer = new EnglishPhonemizer();
            PhonemeSequence observedFallback = phonemizer.Phonemize("eldoria");

            phonemizer.RegisterFallbackPronunciation("eldoria");

            PhonemeSequence registeredReference = phonemizer.PhonemizeReference("eldoria");
            Assert.That(registeredReference.AsSpan().ToArray(), Is.EqualTo(observedFallback.AsSpan().ToArray()));
        }

        [Test]
        public void CreateCostModel_EnglishConfusionOverrides_AreCheaperThanUnrelatedSubstitutions()
        {
            PhonemeCostModel costModel = EnglishPhonemeProfile.CreateCostModel();

            float expectedConfusion = costModel.GetSubstitutionCost(
                EnglishPhonemeProfile.ToId(EnglishPhoneme.TH),
                EnglishPhonemeProfile.ToId(EnglishPhoneme.T));
            float unrelatedConsonants = costModel.GetSubstitutionCost(
                EnglishPhonemeProfile.ToId(EnglishPhoneme.TH),
                EnglishPhonemeProfile.ToId(EnglishPhoneme.G));

            Assert.That(expectedConfusion, Is.LessThan(unrelatedConsonants));
        }

        private static void AssertSequence(PhonemeSequence sequence, params EnglishPhoneme[] expected)
        {
            Assert.That(sequence.Length, Is.EqualTo(expected.Length));
            ReadOnlySpan<PhonemeId> actual = sequence.AsSpan();
            for (int phonemeIndex = 0; phonemeIndex < expected.Length; phonemeIndex++)
            {
                Assert.That(actual[phonemeIndex], Is.EqualTo(EnglishPhonemeProfile.ToId(expected[phonemeIndex])));
            }
        }
    }
}
