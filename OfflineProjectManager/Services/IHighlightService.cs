using System.Collections.Generic;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Interface for highlight operations across different preview controls.
    /// Supports Vietnamese accent-insensitive matching.
    /// </summary>
    public interface IHighlightService
    {
        /// <summary>
        /// Finds all match ranges for the keyword in the given text.
        /// Supports Vietnamese accent-insensitive matching.
        /// </summary>
        /// <param name="text">The original text to search in</param>
        /// <param name="keyword">The search keyword</param>
        /// <returns>List of (Start, Length) tuples in original text coordinates</returns>
        List<(int Start, int Length)> FindMatches(string text, string keyword);

        /// <summary>
        /// Gets the total number of matches found in the last search.
        /// </summary>
        int MatchCount { get; }

        /// <summary>
        /// Gets or sets the current match index for navigation.
        /// </summary>
        int CurrentMatchIndex { get; set; }

        /// <summary>
        /// Moves to the next match.
        /// </summary>
        /// <returns>The next match position, or null if no more matches</returns>
        (int Start, int Length)? NextMatch();

        /// <summary>
        /// Moves to the previous match.
        /// </summary>
        /// <returns>The previous match position, or null if no more matches</returns>
        (int Start, int Length)? PreviousMatch();
    }
}
