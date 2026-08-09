using System;

namespace H1M4W4R1.Incantia.Phonetics
{
    /// <summary>Reusable rolling rows for weighted phoneme alignment.</summary>
    public sealed class PhonemeDistanceWorkspace
    {
        private float[] _previous = Array.Empty<float>();
        private float[] _current = Array.Empty<float>();

        internal Span<float> Previous => _previous;
        internal Span<float> Current => _current;

        internal void EnsureCapacity(int requiredLength)
        {
            if (_previous.Length >= requiredLength)
            {
                return;
            }

            _previous = new float[requiredLength];
            _current = new float[requiredLength];
        }

        internal void SwapRows()
        {
            float[] previous = _previous;
            _previous = _current;
            _current = previous;
        }
    }
}
