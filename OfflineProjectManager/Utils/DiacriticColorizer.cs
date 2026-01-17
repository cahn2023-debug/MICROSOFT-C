using System;
using System.Collections.Generic;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using OfflineProjectManager.Utils;

namespace OfflineProjectManager.Utils
{
    public class DiacriticColorizer : DocumentColorizingTransformer
    {
        private string _keyword;
        private string _keywordNoAccent;

        public DiacriticColorizer(string keyword)
        {
            _keyword = keyword;
            if (!string.IsNullOrEmpty(keyword))
            {
                _keywordNoAccent = VietnameseTextHelper.RemoveAccents(keyword);
            }
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (string.IsNullOrEmpty(_keywordNoAccent)) return;

            int lineStartOffset = line.Offset;
            string text = CurrentContext.Document.GetText(line);
            string textNoAccent = VietnameseTextHelper.RemoveAccents(text);

            int index = 0;
            while ((index = textNoAccent.IndexOf(_keywordNoAccent, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                // Note: This simple mapping assumes 1-to-1 character length mapping 
                // between NFC and NoAccent for highlighting purposes, 
                // which is generally true for Vietnamese base characters.
                int start = lineStartOffset + index;
                int end = start + _keywordNoAccent.Length;

                ChangeLinePart(start, end, element =>
                {
                    element.BackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 255, 0)); // Light Yellow
                });

                index += _keywordNoAccent.Length;
            }
        }
    }
}
