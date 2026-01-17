using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NPOI.SS.UserModel;

namespace OfflineProjectManager.Services.FileParsers
{
    /// <summary>
    /// Parser for Excel files (.xls, .xlsx)
    /// Uses NPOI SS with streaming to extract text from the first sheet
    /// </summary>
    public class ExcelFileParser : IFileParser
    {
        private const int TIMEOUT_SECONDS = 15;
        private const int MAX_ROWS = 500; // Lazy-like loading limit for preview

        public bool CanParse(string extension)
        {
            if (extension == null) return false;
            extension = extension.ToLowerInvariant();
            return extension == ".xlsx" || extension == ".xls";
        }

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

                    // Excel .xls / .xlsx
                    IWorkbook workbook = WorkbookFactory.Create(bs);

                    // CRITICAL FIX: Validate sheet exists before accessing
                    if (workbook.NumberOfSheets <= 0)
                        return "[Excel file has no sheets]>";

                    ISheet sheet = workbook.GetSheetAt(0); // Load first sheet

                    var sb = new StringBuilder();
                    sb.AppendLine($"[Sheet] {sheet.SheetName}");

                    for (int i = 0; i <= Math.Min(sheet.LastRowNum, MAX_ROWS); i++)
                    {
                        token.ThrowIfCancellationRequested();
                        IRow row = sheet.GetRow(i);
                        if (row == null) continue;

                        bool firstCell = true;
                        for (int j = 0; j < row.LastCellNum; j++)
                        {
                            ICell cell = row.GetCell(j);
                            string value = string.Empty;

                            if (cell != null)
                            {
                                try
                                {
                                    DataFormatter formatter = new DataFormatter();
                                    value = formatter.FormatCellValue(cell);
                                }
                                catch { value = cell.ToString(); }
                            }

                            if (!firstCell) sb.Append('\t');
                            sb.Append(value);
                            firstCell = false;
                        }
                        sb.AppendLine();
                    }

                    if (sheet.LastRowNum > MAX_ROWS)
                    {
                        sb.AppendLine($"\n... (Truncated at {MAX_ROWS} rows for preview)");
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
                doc.Text = $"⚠️ Error reading Excel file: {ex.Message}";
            }

            return doc;
        }
    }
}
