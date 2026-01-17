using System;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using OfflineProjectManager.Services;

namespace OfflineProjectManager.Features.Preview.Providers
{
    /// <summary>
    /// Converts Excel spreadsheets (.xlsx, .xls) to HTML for preview
    /// </summary>
    public class ExcelToHtmlConverter
    {
        public string ConvertToHtml(string excelFilePath)
        {
            if (!File.Exists(excelFilePath))
                throw new FileNotFoundException("Excel file not found", excelFilePath);

            // Check if it's old .xls format (not supported by ClosedXML)
            var extension = Path.GetExtension(excelFilePath).ToLower();
            if (extension == ".xls")
            {
                return GetUnsupportedXlsFormatHtml(excelFilePath);
            }

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<title>Excel Preview</title>");
            html.AppendLine("<style>");
            html.AppendLine(GetExcelStyles());
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            try
            {
                using (var workbook = new XLWorkbook(excelFilePath))
                {
                    // Excel container
                    html.AppendLine("<div class='excel-container'>");

                    // Content area (sheets)
                    html.AppendLine("<div class='excel-content'>");
                    int index = 1;
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        string displayStyle = index == 1 ? "block" : "none";
                        html.AppendLine($"<div id='sheet{index}' class='excel-sheet' style='display:{displayStyle}'>");
                        html.AppendLine(ConvertWorksheet(worksheet));
                        html.AppendLine("</div>");
                        index++;
                    }
                    html.AppendLine("</div>");

                    // Tabs at bottom (like Excel)
                    html.AppendLine("<div class='sheet-tabs-bottom'>");
                    int sheetIndex = 1;
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        string activeClass = sheetIndex == 1 ? " active" : "";
                        html.AppendLine($"<button class='sheet-tab{activeClass}' onclick='showSheet({sheetIndex})'>");
                        html.AppendLine($"<span class='tab-icon'>📄</span> {HtmlEncode(worksheet.Name)}");
                        html.AppendLine("</button>");
                        sheetIndex++;
                    }
                    html.AppendLine("</div>");

                    html.AppendLine("</div>");
                }

                // Add JavaScript for sheet switching
                html.AppendLine("<script>");
                html.AppendLine(@"
                    function showSheet(sheetNum) {
                        var sheets = document.getElementsByClassName('excel-sheet');
                        var tabs = document.getElementsByClassName('sheet-tab');
                        for (var i = 0; i < sheets.length; i++) {
                            sheets[i].style.display = 'none';
                            tabs[i].classList.remove('active');
                        }
                        document.getElementById('sheet' + sheetNum).style.display = 'block';
                        tabs[sheetNum - 1].classList.add('active');
                    }
                ");
                html.AppendLine("</script>");
            }
            catch (Exception ex)
            {
                html.AppendLine($"<div class='error'>Error reading Excel file: {HtmlEncode(ex.Message)}</div>");
            }

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private const int MAX_ROWS = 1000;
        private const int MAX_COLS = 50;

