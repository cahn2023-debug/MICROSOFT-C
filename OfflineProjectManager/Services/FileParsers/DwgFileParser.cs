using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OfflineProjectManager.Services.FileParsers
{
    public class DwgFileParser : IFileParser
    {
        public bool CanParse(string extension) => extension != null && extension.ToLowerInvariant() == ".dwg";

        public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var doc = new ParsedDocument();
            if (!File.Exists(filePath)) return Task.FromResult(doc);
            var info = new FileInfo(filePath);
            doc.Metadata["name"] = info.Name;
            doc.Metadata["size"] = info.Length.ToString();
            doc.Metadata["lastWriteTimeUtc"] = info.LastWriteTimeUtc.ToString("O");
            doc.Text = string.Empty;
            return Task.FromResult(doc);
        }
    }
}
