using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using OfflineProjectManager.Services;
using OfflineProjectManager.ViewModels;

namespace OfflineProjectManager.Views
{
    /// <summary>
    /// Professional flat-design Gantt chart view with Canvas rendering
    /// Supports dynamic dark/light theme switching
    /// </summary>
    public partial class GanttChartView : System.Windows.Controls.UserControl
    {
        private readonly GanttChartViewModel _viewModel;
        private const int RowHeight = 36;
        private const int BaseDayWidth = 24;

        // Zoom fields
        private double _zoomLevel = 1.0;
        private const double MinZoom = 0.5;
        private const double MaxZoom = 3.0;
        private const double ZoomStep = 0.1;

        // Task bar lookup for click handling
        private readonly Dictionary<int, (double x, double y, double width, double height)> _taskBarRects = new();

        private int DayWidth => (int)(BaseDayWidth * _zoomLevel);

        public GanttChartView()
        {
            InitializeComponent();

            // Get ViewModel from DI
            if (App.ServiceProvider != null)
            {
                _viewModel = App.ServiceProvider.GetService<GanttChartViewModel>();
                if (_viewModel != null)
                {
                    DataContext = _viewModel;
                    _ = _viewModel.LoadTasksFromDatabaseAsync();

                    // Subscribe to changes to redraw canvas
                    _viewModel.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(_viewModel.GanttItems))
                        {
                            Dispatcher.InvokeAsync(() => DrawGanttBars());
                        }
                    };
                }
            }

            // Subscribe to theme changes
            ThemeService.ThemeChanged += (s, e) => Dispatcher.InvokeAsync(() => DrawGanttBars());

            Loaded += (s, e) => DrawGanttBars();
            SizeChanged += (s, e) => DrawGanttBars();

            // Mouse wheel zoom on Gantt Canvas
            GanttScrollViewer.PreviewMouseWheel += OnGanttCanvasMouseWheel;

