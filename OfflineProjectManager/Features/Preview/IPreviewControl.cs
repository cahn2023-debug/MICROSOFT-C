using System;
using System.Windows;

namespace OfflineProjectManager.Features.Preview
{
    /// <summary>
    /// Standardized interface for all preview controls.
    /// Ensures proper lifecycle management and consistent API across all preview types.
    /// </summary>
    public interface IPreviewControl : IDisposable
    {
        /// <summary>
        /// The WPF visual element to display in the preview pane.
        /// </summary>
        FrameworkElement View { get; }

        /// <summary>
        /// The file path currently being previewed.
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// The type of preview (e.g., "Text", "Image", "Native", "PDF").
        /// </summary>
        string PreviewType { get; }

        /// <summary>
        /// Whether this preview supports text highlighting.
        /// </summary>
        bool SupportsHighlighting { get; }

        /// <summary>
        /// Whether this preview supports text selection for "Add to Task".
        /// </summary>
        bool SupportsSelection { get; }

        /// <summary>
        /// Highlight all occurrences of the given text.
        /// </summary>
        /// <param name="text">The text to highlight.</param>
        void Highlight(string text);

        /// <summary>
        /// Scroll to the specified line number (1-indexed).
        /// </summary>
        /// <param name="line">The line number to scroll to.</param>
        void ScrollToLine(int line);

        /// <summary>
        /// Get the currently selected text (if any).
        /// </summary>
        /// <returns>The selected text or null.</returns>
        string GetSelectedText();

        /// <summary>
        /// Get the current selection context for "Add to Task".
        /// </summary>
        /// <returns>A SelectionContext object or null if no selection.</returns>
        OfflineProjectManager.Models.SelectionContext GetSelectionContext();
    }
}
