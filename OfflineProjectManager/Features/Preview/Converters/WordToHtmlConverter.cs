using System;
using System.IO;
using Mammoth;
using OfflineProjectManager.Services;

namespace OfflineProjectManager.Features.Preview.Providers
{
    /// <summary>
    /// Converts Word documents (.docx) to HTML using Mammoth library
    /// Mammoth provides better HTML conversion with cleaner output
    /// </summary>
    public class WordToHtmlConverter
    {
        public string ConvertToHtml(string docxFilePath)
        {
            if (!File.Exists(docxFilePath))
                throw new FileNotFoundException("Word document not found", docxFilePath);

            // Check if it's an old .doc file (not supported by Mammoth)
            var extension = Path.GetExtension(docxFilePath).ToLower();
            if (extension == ".doc")
            {
                return GetUnsupportedDocFormatHtml(docxFilePath);
            }

            var html = new System.Text.StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<title>Word Document Preview</title>");
            html.AppendLine("<style>");
            html.AppendLine(GetWordStyles());
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<div class='word-document'>");

            try
            {
                // Use Mammoth to convert DOCX to HTML
                var converter = new DocumentConverter();

                using (var fileStream = File.OpenRead(docxFilePath))
                {
                    // Convert to HTML
                    var result = converter.ConvertToHtml(fileStream);

                    // Get the HTML content
                    var convertedHtml = result.Value;

                    // Append the converted HTML
                    html.AppendLine(convertedHtml);

                    // Log any warnings from Mammoth
                    if (result.Warnings.Count > 0)
                    {
                        html.AppendLine("<div class='warnings'>");
                        html.AppendLine("<h4>⚠️ Conversion Warnings:</h4>");
                        html.AppendLine("<ul>");
                        foreach (var warning in result.Warnings)
                        {
                            html.AppendLine($"<li>{HtmlEncode(warning)}</li>");
                        }
                        html.AppendLine("</ul>");
                        html.AppendLine("</div>");
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

        /// <summary>
        /// Convert with images embedded as base64
        /// </summary>
        public string ConvertToHtmlWithBase64Images(string docxFilePath)
        {
            if (!File.Exists(docxFilePath))
                throw new FileNotFoundException("Word document not found", docxFilePath);

            var extension = Path.GetExtension(docxFilePath).ToLower();
            if (extension == ".doc")
            {
                return GetUnsupportedDocFormatHtml(docxFilePath);
            }

            var html = new System.Text.StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<title>Word Document Preview</title>");
            html.AppendLine("<style>");
            html.AppendLine(GetWordStyles());
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<div class='word-document'>");

            try
            {
                var converter = new DocumentConverter();

                using (var fileStream = File.OpenRead(docxFilePath))
                {
                    // Mammoth 1.8.0 automatically embeds images as base64 by default
                    var result = converter.ConvertToHtml(fileStream);

                    html.AppendLine(result.Value);

                    // Log warnings if any
                    if (result.Warnings.Count > 0)
                    {
                        html.AppendLine("<div class='warnings'>");
                        html.AppendLine("<h4>⚠️ Conversion Warnings:</h4>");
                        html.AppendLine("<ul>");
                        foreach (var warning in result.Warnings)
                        {
                            html.AppendLine($"<li>{HtmlEncode(warning)}</li>");
                        }
                        html.AppendLine("</ul>");
                        html.AppendLine("</div>");
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

        private string GetUnsupportedDocFormatHtml(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Unsupported Format</title>
    <style>
        {GetWordStyles()}
    </style>
</head>
<body>
    <div class='word-document' style='text-align: center; padding: 60px 40px;'>
        <div style='font-size: 64px; margin-bottom: 20px;'>⚠️</div>
        <h1 style='color: #f48771; margin-bottom: 20px;'>Định dạng .DOC không được hỗ trợ</h1>
        <p style='font-size: 16px; line-height: 1.8; margin-bottom: 30px;'>
            File <strong>{HtmlEncode(fileName)}</strong> sử dụng định dạng Word cũ (.doc) từ Office 97-2003.
        </p>
        <div style='background-color: #1a472a; padding: 20px; border-radius: 8px; margin: 20px 0;'>
            <p style='margin: 0; font-size: 14px;'>
                💡 <strong>Giải pháp:</strong> Mở file trong Microsoft Word và lưu lại dưới dạng <strong>.docx</strong> (Word 2007+)
            </p>
        </div>
        <p style='font-size: 13px; color: #888888; margin-top: 30px;'>
            Mammoth library chỉ hỗ trợ định dạng .docx (Office Open XML).
        </p>
    </div>
</body>
</html>";
        }

        private static string GetWordStyles()
        {
            bool isDark = ThemeService.IsDarkMode;

            string bodyBg = isDark ? "#1e1e1e" : "#f5f5f5";
            string bodyColor = isDark ? "#cccccc" : "#333333";
            string docBg = isDark ? "#252526" : "#ffffff";
            string docShadow = isDark ? "0 4px 6px rgba(0,0,0,0.3)" : "0 2px 8px rgba(0,0,0,0.1)";
            string headingColor = isDark ? "#007acc" : "#1565c0";
            string tableBorder = isDark ? "#3e3e42" : "#e0e0e0";
            string evenRowBg = isDark ? "#2d2d30" : "#f9f9f9";
            string strongColor = isDark ? "#dcdcdc" : "#222222";
            string errorColor = isDark ? "#f48771" : "#d32f2f";
            string errorBg = isDark ? "#5a1d1d" : "#ffebee";
            string warnBg = isDark ? "#3d3d1d" : "#fff3e0";
            string warnBorder = isDark ? "#f4c771" : "#ff9800";
            string warnHeading = isDark ? "#f4c771" : "#e65100";

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
                img {{
                    max-width: 100%;
                    height: auto;
                    display: block;
                    margin: 16px auto;
                    border-radius: 4px;
                    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
                }}
                table {{
                    width: 100%;
                    border-collapse: collapse;
                    margin: 16px 0;
                    background-color: {docBg};
                }}
                table td, table th {{
                    border: 1px solid {tableBorder};
                    padding: 8px;
                    color: {bodyColor};
                }}
                table tr:nth-child(even) {{
                    background-color: {evenRowBg};
                }}
                .error {{
                    color: {errorColor};
                    background-color: {errorBg};
                    padding: 12px;
                    border-radius: 4px;
                    border-left: 4px solid {errorColor};
                }}
                .warnings {{
                    background-color: {warnBg};
                    padding: 12px;
                    border-radius: 4px;
                    border-left: 4px solid {warnBorder};
                    margin-top: 20px;
                }}
                .warnings h4 {{
                    color: {warnHeading};
                    margin-top: 0;
                }}
                .warnings ul {{
                    margin: 8px 0;
                    padding-left: 20px;
                }}
                .warnings li {{
                    color: {bodyColor};
                    font-size: 13px;
                }}
                strong {{
                    font-weight: bold;
                    color: {strongColor};
                }}
                em {{
                    font-style: italic;
                }}
                u {{
                    text-decoration: underline;
                }}
            ";
        }

        // Simple HTML encoding without System.Web dependency
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
