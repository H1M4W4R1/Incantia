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
            int squareBracketDepth = 0;
            int parenthesisDepth = 0;
            for (int characterIndex = 0; characterIndex < canonicalText.Length; characterIndex++)
            {
                char character = canonicalText[characterIndex];
                if (squareBracketDepth > 0)
                {
                    if (character == '[')
                    {
                        squareBracketDepth++;
                    }
                    else if (character == ']')
                    {
                        squareBracketDepth--;
                    }

                    continue;
                }

                if (parenthesisDepth > 0)
                {
                    if (character == '(')
                    {
                        parenthesisDepth++;
                    }
                    else if (character == ')')
                    {
                        parenthesisDepth--;
                    }

                    continue;
                }

                if (character == '[' || character == '(')
                {
                    if (!previousWasWhitespace)
                    {
                        builder.Append(' ');
                        previousWasWhitespace = true;
                    }

                    if (character == '[')
                    {
                        squareBracketDepth = 1;
                    }
                    else
                    {
                        parenthesisDepth = 1;
                    }

                    continue;
                }

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
