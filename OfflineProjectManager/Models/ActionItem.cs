using System.Windows.Input;

namespace OfflineProjectManager.Models
{
    /// <summary>
    /// Represents a single action item in the project action dialog
    /// </summary>
    public class ActionItem
    {
        /// <summary>
        /// Unicode icon character (e.g., "📂", "🆕")
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Display name of the action
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Keyboard shortcut display text (e.g., "Ctrl+O")
        /// </summary>
        public string Shortcut { get; set; }

        /// <summary>
        /// Command to execute when this action is triggered
        /// </summary>
        public ICommand Command { get; set; }

        /// <summary>
        /// Whether this action is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }
}
