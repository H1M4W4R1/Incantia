using System.Collections.Generic;
using NUnit.Framework;
using H1M4W4R1.Incantia.Database;
using H1M4W4R1.Incantia.Matching;
using H1M4W4R1.Incantia.Phonetics;

namespace H1M4W4R1.Incantia.Tests
{
    public sealed class IncantationMatcherTests
    {
        [Test]
        public void Match_PhoneticObservation_RanksExpectedSpellAndScoresTerminalTrigger()
        {
            WeightedPhonemeDistance distance = CreateDistance();
            CompiledIncantation fireball = CreateIncantation(distance, "Fireball", new ushort[] { 3, 1, 2, 4 }, new ushort[] { 2, 4 });
            CompiledIncantation iceLance = CreateIncantation(distance, "IceLance", new ushort[] { 4, 1, 3, 2 }, new ushort[] { 3, 2 });
            List<CompiledIncantation> incantations = new List<CompiledIncantation> { fireball, iceLance };
            IncantationMatcher matcher = new IncantationMatcher(incantations, distance, CreateConfig());
            PhonemeSequence observed = CreateSequence(new ushort[] { 3, 1, 2, 4 });
            PhoneticObservation observation = PhoneticObservation.Create(observed, distance.CostModel.Inventory);

            IncantationMatchResult result = matcher.Match("en", observation);

            Assert.That(result.Best.Incantation.SpellId, Is.EqualTo("Fireball"));
            Assert.That(result.Best.Trigger, Is.EqualTo(1f));
            Assert.That(result.Best.Total, Is.GreaterThan(result.Second.Total));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.MatchKind, Is.EqualTo(IncantationMatchKind.FullIncantation));
        }

        [Test]
        public void Match_IdenticalCandidates_RejectsAmbiguousWinner()
        {
            WeightedPhonemeDistance distance = CreateDistance();
            CompiledIncantation first = CreateIncantation(distance, "First", new ushort[] { 3, 1, 2 }, null);
            CompiledIncantation second = CreateIncantation(distance, "Second", new ushort[] { 3, 1, 2 }, null);
            List<CompiledIncantation> incantations = new List<CompiledIncantation> { first, second };
            IncantationMatcher matcher = new IncantationMatcher(incantations, distance, CreateConfig());
            PhonemeSequence observed = CreateSequence(new ushort[] { 3, 1, 2 });
            PhoneticObservation observation = PhoneticObservation.Create(observed, distance.CostModel.Inventory);

            IncantationMatchResult result = matcher.Match("en", observation);

            Assert.That(result.Margin, Is.EqualTo(0f));
            Assert.That(result.Accepted, Is.False);
        }

        [Test]
        public void Match_LeadingGibberishBeforeCompleteTerminalIncantation_AcceptsTerminalSpell()
        {
            WeightedPhonemeDistance distance = CreateDistance();
            CompiledIncantation arcaneBarrier = CreateIncantation(distance, "ArcaneBarrier", new ushort[] { 3, 1, 2, 4, 3, 1 }, new ushort[] { 3, 1 });
            CompiledIncantation meteor = CreateIncantation(distance, "Meteor", new ushort[] { 4, 2, 1, 3, 4, 2 }, new ushort[] { 4, 2 });
            List<CompiledIncantation> incantations = new List<CompiledIncantation> { arcaneBarrier, meteor };
            IncantationMatcher matcher = new IncantationMatcher(incantations, distance, CreateConfig());
            PhonemeSequence observed = CreateSequence(new ushort[] { 4, 2, 1, 3, 4, 2, 3, 1, 2, 4, 3, 1 });
            PhoneticObservation observation = PhoneticObservation.Create(observed, distance.CostModel.Inventory);

            IncantationMatchResult result = matcher.Match("en", observation);

            Assert.That(result.Best.Incantation.SpellId, Is.EqualTo("ArcaneBarrier"));
            Assert.That(result.Best.FullPhoneme, Is.EqualTo(1f));
            Assert.That(result.Best.ConsonantSkeleton, Is.EqualTo(1f));
            Assert.That(result.Best.Trigger, Is.EqualTo(1f));
            Assert.That(result.Accepted, Is.True);
        }

