using NUnit.Framework;
using H1M4W4R1.Incantia.Phonetics;

namespace H1M4W4R1.Incantia.Tests
{
    public sealed class WeightedPhonemeDistanceTests
    {
        [Test]
        public void CalculateSimilarity_IdenticalSequences_ReturnsOne()
        {
            WeightedPhonemeDistance distance = CreateDistance();
            PhonemeSequence sequence = new PhonemeSequence(new[] { new PhonemeId(1), new PhonemeId(3), new PhonemeId(2) });

            float similarity = distance.CalculateSimilarity(sequence.AsSpan(), sequence.AsSpan(), new PhonemeDistanceWorkspace());

            Assert.That(similarity, Is.EqualTo(1f));
        }

        [Test]
        public void CalculateSimilarity_VowelMismatch_IsCheaperThanUnrelatedConsonantMismatch()
        {
            WeightedPhonemeDistance distance = CreateDistance();
            PhonemeSequence vowelReference = new PhonemeSequence(new[] { new PhonemeId(1) });
            PhonemeSequence vowelObserved = new PhonemeSequence(new[] { new PhonemeId(2) });
            PhonemeSequence consonantReference = new PhonemeSequence(new[] { new PhonemeId(3) });
            PhonemeSequence consonantObserved = new PhonemeSequence(new[] { new PhonemeId(4) });
            PhonemeDistanceWorkspace workspace = new PhonemeDistanceWorkspace();

            float vowelSimilarity = distance.CalculateSimilarity(vowelReference.AsSpan(), vowelObserved.AsSpan(), workspace);
            float consonantSimilarity = distance.CalculateSimilarity(consonantReference.AsSpan(), consonantObserved.AsSpan(), workspace);

            Assert.That(vowelSimilarity, Is.GreaterThan(consonantSimilarity));
        }

        [Test]
        public void CalculateSimilarity_InsertedVowel_PreservesMoreSimilarityThanInsertedConsonant()
        {
            WeightedPhonemeDistance distance = CreateDistance();
            PhonemeSequence reference = new PhonemeSequence(new[] { new PhonemeId(3) });
            PhonemeSequence vowelInsertion = new PhonemeSequence(new[] { new PhonemeId(3), new PhonemeId(1) });
            PhonemeSequence consonantInsertion = new PhonemeSequence(new[] { new PhonemeId(3), new PhonemeId(4) });
            PhonemeDistanceWorkspace workspace = new PhonemeDistanceWorkspace();

            float vowelSimilarity = distance.CalculateSimilarity(reference.AsSpan(), vowelInsertion.AsSpan(), workspace);
            float consonantSimilarity = distance.CalculateSimilarity(reference.AsSpan(), consonantInsertion.AsSpan(), workspace);

            Assert.That(vowelSimilarity, Is.GreaterThan(consonantSimilarity));
        }

        private static WeightedPhonemeDistance CreateDistance()
        {
            PhonemeInventory inventory = new PhonemeInventory();
            inventory.Register(new PhonemeId(1), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 0, vowelBackness: 0));
            inventory.Register(new PhonemeId(2), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 128, vowelBackness: 0));
            inventory.Register(new PhonemeId(3), new PhonemeFeatures(PhonemeClass.Consonant, place: 0, manner: 0));
            inventory.Register(new PhonemeId(4), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 255, manner: 255));
            return new WeightedPhonemeDistance(new PhonemeCostModel(inventory));
        }
    }
}
