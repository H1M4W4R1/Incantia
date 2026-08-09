using System;

namespace H1M4W4R1.Incantia.Phonetics
{
    /// <summary>Reusable rolling rows for weighted phoneme alignment.</summary>
    public sealed class PhonemeDistanceWorkspace
    {
        private float[] _previous = Array.Empty<float>();
        private float[] _current = Array.Empty<float>();
        private float[] _insertionCosts = Array.Empty<float>();

        internal Span<float> Previous => _previous;
        internal Span<float> Current => _current;
        internal Span<float> InsertionCosts => _insertionCosts;

        internal void EnsureCapacity(int requiredLength)
        {
            if (_previous.Length >= requiredLength)
            {
                return;
            }

            int newLength = _previous.Length == 0 ? 16 : _previous.Length * 2;
            if (newLength < requiredLength)
            {
                newLength = requiredLength;
            }

            _previous = new float[newLength];
            _current = new float[newLength];
            _insertionCosts = new float[newLength];
        }

        internal void SwapRows()
        {
            float[] previous = _previous;
            _previous = _current;
            _current = previous;
        }
    }
}
