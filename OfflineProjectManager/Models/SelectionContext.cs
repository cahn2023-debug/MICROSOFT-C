using System;
using System.Text.Json;

namespace OfflineProjectManager.Models
{
    public class SelectionContext
    {
        public string FilePath { get; set; }
        public string PreviewType { get; set; } // "Text", "Image", "Excel", "Pdf"

        // Common
        public string SelectedText { get; set; }

        // Text / Code
        public int LineNumber { get; set; }
        public int SelectionStart { get; set; }
        public int SelectionLength { get; set; }

        // Excel
        public string SheetName { get; set; }
        public string CellRange { get; set; }

        // Image / PDF
        public double RectX { get; set; }
        public double RectY { get; set; }
        public double RectWidth { get; set; }
        public double RectHeight { get; set; }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }

        public static SelectionContext FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<SelectionContext>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
