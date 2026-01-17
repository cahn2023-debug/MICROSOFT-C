using System;
using System.IO;
using System.Text;
using NPOI.HWPF;
using NPOI.HWPF.UserModel;
using OfflineProjectManager.Services;

namespace OfflineProjectManager.Features.Preview.Providers
{
    /// <summary>
    /// Converts Word .doc (97-2003) files to HTML using ScratchPad.NPOI.HWPF
    /// </summary>
    public class DocToHtmlConverter
    {
        public string ConvertToHtml(string docPath)
        {
            if (!File.Exists(docPath))
                throw new FileNotFoundException("Word document not found", docPath);

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<title>Word Document Preview (.doc)</title>");
            html.AppendLine("<style>");
            html.AppendLine(GetWordStyles());
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<div class='word-document'>");

            try
            {
                using (var fileStream = File.OpenRead(docPath))
                {
                    var doc = new HWPFDocument(fileStream);
                    var range = doc.GetRange();

                    // Extract paragraphs
                    for (int i = 0; i < range.NumParagraphs; i++)
                    {
                        var paragraph = range.GetParagraph(i);
                        html.AppendLine(ConvertParagraph(paragraph));
                    }
                }
            }
            catch (Exception ex)
            {
                html.AppendLine($"<p class='error'>Error reading document: {HtmlEncode(ex.Message)}</p>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string ConvertParagraph(Paragraph paragraph)
        {
            var html = new StringBuilder();
            var text = new StringBuilder();

            // Extract text from all character runs in the paragraph
            for (int i = 0; i < paragraph.NumCharacterRuns; i++)
            {
                var run = paragraph.GetCharacterRun(i);
                var runText = run.Text;

                if (string.IsNullOrEmpty(runText))
                    continue;

                // Apply formatting
                var formatted = runText;

                if (run.IsBold())
                {
                    formatted = $"<strong>{HtmlEncode(formatted)}</strong>";
                }
                else if (run.IsItalic())
                {
                    formatted = $"<em>{HtmlEncode(formatted)}</em>";
                }
                else
                {
                    formatted = HtmlEncode(formatted);
                }

                text.Append(formatted);
            }

            var paragraphText = text.ToString().Trim();

            if (string.IsNullOrEmpty(paragraphText))
            {
                return "<p>&nbsp;</p>"; // Empty paragraph
            }

            // Determine if it's a heading based on font size or style
            // For simplicity, we'll use <p> for all paragraphs
            // You can enhance this to detect headings
            html.AppendLine($"<p>{paragraphText}</p>");

            return html.ToString();
        }

        private static string GetWordStyles()
        {
            bool isDark = ThemeService.IsDarkMode;

            string bodyBg = isDark ? "#1e1e1e" : "#f5f5f5";
            string bodyColor = isDark ? "#cccccc" : "#333333";
            string docBg = isDark ? "#252526" : "#ffffff";
            string docShadow = isDark ? "0 4px 6px rgba(0,0,0,0.3)" : "0 2px 8px rgba(0,0,0,0.1)";
            string headingColor = isDark ? "#007acc" : "#1565c0";
            string strongColor = isDark ? "#dcdcdc" : "#222222";
            string errorColor = isDark ? "#f48771" : "#d32f2f";
            string errorBg = isDark ? "#5a1d1d" : "#ffebee";

            return $@"
                body {{
                    font-family: 'Segoe UI', Calibri, Arial, sans-serif;
                    background-color: {bodyBg};
                    color: {bodyColor};
                    margin: 0;
                    padding: 20px;
                    line-height: 1.6;
                }}
                .word-document {{
                    max-width: 800px;
                    margin: 0 auto;
                    background-color: {docBg};
                    padding: 40px;
                    border-radius: 8px;
                    box-shadow: {docShadow};
                }}
                h1, h2, h3, h4, h5, h6 {{
                    color: {headingColor};
                    margin-top: 24px;
                    margin-bottom: 12px;
                }}
                h1 {{ font-size: 2em; }}
                h2 {{ font-size: 1.5em; }}
                h3 {{ font-size: 1.25em; }}
                p {{
                    margin: 8px 0;
                    color: {bodyColor};
                }}
                strong {{
                    font-weight: bold;
                    color: {strongColor};
                }}
                em {{
                    font-style: italic;
                }}
                .error {{
                    color: {errorColor};
                    background-color: {errorBg};
                    padding: 12px;
                    border-radius: 4px;
                    border-left: 4px solid {errorColor};
                }}
            ";
        }

        private static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }
}
