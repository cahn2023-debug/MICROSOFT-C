using System;

#nullable enable

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Simple service to track the current app theme for HTML preview generation.
    /// </summary>
    public static class ThemeService
    {
        private static bool _isDarkMode = true;

        /// <summary>
        /// Event fired when theme changes.
        /// </summary>
        public static event EventHandler? ThemeChanged;

        /// <summary>
        /// Gets or sets whether the app is in dark mode.
        /// </summary>
        public static bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    ThemeChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Detects current theme from app resources.
        /// </summary>
        public static void DetectFromResources()
        {
            var appResources = System.Windows.Application.Current?.Resources;
            if (appResources?["BackgroundBrush"] is System.Windows.Media.SolidColorBrush bgBrush)
            {
                // Dark background is approx #1E1E1E (R=30)
                // Light is White (R > 200)
                _isDarkMode = bgBrush.Color.R < 100;
            }
        }
    }
}
