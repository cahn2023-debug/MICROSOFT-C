using System;
using System.Collections.Generic;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Implementation of IHighlightService.
    /// Provides accent-insensitive text matching using Vietnamese text normalization.
    /// </summary>
    public class HighlightService : IHighlightService
    {
        private readonly IVietnameseTextNormalizer _normalizer;
        private List<(int Start, int Length)> _matches = new List<(int, int)>();
        private int _currentIndex = -1;

        public HighlightService(IVietnameseTextNormalizer normalizer)
        {
            _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        }

        public int MatchCount => _matches.Count;

        public int CurrentMatchIndex
        {
            get => _currentIndex;
            set
            {
                if (value >= -1 && value < _matches.Count)
                {
                    _currentIndex = value;
                }
            }
        }

        /// <inheritdoc/>
        public List<(int Start, int Length)> FindMatches(string text, string keyword)
        {
            _matches.Clear();
            _currentIndex = -1;

            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
            {
                return _matches;
            }

            // Normalize keyword
            string keywordNorm = _normalizer.RemoveAccents(keyword);

            // Build normalized text with index mapping
            var (textNorm, indexMap) = _normalizer.BuildNoAccentAndMap(text);

            // Find all matches in normalized text
            int searchStart = 0;
            while (searchStart < textNorm.Length)
            {
                int idx = textNorm.IndexOf(keywordNorm, searchStart, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) break;

                // Map normalized indices back to original text
                int originalStart = idx < indexMap.Count ? indexMap[idx] : idx;
                int normalizedEnd = idx + keywordNorm.Length;
                int originalEnd = normalizedEnd < indexMap.Count ? indexMap[normalizedEnd] : text.Length;
                int originalLength = originalEnd - originalStart;

                if (originalLength > 0 && originalStart + originalLength <= text.Length)
                {
                    _matches.Add((originalStart, originalLength));
                }

                searchStart = idx + 1;
            }

            // Set to first match if any found
            if (_matches.Count > 0)
            {
                _currentIndex = 0;
            }

            return _matches;
        }

        /// <inheritdoc/>
        public (int Start, int Length)? NextMatch()
        {
            if (_matches.Count == 0) return null;

            _currentIndex++;
            if (_currentIndex >= _matches.Count)
            {
                _currentIndex = 0; // Wrap around
            }

            return _matches[_currentIndex];
        }

        /// <inheritdoc/>
        public (int Start, int Length)? PreviousMatch()
        {
            if (_matches.Count == 0) return null;

            _currentIndex--;
            if (_currentIndex < 0)
            {
                _currentIndex = _matches.Count - 1; // Wrap around
            }

            return _matches[_currentIndex];
        }

        /// <summary>
        /// Gets the first match if available.
        /// </summary>
        public (int Start, int Length)? GetFirstMatch()
        {
            if (_matches.Count == 0) return null;
            _currentIndex = 0;
            return _matches[0];
        }

        /// <summary>
        /// Gets a specific match by index.
        /// </summary>
        public (int Start, int Length)? GetMatch(int index)
        {
            if (index < 0 || index >= _matches.Count) return null;
            _currentIndex = index;
            return _matches[index];
        }
    }
}
