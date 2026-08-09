using System;
using System.Collections.Generic;
using H1M4W4R1.Incantia.Text;

namespace H1M4W4R1.Incantia.Phonetics.English
{
    /// <summary>
    /// Offline English phonemizer with explicit pronunciations for reference content and a deterministic transcript fallback.
    /// Register every fantasy term before compiling an incantation; unknown reference words are rejected.
    /// </summary>
    public sealed class EnglishPhonemizer : IReferencePhonemizer
    {
        private readonly Dictionary<string, PhonemeSequence> _pronunciations = new Dictionary<string, PhonemeSequence>(StringComparer.Ordinal);

        public EnglishPhonemizer()
        {
            RegisterBuiltInPronunciations();
        }

        public string Language => "en";

        /// <summary>Registers a word or whole normalized phrase, including a pronunciation override for fantasy terms.</summary>
        public void RegisterPronunciation(string text, params EnglishPhoneme[] phonemes)
        {
            if (ReferenceEquals(phonemes, null))
            {
                throw new ArgumentNullException(nameof(phonemes));
            }

            PhonemeId[] phonemeIds = new PhonemeId[phonemes.Length];
            for (int phonemeIndex = 0; phonemeIndex < phonemes.Length; phonemeIndex++)
            {
                phonemeIds[phonemeIndex] = EnglishPhonemeProfile.ToId(phonemes[phonemeIndex]);
            }

            RegisterPronunciation(text, new PhonemeSequence(phonemeIds));
        }

        public void RegisterPronunciation(string text, PhonemeSequence pronunciation)
        {
            string normalizedText = IncantationTextNormalizer.Normalize(text);
            if (normalizedText.Length == 0)
            {
                throw new ArgumentException("Pronunciation text must contain at least one word.", nameof(text));
            }

            if (pronunciation.IsEmpty)
            {
                throw new ArgumentException("A pronunciation must contain at least one phoneme.", nameof(pronunciation));
            }

            _pronunciations[normalizedText] = pronunciation;
        }

        /// <summary>Phonemizes an ASR transcript. Unrecognized words use deterministic spelling-to-sound fallback rules.</summary>
        public PhonemeSequence Phonemize(string text)
        {
            return Phonemize(text, false);
        }

        /// <summary>Phonemizes authoring text. Every word must have a registered pronunciation.</summary>
        public PhonemeSequence PhonemizeReference(string text)
        {
            return Phonemize(text, true);
        }

        private PhonemeSequence Phonemize(string text, bool rejectUnknownWords)
        {
            string normalizedText = IncantationTextNormalizer.Normalize(text);
            if (normalizedText.Length == 0)
            {
                return new PhonemeSequence(Array.Empty<PhonemeId>());
            }

            if (_pronunciations.TryGetValue(normalizedText, out PhonemeSequence fullTextPronunciation))
            {
                return fullTextPronunciation;
            }

            List<PhonemeId> result = new List<PhonemeId>(normalizedText.Length);
            int wordStart = 0;
            while (wordStart < normalizedText.Length)
            {
                while (wordStart < normalizedText.Length && normalizedText[wordStart] == ' ')
                {
                    wordStart++;
                }

                if (wordStart >= normalizedText.Length)
                {
                    break;
                }

                int wordEnd = wordStart;
                while (wordEnd < normalizedText.Length && normalizedText[wordEnd] != ' ')
                {
                    wordEnd++;
                }

                string word = normalizedText.Substring(wordStart, wordEnd - wordStart);
                if (_pronunciations.TryGetValue(word, out PhonemeSequence pronunciation))
                {
                    Append(result, pronunciation.AsSpan());
                }
                else if (rejectUnknownWords)
                {
                    throw new InvalidOperationException($"No English pronunciation is registered for reference word '{word}'. Register a pronunciation override before compiling this incantation.");
                }
                else
                {
                    AppendFallbackPronunciation(word, result);
                }

                wordStart = wordEnd;
            }

            return new PhonemeSequence(result.ToArray());
        }