        private string ConvertWorksheet(IXLWorksheet worksheet)
        {
            var html = new StringBuilder();
            var usedRange = worksheet.RangeUsed();

            if (usedRange == null)
            {
                html.AppendLine("<p class='empty-sheet'>This sheet is empty</p>");
                return html.ToString();
            }

            html.AppendLine("<div class='table-container'>");
            html.AppendLine("<table class='excel-table'>");

            // Get the range bounds
            int firstRow = usedRange.FirstRow().RowNumber();
            int lastRow = usedRange.LastRow().RowNumber();
            int firstCol = usedRange.FirstColumn().ColumnNumber();
            int lastCol = usedRange.LastColumn().ColumnNumber();

            // Apply limits
            bool rowsTruncated = false;
            if (lastRow - firstRow + 1 > MAX_ROWS)
            {
                lastRow = firstRow + MAX_ROWS - 1;
                rowsTruncated = true;
            }

            bool colsTruncated = false;
            if (lastCol - firstCol + 1 > MAX_COLS)
            {
                lastCol = firstCol + MAX_COLS - 1;
                colsTruncated = true;
            }

            // Optimize: Iterate rows directly instead of random cell access if possible,
            // but ClosedXML Cell access is reasonably fast if we limit the range.
            for (int row = firstRow; row <= lastRow; row++)
            {
                html.AppendLine("<tr>");
                var xlRow = worksheet.Row(row); // Access row object to potentially speed up cell access

                for (int col = firstCol; col <= lastCol; col++)
                {
                    // Using worksheet.Cell(row, col) is safer for bounds, 
                    // but xlRow.Cell(col) might be faster if we trust column indices.
                    // Let's stick to safe access but limited by our loop.
                    var cell = worksheet.Cell(row, col);
                    var cellValue = cell.GetString();

                    var style = new StringBuilder();

                    // Check for bold font
                    if (cell.Style.Font.Bold)
                    {
                        style.Append("font-weight: bold; ");
                    }

                    // Determine cell class based on text length
                    string cellClass = "";
                    if (cellValue.Length > 50)
                    {
                        cellClass = " class='long-text'";
                    }
                    else if (cellValue.Length < 20)
                    {
                        cellClass = " class='short-text'";
                    }

                    string styleAttr = style.Length > 0 ? $" style='{style}'" : "";
                    html.AppendLine($"<td{cellClass}{styleAttr}>{HtmlEncode(cellValue)}</td>");
                }
                html.AppendLine("</tr>");
            }

            html.AppendLine("</table>");

            // Add warning if truncated
            if (rowsTruncated || colsTruncated)
            {
                html.AppendLine("<div class='truncation-warning'>");
                html.AppendLine($"⚠️ Preview giới hạn {MAX_ROWS} dòng và {MAX_COLS} cột để tối ưu hiệu năng. Mở file bằng Excel để xem đầy đủ.");
                html.AppendLine("</div>");
            }

            html.AppendLine("</div>");

            return html.ToString();
        }

        private string GetUnsupportedXlsFormatHtml(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Unsupported Format</title>
    <style>
        {GetExcelStyles()}
    </style>
</head>
<body>
    <div class='excel-container' style='display: flex; align-items: center; justify-content: center;'>
        <div style='text-align: center; padding: 60px 40px; max-width: 600px;'>
            <div style='font-size: 64px; margin-bottom: 20px;'>⚠️</div>
            <h1 style='color: #f48771; margin-bottom: 20px; font-size: 24px;'>Định dạng .XLS không được hỗ trợ</h1>
            <p style='font-size: 16px; line-height: 1.8; margin-bottom: 30px; color: #cccccc;'>
                File <strong>{HtmlEncode(fileName)}</strong> sử dụng định dạng Excel cũ (.xls) từ Office 97-2003.
            </p>
            <div style='background-color: #1a472a; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                <p style='margin: 0; font-size: 14px; color: #cccccc;'>
                    💡 <strong>Giải pháp:</strong> Mở file trong Microsoft Excel và lưu lại dưới dạng <strong>.xlsx</strong> (Excel 2007+)
                </p>
            </div>
            <p style='font-size: 13px; color: #888888; margin-top: 30px;'>
                ClosedXML library chỉ hỗ trợ định dạng .xlsx (Office Open XML).
            </p>
        </div>
    </div>
</body>
</html>";
        }

