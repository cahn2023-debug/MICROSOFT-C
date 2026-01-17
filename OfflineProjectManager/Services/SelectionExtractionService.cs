using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using OfflineProjectManager.Models;
using OfflineProjectManager.Features.Preview;
using System.Data;

namespace OfflineProjectManager.Services
{
    public static class SelectionExtractionService
    {
        public static SelectionContext GetSelection(object control, string filePath)
        {
            if (control == null) return null;

            var context = new SelectionContext
            {
                FilePath = filePath
            };

            // 1. Text Editor (TXT, CODE, MD, DOCX Parsed)
            if (control is TextEditor editor)
            {
                if (editor.SelectionLength == 0) return null;

                context.PreviewType = "Text";
                context.SelectedText = editor.SelectedText;
                context.SelectionStart = editor.SelectionStart;
                context.SelectionLength = editor.SelectionLength;

                var loc = editor.Document.GetLocation(editor.SelectionStart);
                context.LineNumber = loc.Line;

                return context;
            }

            // 2. Excel (DataGrid or ContentControl wrapping DataGrid)
            DataGrid dataGrid = control as DataGrid;
            if (dataGrid == null && control is ContentControl cc)
            {
                dataGrid = cc.Content as DataGrid;
            }

            if (dataGrid != null)
            {
                var cellInfo = dataGrid.CurrentCell;
                if (cellInfo.IsValid)
                {
                    context.PreviewType = "Excel";
                    context.SheetName = "Sheet1"; // Default as we only load first sheet now

                    // Get Value
                    if (cellInfo.Item is DataRowView rowView)
                    {
                        var colIndex = cellInfo.Column.DisplayIndex;
                        context.CellRange = $"{rowView.Row.Table.Rows.IndexOf(rowView.Row)},{colIndex}";

                        try
                        {
                            // Property name in DataTable is "Col N"
                            string bindingPath = (cellInfo.Column as DataGridTextColumn)?.Binding is System.Windows.Data.Binding binding
                                ? binding.Path.Path : null;

                            if (!string.IsNullOrEmpty(bindingPath))
                                context.SelectedText = rowView.Row[bindingPath]?.ToString();
                            else
                                context.SelectedText = rowView.Row[colIndex]?.ToString();
                        }
                        catch
                        {
                            context.SelectedText = "Cell Data";
                        }
                    }

                    if (string.IsNullOrEmpty(context.SelectedText)) context.SelectedText = "Cell Data";
                    return context;
                }
            }

            // 3. Grid with PreviewContextMenuHelper Tag data
            if (control is Grid grid && grid.Tag != null)
            {
                // 3a. WebView2SelectionData (Word, Excel via WebView2)
                if (grid.Tag is PreviewContextMenuHelper.WebView2SelectionData webViewData)
                {
                    if (string.IsNullOrWhiteSpace(webViewData.SelectedText)) return null;

                    context.FilePath = webViewData.FilePath;
                    context.PreviewType = webViewData.PreviewType;
                    context.SelectedText = webViewData.SelectedText;
                    return context;
                }

                // 3b. RegionSelectionData (Image, PDF)
                if (grid.Tag is PreviewContextMenuHelper.RegionSelectionData regionData)
                {
                    if (regionData.RectWidth < 10 || regionData.RectHeight < 10) return null;

                    context.FilePath = regionData.FilePath;
                    context.PreviewType = regionData.PreviewType;
                    context.SelectedText = regionData.SelectedText ?? "Region";
                    context.RectX = regionData.RectX;
                    context.RectY = regionData.RectY;
                    context.RectWidth = regionData.RectWidth;
                    context.RectHeight = regionData.RectHeight;
                    context.LineNumber = regionData.PageNumber; // Using LineNumber for page
                    return context;
                }

                // Legacy: Image (Grid with Rect in Tag)
                if (grid.Tag is Rect rect)
                {
                    context.PreviewType = "Image";
                    context.RectX = rect.X;
                    context.RectY = rect.Y;
                    context.RectWidth = rect.Width;
                    context.RectHeight = rect.Height;
                    context.SelectedText = "Region";
                    return context;
                }

                // Legacy: PDF with anonymous type
                var tagData = grid.Tag;
                var tagType = tagData.GetType();

                var webViewProp = tagType.GetProperty("WebView");
                var selectedTextProp = tagType.GetProperty("SelectedText");

                if (webViewProp != null && selectedTextProp != null)
                {
                    context.PreviewType = "PDF";
                    var selectedText = selectedTextProp.GetValue(tagData)?.ToString();
                    context.SelectedText = selectedText ?? "";
                    context.LineNumber = 1;
                    return context;
                }
            }

            return null;
        }
    }
}