        private static void Append(List<PhonemeId> output, ReadOnlySpan<PhonemeId> phonemes)
        {
            for (int phonemeIndex = 0; phonemeIndex < phonemes.Length; phonemeIndex++)
            {
                output.Add(phonemes[phonemeIndex]);
            }
        }

        private static void AppendFallbackPronunciation(string word, List<PhonemeId> output)
        {
            int characterIndex = 0;
            while (characterIndex < word.Length)
            {
                if (TryAppendCluster(word, ref characterIndex, output))
                {
                    continue;
                }

                char character = word[characterIndex];
                switch (character)
                {
                    case 'a':
                        AppendVowel(word, ref characterIndex, output, EnglishPhoneme.AE, EnglishPhoneme.AY, "ai", "ay", null);
                        break;
                    case 'e':
                        if (characterIndex == word.Length - 1 && word.Length > 1)
                        {
                            characterIndex++;
                        }
                        else
                        {
                            AppendVowel(word, ref characterIndex, output, EnglishPhoneme.EH, EnglishPhoneme.IY, "ee", "ea", "ie");
                        }

                        break;
                    case 'i':
                        AppendVowel(word, ref characterIndex, output, EnglishPhoneme.IH, EnglishPhoneme.AY, "ie", "igh", null);
                        break;
                    case 'o':
                        AppendVowel(word, ref characterIndex, output, EnglishPhoneme.AA, EnglishPhoneme.OW, "oa", "oe", "ow");
                        break;
                    case 'u':
                        AppendVowel(word, ref characterIndex, output, EnglishPhoneme.AH, EnglishPhoneme.UW, "oo", "ue", "ui");
                        break;
                    case 'y':
                        output.Add(EnglishPhonemeProfile.ToId(characterIndex == 0 ? EnglishPhoneme.Y : EnglishPhoneme.IY));
                        characterIndex++;
                        break;
                    case 'b':
                        Append(output, EnglishPhoneme.B, ref characterIndex);
                        break;
                    case 'c':
                        Append(output, IsFollowedByFrontVowel(word, characterIndex) ? EnglishPhoneme.S : EnglishPhoneme.K, ref characterIndex);
                        break;
                    case 'd':
                        Append(output, EnglishPhoneme.D, ref characterIndex);
                        break;
                    case 'f':
                        Append(output, EnglishPhoneme.F, ref characterIndex);
                        break;
                    case 'g':
                        Append(output, IsFollowedByFrontVowel(word, characterIndex) ? EnglishPhoneme.JH : EnglishPhoneme.G, ref characterIndex);
                        break;
                    case 'h':
                        Append(output, EnglishPhoneme.HH, ref characterIndex);
                        break;
                    case 'j':
                        Append(output, EnglishPhoneme.JH, ref characterIndex);
                        break;
                    case 'k':
                        Append(output, EnglishPhoneme.K, ref characterIndex);
                        break;
                    case 'l':
                        Append(output, EnglishPhoneme.L, ref characterIndex);
                        break;
                    case 'm':
                        Append(output, EnglishPhoneme.M, ref characterIndex);
                        break;
                    case 'n':
                        Append(output, EnglishPhoneme.N, ref characterIndex);
                        break;
                    case 'p':
                        Append(output, EnglishPhoneme.P, ref characterIndex);
                        break;
                    case 'q':
                        Append(output, EnglishPhoneme.K, ref characterIndex);
                        break;
                    case 'r':
                        Append(output, EnglishPhoneme.R, ref characterIndex);
                        break;
                    case 's':
                        Append(output, EnglishPhoneme.S, ref characterIndex);
                        break;
                    case 't':
                        Append(output, EnglishPhoneme.T, ref characterIndex);
                        break;
                    case 'v':
                        Append(output, EnglishPhoneme.V, ref characterIndex);
                        break;
                    case 'w':
                        Append(output, EnglishPhoneme.W, ref characterIndex);
                        break;
                    case 'x':
                        output.Add(EnglishPhonemeProfile.ToId(EnglishPhoneme.K));
                        output.Add(EnglishPhonemeProfile.ToId(EnglishPhoneme.S));
                        characterIndex++;
                        break;
                    case 'z':
                        Append(output, EnglishPhoneme.Z, ref characterIndex);
                        break;
                    default:
                        characterIndex++;
                        break;
                }
            }
        }

