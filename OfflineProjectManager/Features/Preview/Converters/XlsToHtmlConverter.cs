using System;
using System.IO;
using System.Text;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using OfflineProjectManager.Services;

namespace OfflineProjectManager.Features.Preview.Providers
{
    /// <summary>
    /// Converts Excel .xls (97-2003) files to HTML using NPOI
    /// </summary>
    public class XlsToHtmlConverter
    {
        public string ConvertToHtml(string xlsPath)
        {
            if (!File.Exists(xlsPath))
                throw new FileNotFoundException("Excel file not found", xlsPath);

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<title>Excel Preview (.xls)</title>");
            html.AppendLine("<style>");
            html.AppendLine(GetExcelStyles());
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            try
            {
                using (var fileStream = File.OpenRead(xlsPath))
                {
                    var workbook = new HSSFWorkbook(fileStream);

                    // Excel container
                    html.AppendLine("<div class='excel-container'>");

                    // Content area (sheets)
                    html.AppendLine("<div class='excel-content'>");

                    for (int i = 0; i < workbook.NumberOfSheets; i++)
                    {
                        var sheet = workbook.GetSheetAt(i);
                        string displayStyle = i == 0 ? "block" : "none";
                        html.AppendLine($"<div id='sheet{i + 1}' class='excel-sheet' style='display:{displayStyle}'>");
                        html.AppendLine(ConvertSheet(sheet));
                        html.AppendLine("</div>");
                    }

                    html.AppendLine("</div>");

                    // Tabs at bottom (like Excel)
                    html.AppendLine("<div class='sheet-tabs-bottom'>");
                    for (int i = 0; i < workbook.NumberOfSheets; i++)
                    {
                        var sheet = workbook.GetSheetAt(i);
                        string activeClass = i == 0 ? " active" : "";
                        html.AppendLine($"<button class='sheet-tab{activeClass}' onclick='showSheet({i + 1})'>");
                        html.AppendLine($"<span class='tab-icon'>📄</span> {HtmlEncode(sheet.SheetName)}");
                        html.AppendLine("</button>");
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

        private string ConvertSheet(ISheet sheet)
        {
            var html = new StringBuilder();

            if (sheet.PhysicalNumberOfRows == 0)
            {
                html.AppendLine("<p class='empty-sheet'>This sheet is empty</p>");
                return html.ToString();
            }

            html.AppendLine("<div class='table-container'>");
            html.AppendLine("<table class='excel-table'>");

            // Find the last row and column with data
            int lastRow = sheet.LastRowNum;

            // Apply Row Limit
            bool rowsTruncated = false;
            if (lastRow > MAX_ROWS)
            {
                lastRow = MAX_ROWS;
                rowsTruncated = true;
            }

            int maxCol = 0;

            // Scan columns only up to the limited rows to find maxCol
            // This avoids scanning the entire file if we're only showing the top part
            for (int rowIdx = 0; rowIdx <= lastRow; rowIdx++)
            {
                var row = sheet.GetRow(rowIdx);
                if (row != null && row.LastCellNum > maxCol)
                {
                    maxCol = row.LastCellNum;
                }
            }

            // Apply Col Limit
            bool colsTruncated = false;
            if (maxCol > MAX_COLS)
            {
                maxCol = MAX_COLS;
                colsTruncated = true;
            }

            // Convert rows to HTML
            for (int rowIdx = 0; rowIdx <= lastRow; rowIdx++)
            {
                var row = sheet.GetRow(rowIdx);
                html.AppendLine("<tr>");

                if (row != null)
                {
                    for (int colIdx = 0; colIdx < maxCol; colIdx++)
                    {
                        var cell = row.GetCell(colIdx);
                        var cellValue = GetCellValue(cell);

                        var style = new StringBuilder();

                        // Check for bold font
                        if (cell != null && cell.CellStyle != null)
                        {
                            var font = sheet.Workbook.GetFontAt(cell.CellStyle.FontIndex);
                            if (font.IsBold)
                            {
                                style.Append("font-weight: bold; ");
                            }
                        }

                        // Determine cell class
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
                }
                else
                {
                    // Empty row
                    for (int colIdx = 0; colIdx < maxCol; colIdx++)
                    {
                        html.AppendLine("<td></td>");
                    }
                }

                html.AppendLine("</tr>");
            }

            html.AppendLine("</table>");

            // Add warning if truncated (reusing same class name as Xlsx converter, need to add CSS too or inline it)
            if (rowsTruncated || colsTruncated)
            {
                html.AppendLine("<div style='color: #cca700; background-color: #3e3800; padding: 10px; border-top: 1px solid #cca700; text-align: center; font-size: 13px; position: sticky; bottom: 0; left: 0;'>");
                html.AppendLine($"⚠️ Preview giới hạn {MAX_ROWS} dòng và {MAX_COLS} cột để tối ưu hiệu năng. Mở file bằng Excel để xem đầy đủ.");
                html.AppendLine("</div>");
            }

            html.AppendLine("</div>");

            return html.ToString();
        }

        private static string GetCellValue(ICell cell)
        {
            if (cell == null)
                return "";

            switch (cell.CellType)
            {
                case CellType.String:
                    return cell.StringCellValue;

                case CellType.Numeric:
                    if (DateUtil.IsCellDateFormatted(cell))
                    {
                        var date = cell.DateCellValue;
                        return $"{date.Year:D4}-{date.Month:D2}-{date.Day:D2}";
                    }
                    return cell.NumericCellValue.ToString();

                case CellType.Boolean:
                    return cell.BooleanCellValue.ToString();

                case CellType.Formula:
                    try
                    {
                        return cell.NumericCellValue.ToString();
                    }
                    catch
                    {
                        try
                        {
                            return cell.StringCellValue;
                        }
                        catch
                        {
                            return cell.CellFormula;
                        }
                    }

                case CellType.Blank:
                    return "";

                default:
                    return cell.ToString() ?? "";
            }
        }

        private static string GetExcelStyles()
        {
            bool isDark = ThemeService.IsDarkMode;

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
            ";
        }

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