        [Test]
        public void Match_TriggerOnlyObservation_UsesOptInQuickSpellRecognition()
        {
            WeightedPhonemeDistance distance = CreateDistance();
            CompiledIncantation arcaneBarrier = CreateIncantation(distance, "ArcaneBarrier", new ushort[] { 3, 1, 2, 4, 3, 1 }, new ushort[] { 3, 1 });
            CompiledIncantation meteor = CreateIncantation(distance, "Meteor", new ushort[] { 4, 2, 1, 3, 4, 2 }, new ushort[] { 4, 2 });
            List<CompiledIncantation> incantations = new List<CompiledIncantation> { arcaneBarrier, meteor };
            IncantationMatcherConfig config = CreateConfig();
            config.AllowTriggerOnlyRecognition = true;
            IncantationMatcher matcher = new IncantationMatcher(incantations, distance, config);
            PhonemeSequence observed = CreateSequence(new ushort[] { 3, 1 });
            PhoneticObservation observation = PhoneticObservation.Create(observed, distance.CostModel.Inventory);

            IncantationMatchResult result = matcher.Match("en", observation);

            Assert.That(result.Best.Incantation.SpellId, Is.EqualTo("ArcaneBarrier"));
            Assert.That(result.Best.Total, Is.EqualTo(1f));
            Assert.That(result.MatchKind, Is.EqualTo(IncantationMatchKind.TriggerOnly));
            Assert.That(result.Accepted, Is.True);
        }