        private static bool TryAppendCluster(string word, ref int characterIndex, List<PhonemeId> output)
        {
            if (Matches(word, characterIndex, "tch"))
            {
                Append(output, EnglishPhoneme.CH, ref characterIndex, 3);
                return true;
            }

            if (Matches(word, characterIndex, "dge"))
            {
                Append(output, EnglishPhoneme.JH, ref characterIndex, 3);
                return true;
            }

            if (Matches(word, characterIndex, "ch"))
            {
                Append(output, EnglishPhoneme.CH, ref characterIndex, 2);
                return true;
            }

            if (Matches(word, characterIndex, "sh"))
            {
                Append(output, EnglishPhoneme.SH, ref characterIndex, 2);
                return true;
            }

            if (Matches(word, characterIndex, "th"))
            {
                Append(output, EnglishPhoneme.TH, ref characterIndex, 2);
                return true;
            }

            if (Matches(word, characterIndex, "ph"))
            {
                Append(output, EnglishPhoneme.F, ref characterIndex, 2);
                return true;
            }

            if (Matches(word, characterIndex, "ng"))
            {
                Append(output, EnglishPhoneme.NG, ref characterIndex, 2);
                return true;
            }

            if (Matches(word, characterIndex, "qu"))
            {
                output.Add(EnglishPhonemeProfile.ToId(EnglishPhoneme.K));
                output.Add(EnglishPhonemeProfile.ToId(EnglishPhoneme.W));
                characterIndex += 2;
                return true;
            }

            if (Matches(word, characterIndex, "ck"))
            {
                Append(output, EnglishPhoneme.K, ref characterIndex, 2);
                return true;
            }

            if (Matches(word, characterIndex, "wh"))
            {
                Append(output, EnglishPhoneme.W, ref characterIndex, 2);
                return true;
            }

            if (characterIndex == 0 && (Matches(word, characterIndex, "kn") || Matches(word, characterIndex, "wr")))
            {
                characterIndex++;
                return true;
            }

            return false;
        }

        private static void AppendVowel(
            string word,
            ref int characterIndex,
            List<PhonemeId> output,
            EnglishPhoneme shortVowel,
            EnglishPhoneme longVowel,
            string firstLongPattern,
            string secondLongPattern,
            string thirdLongPattern)
        {
            if (Matches(word, characterIndex, firstLongPattern)
                || Matches(word, characterIndex, secondLongPattern)
                || Matches(word, characterIndex, thirdLongPattern))
            {
                string matchingPattern = Matches(word, characterIndex, firstLongPattern)
                    ? firstLongPattern
                    : Matches(word, characterIndex, secondLongPattern)
                        ? secondLongPattern
                        : thirdLongPattern;
                output.Add(EnglishPhonemeProfile.ToId(longVowel));
                characterIndex += matchingPattern.Length;
                return;
            }

            output.Add(EnglishPhonemeProfile.ToId(shortVowel));
            characterIndex++;
        }

        private static void Append(List<PhonemeId> output, EnglishPhoneme phoneme, ref int characterIndex, int characterCount = 1)
        {
            output.Add(EnglishPhonemeProfile.ToId(phoneme));
            characterIndex += characterCount;
        }

        private static bool IsFollowedByFrontVowel(string word, int characterIndex)
        {
            if (characterIndex + 1 >= word.Length)
            {
                return false;
            }

            char nextCharacter = word[characterIndex + 1];
            return nextCharacter == 'e' || nextCharacter == 'i' || nextCharacter == 'y';
        }

