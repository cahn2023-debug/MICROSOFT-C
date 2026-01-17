using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OfflineProjectManager.Services.FileParsers
{
    public class TextFileParser : IFileParser
    {
        private static readonly string[] Supported = new[]
        {
            ".txt", ".log", ".md", ".json", ".xml", ".py", ".cs", ".js"
        };

        public bool CanParse(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return false;
            extension = extension.ToLowerInvariant();
            foreach (var ext in Supported)
            {
                if (ext == extension) return true;
            }
            return false;
        }

        public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var doc = new ParsedDocument();
            if (!File.Exists(filePath)) return doc;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                cancellationToken.ThrowIfCancellationRequested();
                doc.Text = await reader.ReadToEndAsync();
            }

            return doc;
        }
    }
}
