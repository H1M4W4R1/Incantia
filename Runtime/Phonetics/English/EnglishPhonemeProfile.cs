using H1M4W4R1.Incantia.Phonetics;

namespace H1M4W4R1.Incantia.Phonetics.English
{
    /// <summary>Creates the standard English inventory and conservative accent-tolerance costs.</summary>
    public static class EnglishPhonemeProfile
    {
        public static PhonemeInventory CreateInventory()
        {
            PhonemeInventory inventory = new PhonemeInventory();
            RegisterVowels(inventory);
            RegisterConsonants(inventory);
            return inventory;
        }

        public static PhonemeCostModel CreateCostModel()
        {
            PhonemeCostModel costModel = new PhonemeCostModel(CreateInventory());
            costModel.SetSubstitutionOverride(ToId(EnglishPhoneme.TH), ToId(EnglishPhoneme.S), 0.15f);
            costModel.SetSubstitutionOverride(ToId(EnglishPhoneme.TH), ToId(EnglishPhoneme.T), 0.35f);
            costModel.SetSubstitutionOverride(ToId(EnglishPhoneme.DH), ToId(EnglishPhoneme.Z), 0.15f);
            costModel.SetSubstitutionOverride(ToId(EnglishPhoneme.DH), ToId(EnglishPhoneme.D), 0.35f);
            costModel.SetSubstitutionOverride(ToId(EnglishPhoneme.W), ToId(EnglishPhoneme.V), 0.15f);
            return costModel;
        }

        public static PhonemeId ToId(EnglishPhoneme phoneme)
        {
            return new PhonemeId((ushort)phoneme);
        }

        private static void RegisterVowels(PhonemeInventory inventory)
        {
            inventory.Register(ToId(EnglishPhoneme.AA), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 255, vowelBackness: 255));
            inventory.Register(ToId(EnglishPhoneme.AE), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 255, vowelBackness: 0));
            inventory.Register(ToId(EnglishPhoneme.AH), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 128, vowelBackness: 128));
            inventory.Register(ToId(EnglishPhoneme.AO), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 128, vowelBackness: 255, rounded: true));
            inventory.Register(ToId(EnglishPhoneme.AW), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 192, vowelBackness: 192, rounded: true));
            inventory.Register(ToId(EnglishPhoneme.AY), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 192, vowelBackness: 32));
            inventory.Register(ToId(EnglishPhoneme.EH), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 128, vowelBackness: 0));
            inventory.Register(ToId(EnglishPhoneme.ER), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 128, vowelBackness: 128));
            inventory.Register(ToId(EnglishPhoneme.EY), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 64, vowelBackness: 0));
            inventory.Register(ToId(EnglishPhoneme.IH), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 64, vowelBackness: 0));
            inventory.Register(ToId(EnglishPhoneme.IY), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 0, vowelBackness: 0));
            inventory.Register(ToId(EnglishPhoneme.OW), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 64, vowelBackness: 255, rounded: true));
            inventory.Register(ToId(EnglishPhoneme.OY), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 96, vowelBackness: 160, rounded: true));
            inventory.Register(ToId(EnglishPhoneme.UH), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 64, vowelBackness: 255, rounded: true));
            inventory.Register(ToId(EnglishPhoneme.UW), new PhonemeFeatures(PhonemeClass.Vowel, vowelHeight: 0, vowelBackness: 255, rounded: true));
        }

        private static void RegisterConsonants(PhonemeInventory inventory)
        {
            inventory.Register(ToId(EnglishPhoneme.B), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 0, manner: 0));
            inventory.Register(ToId(EnglishPhoneme.CH), new PhonemeFeatures(PhonemeClass.Consonant, place: 192, manner: 160));
            inventory.Register(ToId(EnglishPhoneme.D), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 128, manner: 0));
            inventory.Register(ToId(EnglishPhoneme.DH), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 64, manner: 128));
            inventory.Register(ToId(EnglishPhoneme.F), new PhonemeFeatures(PhonemeClass.Consonant, place: 0, manner: 128));
            inventory.Register(ToId(EnglishPhoneme.G), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 255, manner: 0));
            inventory.Register(ToId(EnglishPhoneme.HH), new PhonemeFeatures(PhonemeClass.Consonant, place: 255, manner: 128));
            inventory.Register(ToId(EnglishPhoneme.JH), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 192, manner: 160));
            inventory.Register(ToId(EnglishPhoneme.K), new PhonemeFeatures(PhonemeClass.Consonant, place: 255, manner: 0));
            inventory.Register(ToId(EnglishPhoneme.L), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 128, manner: 224));
            inventory.Register(ToId(EnglishPhoneme.M), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 0, manner: 64));
            inventory.Register(ToId(EnglishPhoneme.N), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 128, manner: 64));
            inventory.Register(ToId(EnglishPhoneme.NG), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 255, manner: 64));
            inventory.Register(ToId(EnglishPhoneme.P), new PhonemeFeatures(PhonemeClass.Consonant, place: 0, manner: 0));
            inventory.Register(ToId(EnglishPhoneme.R), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 128, manner: 192));
            inventory.Register(ToId(EnglishPhoneme.S), new PhonemeFeatures(PhonemeClass.Consonant, place: 128, manner: 128));
            inventory.Register(ToId(EnglishPhoneme.SH), new PhonemeFeatures(PhonemeClass.Consonant, place: 192, manner: 128));
            inventory.Register(ToId(EnglishPhoneme.T), new PhonemeFeatures(PhonemeClass.Consonant, place: 128, manner: 0));
            inventory.Register(ToId(EnglishPhoneme.TH), new PhonemeFeatures(PhonemeClass.Consonant, place: 64, manner: 128));
            inventory.Register(ToId(EnglishPhoneme.V), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 0, manner: 128));
            inventory.Register(ToId(EnglishPhoneme.W), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 0, manner: 192));
            inventory.Register(ToId(EnglishPhoneme.Y), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 128, manner: 192));
            inventory.Register(ToId(EnglishPhoneme.Z), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 128, manner: 128));
            inventory.Register(ToId(EnglishPhoneme.ZH), new PhonemeFeatures(PhonemeClass.Consonant | PhonemeClass.Voiced, place: 192, manner: 128));
        }
    }
}