        private static string GetExcelStyles()
        {
            bool isDark = ThemeService.IsDarkMode;

            // Base colors based on theme
            string bodyBg = isDark ? "#1e1e1e" : "#f5f5f5";
            string bodyColor = isDark ? "#cccccc" : "#333333";
            string containerBg = isDark ? "#252526" : "#ffffff";
            string contentBg = isDark ? "#1e1e1e" : "#f5f5f5";
            string tableBg = isDark ? "#2d2d30" : "#ffffff";
            string cellBorder = isDark ? "#3e3e42" : "#e0e0e0";
            string cellColor = isDark ? "#cccccc" : "#333333";
            string headerBg = isDark ? "#1a472a" : "#2e7d32";
            string evenRowBg = isDark ? "#252526" : "#f9f9f9";
            string tabsBg = isDark ? "#2d2d30" : "#e8e8e8";
            string tabsBorder = isDark ? "#3e3e42" : "#d0d0d0";
            string tabBg = isDark ? "#3e3e42" : "#d8d8d8";
            string tabColor = isDark ? "#cccccc" : "#333333";
            string tabHoverBg = isDark ? "#4e4e52" : "#c8c8c8";
            string tabActiveBg = isDark ? "#1e1e1e" : "#ffffff";
            string tabActiveColor = isDark ? "#4ec9b0" : "#2e7d32";
            string errorColor = isDark ? "#f48771" : "#d32f2f";
            string errorBg = isDark ? "#5a1d1d" : "#ffebee";
            string warnColor = isDark ? "#cca700" : "#895d0b";
            string warnBg = isDark ? "#3e3800" : "#fff3cd";
            string warnBorder = isDark ? "#cca700" : "#ffc107";

            return $@"
                body {{
                    font-family: 'Segoe UI', Calibri, Arial, sans-serif;
                    background-color: {bodyBg};
                    color: {bodyColor};
                    margin: 0;
                    padding: 0;
                    overflow: hidden;
                }}
                .excel-container {{
                    display: flex;
                    flex-direction: column;
                    height: 100vh;
                    background-color: {containerBg};
                }}
                .excel-content {{
                    flex: 1;
                    overflow: auto;
                    background-color: {contentBg};
                }}
                .excel-sheet {{
                    padding: 20px;
                    height: 100%;
                }}
                .table-container {{
                }}
                .excel-table {{
                    border-collapse: collapse;
                    background-color: {tableBg};
                    font-size: 13px;
                    table-layout: auto;
                }}
                .excel-table td {{
                    border: 1px solid {cellBorder};
                    padding: 6px 10px;
                    color: {cellColor};
                    white-space: normal;
                    word-wrap: break-word;
                    max-width: 300px;
                    min-width: 100px;
                    vertical-align: top;
                }}
                .excel-table td.short-text {{
                    width: 120px;
                }}
                .excel-table td.long-text {{
                    width: auto;
                    min-width: 200px;
                }}
                .excel-table tr:first-child td {{
                    background-color: {headerBg};
                    font-weight: bold;
                    color: #ffffff;
                }}
                .excel-table tr:nth-child(even) td {{
                    background-color: {evenRowBg};
                }}
                .sheet-tabs-bottom {{
                    display: flex;
                    background-color: {tabsBg};
                    border-top: 1px solid {tabsBorder};
                    padding: 4px 8px;
                    gap: 2px;
                    overflow-x: auto;
                }}
                .sheet-tab {{
                    background-color: {tabBg};
                    color: {tabColor};
                    border: none;
                    border-top-left-radius: 4px;
                    border-top-right-radius: 4px;
                    padding: 8px 16px;
                    cursor: pointer;
                    font-size: 13px;
                    transition: all 0.2s;
                    white-space: nowrap;
                    display: flex;
                    align-items: center;
                    gap: 6px;
                }}
                .sheet-tab:hover {{
                    background-color: {tabHoverBg};
                }}
                .sheet-tab.active {{
                    background-color: {tabActiveBg};
                    color: {tabActiveColor};
                    font-weight: 600;
                    border-top: 2px solid {tabActiveColor};
                    padding-top: 6px;
                }}
                .tab-icon {{
                    font-size: 14px;
                }}
                .empty-sheet {{
                    color: #888888;
                    font-style: italic;
                    padding: 40px;
                    text-align: center;
                }}
                .error {{
                    color: {errorColor};
                    background-color: {errorBg};
                    padding: 12px;
                    border-radius: 4px;
                    border-left: 4px solid {errorColor};
                    margin: 20px;
                }}
                .truncation-warning {{
                    color: {warnColor};
                    background-color: {warnBg};
                    padding: 10px;
                    border-top: 1px solid {warnBorder};
                    text-align: center;
                    font-size: 13px;
                    margin-top: 10px;
                    position: sticky;
                    bottom: 0;
                    left: 0;
                }}
            ";
        }

        // Simple HTML encoding
        private static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }
}
