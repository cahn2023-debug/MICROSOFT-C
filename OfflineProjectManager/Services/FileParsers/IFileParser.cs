using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OfflineProjectManager.Services.FileParsers
{
    public class ParsedDocument
    {
        public string Text { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public interface IFileParser
    {
        bool CanParse(string extension);
        Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
