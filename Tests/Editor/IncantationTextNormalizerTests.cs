using H1M4W4R1.Incantia.Text;
using NUnit.Framework;

namespace H1M4W4R1.Incantia.Tests
{
    public sealed class IncantationTextNormalizerTests
    {
        [TestCase("[BLANK_AUDIO] Flame [noise] of the ancient sun", "flame of the ancient sun")]
        [TestCase("fire[background noise]ball", "fire ball")]
        [TestCase("flame [noise [echo]] ancient sun", "flame ancient sun")]
        [TestCase("flame [unfinished annotation", "flame")]
        public void Normalize_SquareBracketAnnotation_RemovesAnnotationText(string transcript, string expected)
        {
            string normalized = IncantationTextNormalizer.Normalize(transcript);

            Assert.That(normalized, Is.EqualTo(expected));
        }

        [TestCase("(background noise) Flame of the ancient sun", "flame of the ancient sun")]
        [TestCase("fire(cough)ball", "fire ball")]
        [TestCase("flame (noise (echo)) ancient sun", "flame ancient sun")]
        [TestCase("flame (unfinished annotation", "flame")]
        public void Normalize_ParenthesizedAnnotation_RemovesAnnotationText(string transcript, string expected)
        {
            string normalized = IncantationTextNormalizer.Normalize(transcript);

            Assert.That(normalized, Is.EqualTo(expected));
        }
    }
}
