using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace OfflineProjectManager.Services.FileParsers
{
    public class PdfFileParser : IFileParser
    {
        public bool CanParse(string extension) => extension != null && extension.ToLowerInvariant() == ".pdf";

        public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var doc = new ParsedDocument();
            if (!File.Exists(filePath)) return Task.FromResult(doc);

            cancellationToken.ThrowIfCancellationRequested();

            using (var pdf = PdfDocument.Open(filePath))
            {
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sb.AppendLine(page.Text);
                }
                doc.Text = sb.ToString();
            }

            return Task.FromResult(doc);
        }
    }
}
