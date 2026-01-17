using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;
using OfflineProjectManager.Models;
using OfflineProjectManager.Services;

// Explicit alias for WPF types to avoid conflicts with System.Drawing
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfColor = System.Windows.Media.Color;
using WpfSize = System.Windows.Size;
using WpfBrush = System.Windows.Media.SolidColorBrush;

namespace OfflineProjectManager.Features.Preview.Controls
{
    /// <summary>
    /// Preview control for text/code files using AvalonEdit.
    /// Supports syntax highlighting, search highlighting, and text selection.
    /// </summary>
    public class TextPreviewControl : IPreviewControl
    {
        private readonly TextEditor _editor;
        private readonly string _filePath;
        private SearchHighlightRenderer _highlighter;
        private bool _disposed;

        public FrameworkElement View => _editor;
        public string FilePath => _filePath;
        public string PreviewType => "Text";
        public bool SupportsHighlighting => true;
        public bool SupportsSelection => true;

        public TextPreviewControl(string filePath, string highlightKeyword = null)
        {
            _filePath = filePath;

            _editor = new TextEditor
            {
                IsReadOnly = true,
                ShowLineNumbers = true,
                FontFamily = new WpfFontFamily("Consolas"),
                FontSize = 13,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            try
            {
                _editor.Load(filePath);
                ApplySyntaxHighlighting(filePath);

                if (!string.IsNullOrEmpty(highlightKeyword))
                {
                    Highlight(highlightKeyword);
                }
            }
            catch (Exception ex)
            {
                _editor.Text = $"Error loading file: {ex.Message}";
            }
        }

        private void ApplySyntaxHighlighting(string filePath)
        {
            string ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            var definition = ext switch
            {
                ".cs" => HighlightingManager.Instance.GetDefinition("C#"),
                ".xml" or ".xaml" or ".csproj" or ".sln" => HighlightingManager.Instance.GetDefinition("XML"),
                ".js" or ".ts" or ".jsx" or ".tsx" => HighlightingManager.Instance.GetDefinition("JavaScript"),
                ".json" => HighlightingManager.Instance.GetDefinition("Json"),
                ".html" or ".htm" => HighlightingManager.Instance.GetDefinition("HTML"),
                ".css" or ".scss" or ".less" => HighlightingManager.Instance.GetDefinition("CSS"),
                ".sql" => HighlightingManager.Instance.GetDefinition("TSQL"),
                ".py" => HighlightingManager.Instance.GetDefinition("Python"),
                ".java" => HighlightingManager.Instance.GetDefinition("Java"),
                ".cpp" or ".c" or ".h" or ".hpp" => HighlightingManager.Instance.GetDefinition("C++"),
                ".php" => HighlightingManager.Instance.GetDefinition("PHP"),
                ".vb" => HighlightingManager.Instance.GetDefinition("VB"),
                _ => null
            };

            if (definition != null)
            {
                _editor.SyntaxHighlighting = definition;
            }
        }

        public void Highlight(string text)
        {
            if (string.IsNullOrEmpty(text) || _disposed) return;

            SearchPanel.Install(_editor);

            _highlighter = new SearchHighlightRenderer(_editor, text);
            _editor.TextArea.TextView.BackgroundRenderers.Add(_highlighter);

            int firstMatchOffset = _highlighter.GetFirstMatchOffset();
            if (firstMatchOffset >= 0)
            {
                // Defer scrolling until layout is complete
                _editor.Dispatcher.InvokeAsync(() =>
                {
                    var location = _editor.Document.GetLocation(firstMatchOffset);
                    _editor.ScrollTo(location.Line, location.Column);
                    _editor.Select(firstMatchOffset, text.Length);
                    _editor.TextArea.Caret.BringCaretToView();
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        public void ScrollToLine(int line)
        {
            if (_disposed) return;
            _editor.Dispatcher.InvokeAsync(() =>
            {
                _editor.ScrollTo(line, 0);
                _editor.TextArea.Caret.Line = line;
                _editor.TextArea.Caret.BringCaretToView();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        public string GetSelectedText()
        {
            if (_disposed) return null;
            return _editor.SelectionLength > 0 ? _editor.SelectedText : null;
        }

        public SelectionContext GetSelectionContext()
        {
            if (_disposed || _editor.SelectionLength == 0) return null;

            var location = _editor.Document.GetLocation(_editor.SelectionStart);
            return new SelectionContext
            {
                FilePath = _filePath,
                PreviewType = PreviewType,
                SelectedText = _editor.SelectedText,
                SelectionStart = _editor.SelectionStart,
                SelectionLength = _editor.SelectionLength,
                LineNumber = location.Line
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Remove highlight renderer
            if (_highlighter != null)
            {
                _editor.TextArea.TextView.BackgroundRenderers.Remove(_highlighter);
                _highlighter = null;
            }
        }
    }

    /// <summary>
    /// Custom background renderer to highlight all search matches with yellow background.
    /// Supports Vietnamese accent-insensitive matching with proper offset mapping.
    /// </summary>
    internal class SearchHighlightRenderer : IBackgroundRenderer
    {
        private readonly TextEditor _editor;
        private readonly string _keyword;
        private readonly List<(int Offset, int Length)> _matches = new();

        public SearchHighlightRenderer(TextEditor editor, string keyword)
        {
            _editor = editor;
            _keyword = keyword;
            FindAllMatches();
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public int GetFirstMatchOffset() => _matches.Count > 0 ? _matches[0].Offset : -1;

        private void FindAllMatches()
        {
            if (string.IsNullOrEmpty(_keyword)) return;

            string text = _editor.Text;
            if (string.IsNullOrEmpty(text)) return;

            // Build normalized text and index map (normalized index -> original index)
            var (normalizedText, indexMap) = BuildNormalizedTextAndMap(text);
            string normalizedKeyword = RemoveVietnameseAccents(_keyword.ToLowerInvariant());

            int searchStart = 0;
            while (true)
            {
                int normalizedIdx = normalizedText.IndexOf(normalizedKeyword, searchStart, StringComparison.Ordinal);
                if (normalizedIdx < 0) break;

                // Map normalized index back to original text index
                int originalStart = indexMap[normalizedIdx];

                // Find the end in original text by mapping the normalized end position
                int normalizedEnd = normalizedIdx + normalizedKeyword.Length;
                int originalEnd = normalizedEnd < indexMap.Count ? indexMap[normalizedEnd] : text.Length;
                int originalLength = originalEnd - originalStart;

                // Ensure we don't exceed document bounds
                if (originalStart + originalLength <= text.Length && originalLength > 0)
                {
                    _matches.Add((originalStart, originalLength));
                }

                searchStart = normalizedIdx + 1;
                if (searchStart >= normalizedText.Length) break;
            }

            System.Diagnostics.Debug.WriteLine($"[SearchHighlightRenderer] Found {_matches.Count} matches for '{_keyword}'");
        }

        /// <summary>
        /// Builds a normalized (accent-removed, lowercase) version of text along with an index map.
        /// indexMap[i] gives the original text index that corresponds to normalized text position i.
        /// </summary>
        private static (string NormalizedText, List<int> IndexMap) BuildNormalizedTextAndMap(string originalText)
        {
            var normalized = new StringBuilder();
            var indexMap = new List<int>();

            // FIX: Iterate over original text characters to correctly track indices
            for (int originalIdx = 0; originalIdx < originalText.Length; originalIdx++)
            {
                char c = originalText[originalIdx];

                // Decompose single character to handle combining marks
                string decomposed = c.ToString().Normalize(NormalizationForm.FormD);

                // Extract only the base character (skip combining marks)
                bool addedBase = false;
                foreach (char dc in decomposed)
                {
                    var category = CharUnicodeInfo.GetUnicodeCategory(dc);
                    if (category != UnicodeCategory.NonSpacingMark && !addedBase)
                    {
                        char lower = char.ToLowerInvariant(dc);

                        // Handle đ/Đ special case
                        if (lower == 'đ') lower = 'd';

                        normalized.Append(lower);
                        indexMap.Add(originalIdx);  // Map to original string index
                        addedBase = true;
                    }
                }
            }

            // Add sentinel for end-of-string mapping
            indexMap.Add(originalText.Length);

            return (normalized.ToString(), indexMap);
        }

        private static string RemoveVietnameseAccents(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString()
                .Replace('đ', 'd')
                .Replace('Đ', 'D');
        }

        public void Draw(TextView textView, System.Windows.Media.DrawingContext drawingContext)
        {
            if (_matches.Count == 0) return;

            var highlightBrush = new WpfBrush(WpfColor.FromRgb(255, 235, 59)); // Yellow #FFEB3B
            var firstMatchBrush = new WpfBrush(WpfColor.FromRgb(255, 152, 0)); // Orange #FF9800 for first match
            highlightBrush.Freeze();
            firstMatchBrush.Freeze();

            for (int i = 0; i < _matches.Count; i++)
            {
                var (offset, length) = _matches[i];
                int endOffset = offset + length;
                if (endOffset > _editor.Document.TextLength) continue;

                var segment = new TextSegment { StartOffset = offset, EndOffset = endOffset };
                var brush = i == 0 ? firstMatchBrush : highlightBrush;

                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    drawingContext.DrawRectangle(brush, null, new Rect(rect.Location, new WpfSize(rect.Width, rect.Height)));
                }
            }
        }
    }
}
