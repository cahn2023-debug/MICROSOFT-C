using System;
using System.IO;
using System.Text;
using NPOI.XWPF.UserModel;
using NPOI.SS.UserModel;

namespace OfflineProjectManager.Services
{
    public class IndexerService : IIndexerService
    {
        public string ExtractText(string filePath)
        {
            if (!File.Exists(filePath)) return "";

            string ext = Path.GetExtension(filePath).ToLower();

            try
            {
                if (IsPlainTextFile(ext))
                {
                    return ExtractPlainText(filePath);
                }
                else if (ext == ".pdf")
                {
                    return ExtractPdf(filePath);
                }
                else if (ext == ".docx")
                {
                    return ExtractDocxNpoi(filePath);
                }
                else if (ext == ".doc")
                {
                    return ExtractDocNpoi(filePath);
                }
                else if (ext == ".xlsx" || ext == ".xls")
                {
                    return ExtractExcelNpoi(filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Indexing Error {filePath}: {ex.Message}");
                return "";
            }

            return "";
        }

        private bool IsPlainTextFile(string ext)
        {
            var plainExts = new[] { ".txt", ".md", ".py", ".cs", ".json", ".xml", ".log", ".csv", ".html", ".css", ".js" };
            return Array.Exists(plainExts, e => e == ext);
        }

        private string ExtractPlainText(string path)
        {
            return File.ReadAllText(path); 
        }

        private string ExtractPdf(string path)
        {
            try 
            {
                using (var doc = UglyToad.PdfPig.PdfDocument.Open(path))
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var page in doc.GetPages())
                    {
                        sb.AppendLine(page.Text);
                    }
                    return sb.ToString();
                }
            }
            catch { return ""; }
        }

        private string ExtractDocxNpoi(string path)
        {
            try
            {
                using var fs = CreateStream(path);
                using var bs = new BufferedStream(fs, 8192);
                var doc = new XWPFDocument(bs);
                var sb = new StringBuilder();
                foreach (var para in doc.Paragraphs)
                {
                    sb.AppendLine(para.ParagraphText);
                }
                return sb.ToString();
            }
            catch { return ""; }
        }

        private string ExtractDocNpoi(string path)
        {
            try
            {
                using var fs = CreateStream(path);
                using var reader = new BinaryReader(fs);
                var bytes = reader.ReadBytes((int)fs.Length);
                var text = Encoding.Unicode.GetString(bytes);
                text = System.Text.RegularExpressions.Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
                return text;
            }
            catch { return ""; }
        }

        private string ExtractExcelNpoi(string path)
        {
            try
            {
                using var fs = CreateStream(path);
                using var bs = new BufferedStream(fs, 8192);
                IWorkbook workbook = WorkbookFactory.Create(bs);
                StringBuilder sb = new StringBuilder();
                
                // Index all sheets for search accuracy
                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    ISheet sheet = workbook.GetSheetAt(i);
                    for (int r = 0; r <= sheet.LastRowNum; r++)
                    {
                        IRow row = sheet.GetRow(r);
                        if (row == null) continue;
                        foreach (NPOI.SS.UserModel.ICell cell in row.Cells)
                        {
                            sb.Append(cell.ToString() + " ");
                        }
                        sb.AppendLine();
                    }
                }
                return sb.ToString();
            }
            catch { return ""; }
        }

        private FileStream CreateStream(string path)
        {
            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096,
                useAsync: true
            );
        }
    }
}

