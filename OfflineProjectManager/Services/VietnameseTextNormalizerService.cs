using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Implementation of IVietnameseTextNormalizer.
    /// Wraps the existing VietnameseTextHelper static methods.
    /// </summary>
    public class VietnameseTextNormalizerService : IVietnameseTextNormalizer
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<char, (char baseChar, bool hasAccent)> _charCache =
            new System.Collections.Concurrent.ConcurrentDictionary<char, (char, bool)>();

        /// <inheritdoc/>
        public string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Normalize(NormalizationForm.FormC).ToLower();
        }

        /// <inheritdoc/>
        public string RemoveAccents(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string normalizedString = text.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder();

            foreach (char c in normalizedString)
            {
                UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            string result = stringBuilder.ToString().Normalize(NormalizationForm.FormC).ToLower();
            result = result.Replace('đ', 'd');

            return result;
        }

        /// <inheritdoc/>
        public (string NoAccentText, List<int> IndexMap) BuildNoAccentAndMap(string originalText)
        {
            if (string.IsNullOrEmpty(originalText)) return ("", new List<int>());

            StringBuilder noAccentBuilder = new StringBuilder(originalText.Length);
            List<int> idxMap = new List<int>(originalText.Length);

            for (int i = 0; i < originalText.Length; i++)
            {
                char ch = originalText[i];

                if (!_charCache.TryGetValue(ch, out var cached))
                {
                    string decomposed = ch.ToString().Normalize(NormalizationForm.FormD);
                    char baseChar = ' ';
                    bool foundBase = false;

                    foreach (char dc in decomposed)
                    {
                        if (CharUnicodeInfo.GetUnicodeCategory(dc) != UnicodeCategory.NonSpacingMark)
                        {
                            baseChar = char.ToLowerInvariant(dc);
                            if (baseChar == 'đ') baseChar = 'd';
                            foundBase = true;
                            break;
                        }
                    }

                    if (!foundBase) baseChar = char.ToLowerInvariant(ch);
                    cached = (baseChar, decomposed.Length > 1 || baseChar != char.ToLowerInvariant(ch));
                    _charCache.TryAdd(ch, cached);
                }

                noAccentBuilder.Append(cached.baseChar);
                idxMap.Add(i);
            }

            // Add sentinel for end-of-string mapping
            idxMap.Add(originalText.Length);

            return (noAccentBuilder.ToString(), idxMap);
        }
    }
}
