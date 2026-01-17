using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SysTask = System.Threading.Tasks.Task;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Spreadsheet;

namespace OfflineProjectManager.Features.Preview.Extraction
{
    /// <summary>
    /// Represents an extracted text block from an Office document
    /// </summary>
    public class ExtractedTextBlock
    {
        public string Text { get; set; }
        public int Index { get; set; }
        public string Type { get; set; } // "Paragraph", "Cell", "Slide"
        public string SheetName { get; set; } // For Excel
        public int? Row { get; set; } // For Excel
        public int? Column { get; set; } // For Excel
        public int? SlideNumber { get; set; } // For PowerPoint
    }

    /// <summary>
    /// Service for extracting text from Office documents using OpenXML SDK.
    /// Used by OfficeTextPreviewProvider to enable search highlight.
    /// </summary>
    public class OfficeTextExtractor
    {
        /// <summary>
        /// Extract text blocks from a Word document (.docx)
        /// </summary>
        public async System.Threading.Tasks.Task<List<ExtractedTextBlock>> ExtractWordAsync(string filePath)
        {
            return await SysTask.Run(() =>
            {
                var blocks = new List<ExtractedTextBlock>();

                try
                {
                    using var doc = WordprocessingDocument.Open(filePath, false);
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body == null) return blocks;

                    int index = 0;
                    foreach (var para in body.Elements<Paragraph>())
                    {
                        var text = para.InnerText;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            blocks.Add(new ExtractedTextBlock
                            {
                                Text = text,
                                Index = index,
                                Type = "Paragraph"
                            });
                        }
                        index++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OfficeTextExtractor] Word extraction error: {ex.Message}");
                }

                return blocks;
            });
        }

        /// <summary>
        /// Extract text blocks from an Excel spreadsheet (.xlsx)
        /// </summary>
        public async System.Threading.Tasks.Task<List<ExtractedTextBlock>> ExtractExcelAsync(string filePath)
        {
            return await SysTask.Run(() =>
            {
                var blocks = new List<ExtractedTextBlock>();

                try
                {
                    using var doc = SpreadsheetDocument.Open(filePath, false);
                    var workbookPart = doc.WorkbookPart;
                    if (workbookPart == null) return blocks;

                    var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

                    int blockIndex = 0;
                    foreach (var worksheetPart in workbookPart.WorksheetParts)
                    {
                        var sheetData = worksheetPart.Worksheet?.GetFirstChild<SheetData>();
                        if (sheetData == null) continue;

                        // Get sheet name
                        var sheetId = workbookPart.GetIdOfPart(worksheetPart);
                        var sheet = workbookPart.Workbook.Sheets?.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>()
                            .FirstOrDefault(s => s.Id == sheetId);
                        var sheetName = sheet?.Name ?? "Sheet";

                        foreach (var row in sheetData.Elements<DocumentFormat.OpenXml.Spreadsheet.Row>())
                        {
                            foreach (var cell in row.Elements<DocumentFormat.OpenXml.Spreadsheet.Cell>())
                            {
                                var cellValue = GetCellValue(cell, sharedStrings);
                                if (!string.IsNullOrWhiteSpace(cellValue))
                                {
                                    var cellRef = cell.CellReference?.Value ?? "";
                                    var (colNum, rowNum) = ParseCellReference(cellRef);

                                    blocks.Add(new ExtractedTextBlock
                                    {
                                        Text = cellValue,
                                        Index = blockIndex++,
                                        Type = "Cell",
                                        SheetName = sheetName,
                                        Row = rowNum,
                                        Column = colNum
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OfficeTextExtractor] Excel extraction error: {ex.Message}");
                }

                return blocks;
            });
        }

        /// <summary>
        /// Extract text blocks from a PowerPoint presentation (.pptx)
        /// </summary>
        public async System.Threading.Tasks.Task<List<ExtractedTextBlock>> ExtractPowerPointAsync(string filePath)
        {
            return await SysTask.Run(() =>
            {
                var blocks = new List<ExtractedTextBlock>();

                try
                {
                    using var doc = PresentationDocument.Open(filePath, false);
                    var presentationPart = doc.PresentationPart;
                    if (presentationPart == null) return blocks;

                    int slideNumber = 1;
                    int blockIndex = 0;

                    foreach (var slidePart in presentationPart.SlideParts)
                    {
                        var slide = slidePart.Slide;
                        if (slide == null) continue;

                        // Extract text from all shapes
                        var texts = slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>()
                            .Select(t => t.Text)
                            .Where(t => !string.IsNullOrWhiteSpace(t));

                        foreach (var text in texts)
                        {
                            blocks.Add(new ExtractedTextBlock
                            {
                                Text = text,
                                Index = blockIndex++,
                                Type = "Slide",
                                SlideNumber = slideNumber
                            });
                        }

                        slideNumber++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OfficeTextExtractor] PowerPoint extraction error: {ex.Message}");
                }

                return blocks;
            });
        }

        /// <summary>
        /// Extract text from any Office file based on extension
        /// </summary>
        public async System.Threading.Tasks.Task<List<ExtractedTextBlock>> ExtractAsync(string filePath)
        {
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();

            return ext switch
            {
                ".docx" or ".docm" => await ExtractWordAsync(filePath),
                ".xlsx" or ".xlsm" => await ExtractExcelAsync(filePath),
                ".pptx" or ".pptm" => await ExtractPowerPointAsync(filePath),
                _ => new List<ExtractedTextBlock>()
            };
        }

        /// <summary>
        /// Get combined text from all blocks
        /// </summary>
        public static string CombineText(List<ExtractedTextBlock> blocks)
        {
            return string.Join("\n\n", blocks.Select(b => b.Text));
        }

        private static string GetCellValue(DocumentFormat.OpenXml.Spreadsheet.Cell cell, SharedStringTable sharedStrings)
        {
            if (cell.CellValue == null) return null;

            var value = cell.CellValue.InnerText;

            if (cell.DataType?.Value == CellValues.SharedString && sharedStrings != null)
            {
                if (int.TryParse(value, out int index) && index < sharedStrings.Count())
                {
                    return sharedStrings.ElementAt(index).InnerText;
                }
            }

            return value;
        }

        private static (int column, int row) ParseCellReference(string cellRef)
        {
            int column = 0;
            int row = 0;
            int i = 0;

            // Parse column letters
            while (i < cellRef.Length && char.IsLetter(cellRef[i]))
            {
                column = column * 26 + (char.ToUpper(cellRef[i]) - 'A' + 1);
                i++;
            }

            // Parse row number
            if (i < cellRef.Length)
            {
                int.TryParse(cellRef.Substring(i), out row);
            }

            return (column, row);
        }
    }
}
