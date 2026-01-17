using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NPOI.XWPF.UserModel;

namespace OfflineProjectManager.Services.FileParsers
{
    /// <summary>
    /// Parser for .docx files (Word 2007+)
    /// Uses NPOI XWPF with streaming to extract text/tables without Office dependency
    /// </summary>
    public class DocxFileParser : IFileParser
    {
        private const int TIMEOUT_SECONDS = 15;

        public bool CanParse(string extension) => extension != null && extension.ToLowerInvariant() == ".docx";

        public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var doc = new ParsedDocument();
            if (!File.Exists(filePath)) return doc;

            try
            {
                // CRITICAL: Use streaming to avoid loading entire file into memory at once
                // Use background thread to keep UI responsive
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

                    using var bs = new BufferedStream(fs, 8192);

                    // Word .docx
                    var xwpfDoc = new XWPFDocument(bs);
                    var sb = new StringBuilder();

                    // Extract paragraphs
                    foreach (var para in xwpfDoc.Paragraphs)
                    {
                        token.ThrowIfCancellationRequested();
                        var text = para.ParagraphText;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            sb.AppendLine(text.Trim());
                        }
                    }

                    // Extract tables (simple conversion to text)
                    foreach (var table in xwpfDoc.Tables)
                    {
                        token.ThrowIfCancellationRequested();
                        sb.AppendLine("\n[Table]");
                        foreach (var row in table.Rows)
                        {
                            bool firstCell = true;
                            foreach (var cell in row.GetTableCells())
                            {
                                if (!firstCell) sb.Append(" | ");
                                sb.Append(cell.GetText());
                                firstCell = false;
                            }
                            sb.AppendLine();
                        }
                        sb.AppendLine();
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
                doc.Text = $"⚠️ Error reading .docx file: {ex.Message}";
            }

            return doc;
        }
    }
}