        [Test]
        public void Match_TriggerOnlyObservation_IsDisabledByDefault()
        {
            WeightedPhonemeDistance distance = CreateDistance();
            CompiledIncantation arcaneBarrier = CreateIncantation(distance, "ArcaneBarrier", new ushort[] { 3, 1, 2, 4, 3, 1 }, new ushort[] { 3, 1 });
            List<CompiledIncantation> incantations = new List<CompiledIncantation> { arcaneBarrier };
            IncantationMatcher matcher = new IncantationMatcher(incantations, distance, CreateConfig());
            PhonemeSequence observed = CreateSequence(new ushort[] { 3, 1 });
            PhoneticObservation observation = PhoneticObservation.Create(observed, distance.CostModel.Inventory);

            IncantationMatchResult result = matcher.Match("en", observation);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.MatchKind, Is.EqualTo(IncantationMatchKind.None));
        }

        [Test]
        public void Match_PartialIncantationWithEarlyTrigger_DoesNotCast()
        {
            WeightedPhonemeDistance distance = CreateDistance();
            CompiledIncantation arcaneBarrier = CreateIncantation(distance, "ArcaneBarrier", new ushort[] { 3, 1, 2, 4, 3, 1 }, new ushort[] { 3, 1 });
            List<CompiledIncantation> incantations = new List<CompiledIncantation> { arcaneBarrier };
            IncantationMatcherConfig config = CreateConfig();
            config.AllowTrailingSpeech = true;
            config.AllowTriggerOnlyRecognition = true;
            config.SuppressTriggerOnlyRecognitionDuringPartialIncantation = true;
            IncantationMatcher matcher = new IncantationMatcher(incantations, distance, config);
            PhonemeSequence observed = CreateSequence(new ushort[] { 3, 1, 2, 4 });
            PhoneticObservation observation = PhoneticObservation.Create(observed, distance.CostModel.Inventory);

            IncantationMatchResult result = matcher.Match("en", observation);

            Assert.That(result.Best.TriggerEndPhonemeIndex, Is.EqualTo(2));
            Assert.That(result.Best.FullIncantationEndPhonemeIndex, Is.EqualTo(4));
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.MatchKind, Is.EqualTo(IncantationMatchKind.None));
        }

        [Test]
        public void Match_TrailingSpeechAfterCompleteIncantation_AcceptsWhenEnabledAndReportsTriggerPosition()
        {
            WeightedPhonemeDistance distance = CreateDistance();
            CompiledIncantation arcaneBarrier = CreateIncantation(distance, "ArcaneBarrier", new ushort[] { 3, 1, 2, 4, 3, 1 }, new ushort[] { 3, 1 });
            CompiledIncantation meteor = CreateIncantation(distance, "Meteor", new ushort[] { 4, 2, 1, 3, 4, 2 }, new ushort[] { 4, 2 });
            List<CompiledIncantation> incantations = new List<CompiledIncantation> { arcaneBarrier, meteor };
            IncantationMatcherConfig config = CreateConfig();
            config.AllowTrailingSpeech = true;
            IncantationMatcher matcher = new IncantationMatcher(incantations, distance, config);
            PhonemeSequence observed = CreateSequence(new ushort[] { 3, 1, 2, 4, 3, 1, 4, 2, 1 });
            PhoneticObservation observation = PhoneticObservation.Create(observed, distance.CostModel.Inventory);

            IncantationMatchResult result = matcher.Match("en", observation);

            Assert.That(result.Best.Incantation.SpellId, Is.EqualTo("ArcaneBarrier"));
            Assert.That(result.Best.FullPhoneme, Is.EqualTo(1f));
            Assert.That(result.Best.Trigger, Is.EqualTo(1f));
            Assert.That(result.Best.TriggerEndPhonemeIndex, Is.EqualTo(6));
            Assert.That(result.Accepted, Is.True);
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

        private static CompiledIncantation CreateIncantation(WeightedPhonemeDistance distance, string spellId, ushort[] phonemeValues, ushort[] triggerValues)
        {
            PhonemeSequence phonemes = CreateSequence(phonemeValues);
            PhonemeSequence consonants = CreateConsonantSequence(phonemes, distance.CostModel.Inventory);
            PhonemeSequence trigger = triggerValues == null ? new PhonemeSequence(System.Array.Empty<PhonemeId>()) : CreateSequence(triggerValues);
            return new CompiledIncantation(
                spellId,
                "en",
                phonemes,
                consonants,
                trigger,
                distance.CalculateDeletionCost(phonemes.AsSpan()),
                distance.CalculateDeletionCost(consonants.AsSpan()),
                distance.CalculateDeletionCost(trigger.AsSpan()));
        }

        private static PhonemeSequence CreateConsonantSequence(PhonemeSequence phonemes, PhonemeInventory inventory)
        {
            List<PhonemeId> consonants = new List<PhonemeId>();
            System.ReadOnlySpan<PhonemeId> source = phonemes.AsSpan();
            for (int phonemeIndex = 0; phonemeIndex < source.Length; phonemeIndex++)
            {
                if (inventory.IsConsonant(source[phonemeIndex]))
                {
                    consonants.Add(source[phonemeIndex]);
                }
            }

            return new PhonemeSequence(consonants.ToArray());
        }

        private static PhonemeSequence CreateSequence(ushort[] values)
        {
            PhonemeId[] phonemes = new PhonemeId[values.Length];
            for (int phonemeIndex = 0; phonemeIndex < values.Length; phonemeIndex++)
            {
                phonemes[phonemeIndex] = new PhonemeId(values[phonemeIndex]);
            }

            return new PhonemeSequence(phonemes);
        }

        private static WeightedPhonemeDistance CreateDistance()
        {
            PhonemeInventory inventory = new PhonemeInventory();
            inventory.Register(new PhonemeId(1), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 0, vowelBackness: 0));
            inventory.Register(new PhonemeId(2), new PhonemeFeatures(PhonemeClass.Consonant, place: 0, manner: 0));
            inventory.Register(new PhonemeId(3), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 0, manner: 0));
            inventory.Register(new PhonemeId(4), new PhonemeFeatures(PhonemeClass.Consonant, place: 255, manner: 255));
            return new WeightedPhonemeDistance(new PhonemeCostModel(inventory));
        }
    }
}
