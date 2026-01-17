using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OfflineProjectManager.Services.FileParsers
{
    /// <summary>
    /// Parser for legacy .doc files (Word 97-2003 binary format)
    /// Extracts text content using binary reading (NPOI for .NET Core lacks HWPF support)
    /// </summary>
    public class DocFileParser : IFileParser
    {
        private const int TIMEOUT_SECONDS = 15;

        public bool CanParse(string extension) => extension != null && extension.ToLowerInvariant() == ".doc";

        public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var doc = new ParsedDocument();
            if (!File.Exists(filePath)) return doc;

            try
            {
                doc.Text = await Task.Run(() =>
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_SECONDS));
                    var token = cts.Token;

                    token.ThrowIfCancellationRequested();

                    // BẮT BUỘC: Sử dụng FileStream với buffer và useAsync
                    using var fs = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,
                        bufferSize: 4096,
                        useAsync: true
                    );

                    using var reader = new BinaryReader(fs);
                    var bytes = reader.ReadBytes((int)fs.Length);
                    
                    token.ThrowIfCancellationRequested();

                    // Extract readable text from binary OLE structure
                    var sb = new StringBuilder();
                    var text = Encoding.Unicode.GetString(bytes); // .doc often uses UTF-16 LE
                    
                    // Clean text (remove control characters)
                    text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
                    var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var line in lines)
                    {
                        token.ThrowIfCancellationRequested();
                        var trimmed = line.Trim();
                        // Filter out lines that look like binary noise (require some alphanumeric content)
                        if (Regex.IsMatch(trimmed, @"[a-zA-Z0-9]{3,}"))
                        {
                            sb.AppendLine(trimmed);
                        }
                    }

                    return sb.ToString();
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                doc.Text = "⚠️ Preview timeout or cancelled.";
            }
            catch (Exception ex)
            {
                doc.Text = $"⚠️ Error reading .doc file: {ex.Message}";
            }

            return doc;
        }
    }
}
