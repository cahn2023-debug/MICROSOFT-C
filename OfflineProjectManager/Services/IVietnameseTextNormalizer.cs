using System.Collections.Generic;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Interface for Vietnamese text normalization operations.
    /// Supports accent-insensitive searching and highlighting.
    /// </summary>
    public interface IVietnameseTextNormalizer
    {
        /// <summary>
        /// Normalizes text to NFC form and lowercase.
        /// </summary>
        string Normalize(string text);

        /// <summary>
        /// Removes all Vietnamese accents from text.
        /// </summary>
        string RemoveAccents(string text);

        /// <summary>
        /// Builds accent-removed text with index mapping to original positions.
        /// </summary>
        /// <param name="originalText">The original text in NFC form</param>
        /// <returns>Tuple of (noAccentText, indexMap) where indexMap[i] = original index</returns>
        (string NoAccentText, List<int> IndexMap) BuildNoAccentAndMap(string originalText);
    }
}
