using System;
using System.Text;

namespace H1M4W4R1.Incantia.Text
{
    /// <summary>Applies only pronunciation-safe normalization before phonemization.</summary>
    public static class IncantationTextNormalizer
    {
        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string canonicalText = text.Normalize(NormalizationForm.FormC).ToLowerInvariant();
            StringBuilder builder = new StringBuilder(canonicalText.Length);
            bool previousWasWhitespace = true;
            for (int characterIndex = 0; characterIndex < canonicalText.Length; characterIndex++)
            {
                char character = canonicalText[characterIndex];
                if (char.IsWhiteSpace(character))
                {
                    if (!previousWasWhitespace)
                    {
                        builder.Append(' ');
                        previousWasWhitespace = true;
                    }

                    continue;
                }

                if (char.IsPunctuation(character) || char.IsSymbol(character))
                {
                    continue;
                }

                builder.Append(character);
                previousWasWhitespace = false;
            }

            if (builder.Length > 0 && builder[builder.Length - 1] == ' ')
            {
                builder.Length--;
            }

            return builder.ToString();
        }
    }
}