        private static bool Matches(string word, int characterIndex, string pattern)
        {
            if (ReferenceEquals(pattern, null))
            {
                return false;
            }

            if (characterIndex + pattern.Length > word.Length)
            {
                return false;
            }

            for (int patternIndex = 0; patternIndex < pattern.Length; patternIndex++)
            {
                if (word[characterIndex + patternIndex] != pattern[patternIndex])
                {
                    return false;
                }
            }

            return true;
        }

        private void RegisterBuiltInPronunciations()
        {
            RegisterPronunciation("a", EnglishPhoneme.AH);
            RegisterPronunciation("ancient", EnglishPhoneme.EY, EnglishPhoneme.N, EnglishPhoneme.SH, EnglishPhoneme.AH, EnglishPhoneme.N, EnglishPhoneme.T);
            RegisterPronunciation("and", EnglishPhoneme.AE, EnglishPhoneme.N, EnglishPhoneme.D);
            RegisterPronunciation("agent", EnglishPhoneme.EY, EnglishPhoneme.JH, EnglishPhoneme.AH, EnglishPhoneme.N, EnglishPhoneme.T);
            RegisterPronunciation("away", EnglishPhoneme.AH, EnglishPhoneme.W, EnglishPhoneme.EY);
            RegisterPronunciation("ball", EnglishPhoneme.B, EnglishPhoneme.AO, EnglishPhoneme.L);
            RegisterPronunciation("born", EnglishPhoneme.B, EnglishPhoneme.AO, EnglishPhoneme.R, EnglishPhoneme.N);
            RegisterPronunciation("burn", EnglishPhoneme.B, EnglishPhoneme.ER, EnglishPhoneme.N);
            RegisterPronunciation("by", EnglishPhoneme.B, EnglishPhoneme.AY);
            RegisterPronunciation("dark", EnglishPhoneme.D, EnglishPhoneme.AA, EnglishPhoneme.R, EnglishPhoneme.K);
            RegisterPronunciation("darkness", EnglishPhoneme.D, EnglishPhoneme.AA, EnglishPhoneme.R, EnglishPhoneme.K, EnglishPhoneme.N, EnglishPhoneme.AH, EnglishPhoneme.S);
            RegisterPronunciation("enemy", EnglishPhoneme.EH, EnglishPhoneme.N, EnglishPhoneme.AH, EnglishPhoneme.M, EnglishPhoneme.IY);
            RegisterPronunciation("fire", EnglishPhoneme.F, EnglishPhoneme.AY, EnglishPhoneme.ER);
            RegisterPronunciation("fireball", EnglishPhoneme.F, EnglishPhoneme.AY, EnglishPhoneme.ER, EnglishPhoneme.B, EnglishPhoneme.AO, EnglishPhoneme.L);
            RegisterPronunciation("flame", EnglishPhoneme.F, EnglishPhoneme.L, EnglishPhoneme.EY, EnglishPhoneme.M);
            RegisterPronunciation("flyer", EnglishPhoneme.F, EnglishPhoneme.L, EnglishPhoneme.AY, EnglishPhoneme.ER);
            RegisterPronunciation("foe", EnglishPhoneme.F, EnglishPhoneme.OW);
            RegisterPronunciation("frozen", EnglishPhoneme.F, EnglishPhoneme.R, EnglishPhoneme.OW, EnglishPhoneme.Z, EnglishPhoneme.AH, EnglishPhoneme.N);
            RegisterPronunciation("gather", EnglishPhoneme.G, EnglishPhoneme.AE, EnglishPhoneme.DH, EnglishPhoneme.ER);
            RegisterPronunciation("gathered", EnglishPhoneme.G, EnglishPhoneme.AE, EnglishPhoneme.DH, EnglishPhoneme.ER, EnglishPhoneme.D);
            RegisterPronunciation("greater", EnglishPhoneme.G, EnglishPhoneme.R, EnglishPhoneme.EY, EnglishPhoneme.T, EnglishPhoneme.ER);
            RegisterPronunciation("hand", EnglishPhoneme.HH, EnglishPhoneme.AE, EnglishPhoneme.N, EnglishPhoneme.D);
            RegisterPronunciation("head", EnglishPhoneme.HH, EnglishPhoneme.EH, EnglishPhoneme.D);
            RegisterPronunciation("hear", EnglishPhoneme.HH, EnglishPhoneme.IY, EnglishPhoneme.R);
            RegisterPronunciation("heal", EnglishPhoneme.HH, EnglishPhoneme.IY, EnglishPhoneme.L);
            RegisterPronunciation("here", EnglishPhoneme.HH, EnglishPhoneme.IY, EnglishPhoneme.R);
            RegisterPronunciation("ice", EnglishPhoneme.AY, EnglishPhoneme.S);
            RegisterPronunciation("in", EnglishPhoneme.IH, EnglishPhoneme.N);
            RegisterPronunciation("lance", EnglishPhoneme.L, EnglishPhoneme.AE, EnglishPhoneme.N, EnglishPhoneme.S);
            RegisterPronunciation("lake", EnglishPhoneme.L, EnglishPhoneme.EY, EnglishPhoneme.K);
            RegisterPronunciation("mess", EnglishPhoneme.M, EnglishPhoneme.EH, EnglishPhoneme.S);
            RegisterPronunciation("moon", EnglishPhoneme.M, EnglishPhoneme.UW, EnglishPhoneme.N);
            RegisterPronunciation("my", EnglishPhoneme.M, EnglishPhoneme.AY);
            RegisterPronunciation("of", EnglishPhoneme.AH, EnglishPhoneme.V);
            RegisterPronunciation("pear", EnglishPhoneme.P, EnglishPhoneme.EH, EnglishPhoneme.R);
            RegisterPronunciation("pierce", EnglishPhoneme.P, EnglishPhoneme.IY, EnglishPhoneme.R, EnglishPhoneme.S);
            RegisterPronunciation("prayer", EnglishPhoneme.P, EnglishPhoneme.R, EnglishPhoneme.EH, EnglishPhoneme.R);
            RegisterPronunciation("silent", EnglishPhoneme.S, EnglishPhoneme.AY, EnglishPhoneme.L, EnglishPhoneme.AH, EnglishPhoneme.N, EnglishPhoneme.T);
            RegisterPronunciation("solar", EnglishPhoneme.S, EnglishPhoneme.OW, EnglishPhoneme.L, EnglishPhoneme.ER);
            RegisterPronunciation("storm", EnglishPhoneme.S, EnglishPhoneme.T, EnglishPhoneme.AO, EnglishPhoneme.R, EnglishPhoneme.M);
            RegisterPronunciation("strike", EnglishPhoneme.S, EnglishPhoneme.T, EnglishPhoneme.R, EnglishPhoneme.AY, EnglishPhoneme.K);
            RegisterPronunciation("sun", EnglishPhoneme.S, EnglishPhoneme.AH, EnglishPhoneme.N);
            RegisterPronunciation("teleport", EnglishPhoneme.T, EnglishPhoneme.EH, EnglishPhoneme.L, EnglishPhoneme.AH, EnglishPhoneme.P, EnglishPhoneme.AO, EnglishPhoneme.R, EnglishPhoneme.T);
            RegisterPronunciation("the", EnglishPhoneme.DH, EnglishPhoneme.AH);
            RegisterPronunciation("toe", EnglishPhoneme.T, EnglishPhoneme.OW);
            RegisterPronunciation("with", EnglishPhoneme.W, EnglishPhoneme.IH, EnglishPhoneme.DH);
            RegisterPronunciation("within", EnglishPhoneme.W, EnglishPhoneme.IH, EnglishPhoneme.DH, EnglishPhoneme.IH, EnglishPhoneme.N);
        }
    }
}
