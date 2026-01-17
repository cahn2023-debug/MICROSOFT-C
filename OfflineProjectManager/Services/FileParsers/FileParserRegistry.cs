using System;
using System.Collections.Generic;

namespace OfflineProjectManager.Services.FileParsers
{
    public interface IFileParserRegistry
    {
        IFileParser GetParserForPath(string filePath);
    }

    public class FileParserRegistry : IFileParserRegistry
    {
        private readonly List<IFileParser> _parsers;

        public FileParserRegistry(IEnumerable<IFileParser> parsers)
        {
            _parsers = new List<IFileParser>(parsers ?? Array.Empty<IFileParser>());
        }

        public IFileParser GetParserForPath(string filePath)
        {
            var ext = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant();
            foreach (var parser in _parsers)
            {
                if (parser.CanParse(ext)) return parser;
            }
            return null;
        }
    }
}