            // Double-click to edit task
            GanttCanvas.MouseLeftButtonDown += OnGanttCanvasClick;
        }

        /// <summary>
        /// Expand button click handler - raises event to parent MainWindow
        /// </summary>
        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            // Find MainWindow and toggle Gantt expansion
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.ToggleGanttExpand();
            }
        }

        /// <summary>
        /// Update expand button appearance based on expanded state
        /// </summary>
        public void SetExpandedState(bool isExpanded)
        {
            ExpandButton.Content = isExpanded ? "⛶" : "⛶"; // Same icon, tooltip changes
            ExpandButton.ToolTip = isExpanded ? "Collapse Gantt Chart" : "Expand Gantt Chart to Full Screen";
        }

        /// <summary>
        /// Force redraw the Gantt chart and reset scroll position
        /// </summary>
        public void ForceRedraw()
        {
            // Reset scroll position to top-left
            GanttScrollViewer.ScrollToHome();

            // Delay redraw to allow layout to settle
            Dispatcher.InvokeAsync(() => DrawGanttBars(), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void OnGanttCanvasMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // Check if Ctrl key is pressed for zoom
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;

                // Calculate new zoom level
                if (e.Delta > 0)
                {
                    _zoomLevel = Math.Min(MaxZoom, _zoomLevel + ZoomStep);
                }
                else
                {
                    _zoomLevel = Math.Max(MinZoom, _zoomLevel - ZoomStep);
                }

                // Redraw with new zoom
                DrawGanttBars();
            }
        }

        #region Theme Color Helpers
        private static SolidColorBrush GetThemeBrush(string resourceKey, System.Windows.Media.Color fallback)
        {
            if (System.Windows.Application.Current?.Resources[resourceKey] is SolidColorBrush brush)
                return brush;
            return new SolidColorBrush(fallback);
        }

        private static System.Windows.Media.Color GetThemeColor(string resourceKey, System.Windows.Media.Color fallback)
        {
            if (System.Windows.Application.Current?.Resources[resourceKey] is SolidColorBrush brush)
                return brush.Color;
            return fallback;
        }
        #endregion

        private void DrawGanttBars()
        {
            if (_viewModel?.GanttItems == null || GanttCanvas == null) return;

            GanttCanvas.Children.Clear();
            TimelineHeaderCanvas?.Children.Clear();
            _taskBarRects.Clear();

            var items = _viewModel.GanttItems.ToList();
            if (items.Count == 0) return;

            // Calculate timeline range
            var projectStart = _viewModel.ProjectStartDate;
            var projectEnd = _viewModel.ProjectEndDate;
            var totalDays = (projectEnd - projectStart).Days + 30; // Add buffer

            // Set canvas size
            GanttCanvas.Width = Math.Max(800, totalDays * DayWidth);
            GanttCanvas.Height = items.Count * RowHeight;

            // Set header canvas size
            if (TimelineHeaderCanvas != null)
            {
                TimelineHeaderCanvas.Width = GanttCanvas.Width;
                TimelineHeaderCanvas.Height = 50;
                DrawTimelineHeader(totalDays, projectStart);
            }

            // Draw grid lines
            DrawGridLines(totalDays, items.Count, projectStart);

            // Draw TODAY line
            DrawTodayLine(projectStart, items.Count);

            // Draw task bars
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.StartDate.HasValue && item.EndDate.HasValue)
                {
                    DrawTaskBar(item, i, projectStart);
                }
                else if (item.IsMilestone)
                {
                    DrawMilestone(item, i, projectStart);
                }
            }

            // Draw dependency arrows
            DrawDependencyArrows(items, projectStart);
        }

        /// <summary>
        /// Handle click on Gantt canvas - opens TaskEditDialog on double-click
        /// </summary>
        private async void OnGanttCanvasClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return; // Only double-click

            var pos = e.GetPosition(GanttCanvas);
            var clickedTaskId = GetTaskIdAtPosition(pos.X, pos.Y);

            if (clickedTaskId.HasValue)
            {
                await OpenTaskEditDialog(clickedTaskId.Value);
            }
        }

        private int? GetTaskIdAtPosition(double x, double y)
        {
            foreach (var kvp in _taskBarRects)
            {
                var rect = kvp.Value;
                if (x >= rect.x && x <= rect.x + rect.width &&
                    y >= rect.y && y <= rect.y + rect.height)
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        private async System.Threading.Tasks.Task OpenTaskEditDialog(int taskId)
        {
            var taskService = App.ServiceProvider?.GetService<ITaskService>();
            if (taskService == null) return;

            var task = await taskService.GetTaskByIdAsync(taskId);
            if (task == null) return;

            var dialog = new TaskEditDialog(task)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                // Task was updated or deleted - Gantt will refresh via TasksChanged event
            }
        }

        /// <summary>
        /// Draw dependency arrows between tasks
        /// </summary>
        private void DrawDependencyArrows(List<GanttItemViewModel> items, DateTime projectStart)
        {
            var arrowColor = System.Windows.Media.Color.FromRgb(100, 100, 100);
            var arrowBrush = new SolidColorBrush(arrowColor);

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Dependencies)) continue;

                int[] depIds;
                try
                {
                    depIds = System.Text.Json.JsonSerializer.Deserialize<int[]>(item.Dependencies);
                }
                catch { continue; }

                if (depIds == null) continue;

                foreach (var depId in depIds)
                {
                    if (!_taskBarRects.TryGetValue(depId, out var fromRect)) continue;
                    if (!_taskBarRects.TryGetValue(item.Id, out var toRect)) continue;

                    // Draw arrow from end of predecessor to start of successor
                    var fromX = fromRect.x + fromRect.width;
                    var fromY = fromRect.y + fromRect.height / 2;
                    var toX = toRect.x;
                    var toY = toRect.y + toRect.height / 2;

                    // Draw line
                    var line = new System.Windows.Shapes.Line
                    {
                        X1 = fromX,
                        Y1 = fromY,
                        X2 = toX - 4,
                        Y2 = toY,
                        Stroke = arrowBrush,
                        StrokeThickness = 1.5
                    };
                    GanttCanvas.Children.Add(line);

                    // Draw arrowhead
                    var arrowHead = new System.Windows.Shapes.Polygon
                    {
                        Points = new System.Windows.Media.PointCollection
                        {
                            new System.Windows.Point(toX, toY),
                            new System.Windows.Point(toX - 6, toY - 4),
                            new System.Windows.Point(toX - 6, toY + 4)
                        },
                        Fill = arrowBrush
                    };
                    GanttCanvas.Children.Add(arrowHead);
                }
            }
        }

        private void DrawTimelineHeader(int totalDays, DateTime projectStart)
        {
            var foregroundBrush = GetThemeBrush("ForegroundBrush", System.Windows.Media.Color.FromRgb(0, 0, 0));
            var secondaryBrush = GetThemeBrush("SecondaryForegroundBrush", System.Windows.Media.Color.FromRgb(100, 100, 100));
            var borderBrush = GetThemeBrush("BorderBrush", System.Windows.Media.Color.FromRgb(200, 200, 200));

            // Draw bottom border line
            var bottomLine = new System.Windows.Shapes.Line
            {
                X1 = 0,
                Y1 = 49,
                X2 = totalDays * DayWidth,
                Y2 = 49,
                Stroke = borderBrush,
                StrokeThickness = 1
            };
            TimelineHeaderCanvas.Children.Add(bottomLine);

            // Track current month for rendering month headers
            var currentMonth = projectStart.Month;
            var currentYear = projectStart.Year;
            var monthStartX = 0;

            for (int d = 0; d <= totalDays; d++)
            {
                var currentDate = projectStart.AddDays(d);
                var x = d * DayWidth;

                // Check if month changed - draw month label
                if (currentDate.Month != currentMonth || d == totalDays)
                {
                    // Draw month name in top row
                    var monthWidth = x - monthStartX;
                    if (monthWidth > 40)
                    {
                        var monthName = new DateTime(currentYear, currentMonth, 1).ToString("MMMM");
                        var monthLabel = new TextBlock
                        {
                            Text = $"{monthName} | {currentYear}",
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = foregroundBrush,
                            Width = monthWidth,
                            TextAlignment = TextAlignment.Center
                        };
                        Canvas.SetLeft(monthLabel, monthStartX);
                        Canvas.SetTop(monthLabel, 4);
                        TimelineHeaderCanvas.Children.Add(monthLabel);

                        // Draw vertical separator line
                        var sepLine = new System.Windows.Shapes.Line
                        {
                            X1 = x,
                            Y1 = 0,
                            X2 = x,
                            Y2 = 50,
                            Stroke = borderBrush,
                            StrokeThickness = 1
                        };
                        TimelineHeaderCanvas.Children.Add(sepLine);
                    }

                    // Reset for next month
                    currentMonth = currentDate.Month;
                    currentYear = currentDate.Year;
                    monthStartX = (int)x;
                }

                // Draw each day number in bottom row
                // Adjust display based on zoom level
                bool shouldShowDay = true;
                if (_zoomLevel < 0.7)
                {
                    // At low zoom, show every 3rd day
                    shouldShowDay = (d % 3 == 0);
                }
                else if (_zoomLevel < 1.0)
                {
                    // At medium zoom, show every 2nd day
                    shouldShowDay = (d % 2 == 0);
                }

                if (shouldShowDay && d < totalDays)
                {
                    var dayLabel = new TextBlock
                    {
                        Text = currentDate.Day.ToString(),
                        FontSize = _zoomLevel >= 1.0 ? 9 : 8,
                        Foreground = secondaryBrush,
                        Width = DayWidth,
                        TextAlignment = TextAlignment.Center
                    };
                    Canvas.SetLeft(dayLabel, x);
                    Canvas.SetTop(dayLabel, 28);
                    TimelineHeaderCanvas.Children.Add(dayLabel);
                }
            }

            // Draw middle separator line between months and days
            var middleLine = new System.Windows.Shapes.Line
            {
                X1 = 0,
                Y1 = 25,
                X2 = totalDays * DayWidth,
                Y2 = 25,
                Stroke = borderBrush,
                StrokeThickness = 0.5
            };
            TimelineHeaderCanvas.Children.Add(middleLine);
        }

        private void DrawGridLines(int totalDays, int rowCount, DateTime projectStart)
        {
            // Get theme colors
            var gridLineColor = GetThemeColor("GanttGridLineBrush", System.Windows.Media.Color.FromRgb(229, 231, 235));
            var weekLineColor = GetThemeColor("GanttGridLineWeekBrush", System.Windows.Media.Color.FromRgb(209, 213, 219));
            var weekBandColor = GetThemeColor("GanttWeekBandBrush", System.Windows.Media.Color.FromArgb(20, 100, 100, 100));

            // Draw week bands (alternating subtle background)
            DrawWeekBands(totalDays, rowCount, projectStart, weekBandColor);

            // Vertical lines (days/weeks)
            for (int d = 0; d <= totalDays; d++)
            {
                var x = d * DayWidth;
                var currentDate = projectStart.AddDays(d);
                var isWeekStart = currentDate.DayOfWeek == DayOfWeek.Monday;

                var line = new System.Windows.Shapes.Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = rowCount * RowHeight,
                    Stroke = new SolidColorBrush(isWeekStart ? weekLineColor : gridLineColor),
                    StrokeThickness = isWeekStart ? 1.5 : 0.5
                };
                GanttCanvas.Children.Add(line);
            }

            // Horizontal lines (rows)
            for (int r = 0; r <= rowCount; r++)
            {
                var y = r * RowHeight;
                var line = new System.Windows.Shapes.Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = totalDays * DayWidth,
                    Y2 = y,
                    Stroke = new SolidColorBrush(gridLineColor),
                    StrokeThickness = 0.5
                };
                GanttCanvas.Children.Add(line);
            }
        }

        private void DrawWeekBands(int totalDays, int rowCount, DateTime projectStart, System.Windows.Media.Color bandColor)
        {
            // Find the first Monday
            int daysToFirstMonday = ((int)DayOfWeek.Monday - (int)projectStart.DayOfWeek + 7) % 7;
            int weekIndex = 0;

            for (int d = daysToFirstMonday; d < totalDays; d += 7)
            {
                // Draw band for every other week
                if (weekIndex % 2 == 1)
                {
                    var x = d * DayWidth;
                    var width = Math.Min(7 * DayWidth, (totalDays - d) * DayWidth);

                    var weekBand = new System.Windows.Shapes.Rectangle
                    {
                        Width = width,
                        Height = rowCount * RowHeight,
                        Fill = new SolidColorBrush(bandColor)
                    };
                    Canvas.SetLeft(weekBand, x);
                    Canvas.SetTop(weekBand, 0);
                    GanttCanvas.Children.Add(weekBand);
                }
                weekIndex++;
            }
        }

        private void DrawTodayLine(DateTime projectStart, int rowCount)
        {
            var daysFromStart = (DateTime.Today - projectStart).Days;
            if (daysFromStart < 0) return;

            var todayLineColor = GetThemeColor("GanttTodayLineBrush", System.Windows.Media.Color.FromRgb(239, 68, 68));
            var backgroundBrush = GetThemeBrush("BackgroundBrush", Colors.White);

            var x = daysFromStart * DayWidth;
            var line = new System.Windows.Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = rowCount * RowHeight,
                Stroke = new SolidColorBrush(todayLineColor),
                StrokeThickness = 2,
                StrokeDashArray = [4, 2]
            };
            GanttCanvas.Children.Add(line);

            // TODAY label
            var label = new TextBlock
            {
                Text = "TODAY",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(todayLineColor),
                Background = backgroundBrush
            };
            Canvas.SetLeft(label, x - 18);
            Canvas.SetTop(label, 2);
            GanttCanvas.Children.Add(label);
        }

        private void DrawTaskBar(GanttItemViewModel item, int rowIndex, DateTime projectStart)
        {
            var startDay = (item.StartDate.Value - projectStart).Days;
            var duration = (item.EndDate.Value - item.StartDate.Value).Days;
            if (duration < 1) duration = 1;

            var x = startDay * DayWidth;
            // FIXED: Explicit centered vertical positioning to match XAML Grid
            // Grid Height=36, vertical padding = (36 - barHeight) / 2
            const int barHeight = 24; // RowHeight(36) - 12 for padding
            var y = rowIndex * RowHeight + (RowHeight - barHeight) / 2;
            var width = duration * DayWidth - 4;
            var height = barHeight;

            // Store rect for click detection
            _taskBarRects[item.Id] = (x, y, width, height);

            // ENHANCED: Drop shadow for depth
            var shadowRect = new System.Windows.Shapes.Rectangle
            {
                Width = width,
                Height = height,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                RadiusX = 4,
                RadiusY = 4,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(shadowRect, x + 2);
            Canvas.SetTop(shadowRect, y + 2);
            GanttCanvas.Children.Add(shadowRect);

            // Background bar with gradient
            var bgColor = GetStatusColor(item.Status);
            var lightColor = LightenColor(bgColor, 0.15);
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(lightColor, 0.0),
                    new GradientStop(bgColor, 1.0)
                }
            };

            var bgRect = new System.Windows.Shapes.Rectangle
            {
                Width = width,
                Height = height,
                Fill = gradient,
                Stroke = new SolidColorBrush(DarkenColor(bgColor, 0.3)),
                StrokeThickness = 1,
                RadiusX = 4,
                RadiusY = 4,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"{item.Name}\nProgress: {item.Progress:0}%\n{item.StartDate:d} - {item.EndDate:d}"
            };
            Canvas.SetLeft(bgRect, x);
            Canvas.SetTop(bgRect, y);
            GanttCanvas.Children.Add(bgRect);

            // Progress fill with gradient
            if (item.Progress > 0)
            {
                var progressWidth = width * (item.Progress / 100.0);
                var progressColor = DarkenColor(bgColor, 0.25);
                var progressLightColor = LightenColor(progressColor, 0.1);

                var progressGradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(progressLightColor, 0.0),
                        new GradientStop(progressColor, 1.0)
                    }
                };

                var progressRect = new System.Windows.Shapes.Rectangle
                {
                    Width = progressWidth,
                    Height = height,
                    Fill = progressGradient,
                    RadiusX = 4,
                    RadiusY = 4,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(progressRect, x);
                Canvas.SetTop(progressRect, y);
                GanttCanvas.Children.Add(progressRect);
            }

            // Task name label (if bar is wide enough)
            if (width > 60)
            {
                var label = new TextBlock
                {
                    Text = item.Name,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.White),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = width - 8,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, x + 4);
                Canvas.SetTop(label, y + (height - 14) / 2);
                GanttCanvas.Children.Add(label);
            }
        }

        private void DrawMilestone(GanttItemViewModel item, int rowIndex, DateTime projectStart)
        {
            if (!item.MilestoneDate.HasValue) return;

            var day = (item.MilestoneDate.Value - projectStart).Days;
            var x = day * DayWidth;
            var y = rowIndex * RowHeight + RowHeight / 2;

            var milestoneColor = System.Windows.Media.Color.FromRgb(220, 38, 38);

            // Flag pole
            var pole = new System.Windows.Shapes.Line
            {
                X1 = x,
                Y1 = y - 10,
                X2 = x,
                Y2 = y + 10,
                Stroke = new SolidColorBrush(milestoneColor),
                StrokeThickness = 2
            };
            GanttCanvas.Children.Add(pole);

            // Flag
            var flag = new System.Windows.Shapes.Polygon
            {
                Points =
                [
                    new System.Windows.Point(x, y - 10),
                    new System.Windows.Point(x + 12, y - 5),
                    new System.Windows.Point(x, y)
                ],
                Fill = new SolidColorBrush(milestoneColor)
            };
            GanttCanvas.Children.Add(flag);
        }

        private static System.Windows.Media.Color GetStatusColor(string status)
        {
            return status?.ToLower() switch
            {
                "done" or "completed" => System.Windows.Media.Color.FromRgb(22, 163, 74),   // Green #16A34A
                "inprogress" or "in progress" => System.Windows.Media.Color.FromRgb(37, 99, 235), // Blue #2563EB
                "overdue" => System.Windows.Media.Color.FromRgb(220, 38, 38),              // Red #DC2626
                _ => System.Windows.Media.Color.FromRgb(156, 163, 175)                     // Gray #9CA3AF
            };
        }

        private static System.Windows.Media.Color DarkenColor(System.Windows.Media.Color color, double factor)
        {
            return System.Windows.Media.Color.FromRgb(
                (byte)(color.R * (1 - factor)),
                (byte)(color.G * (1 - factor)),
                (byte)(color.B * (1 - factor))
            );
        }

        private static System.Windows.Media.Color LightenColor(System.Windows.Media.Color color, double factor)
        {
            return System.Windows.Media.Color.FromRgb(
                (byte)Math.Min(255, color.R + (255 - color.R) * factor),
                (byte)Math.Min(255, color.G + (255 - color.G) * factor),
                (byte)Math.Min(255, color.B + (255 - color.B) * factor)
            );
        }
    }
}
