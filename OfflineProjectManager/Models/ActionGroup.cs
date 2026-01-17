using System.Collections.Generic;

namespace OfflineProjectManager.Models
{
    /// <summary>
    /// Represents a group of related actions in the project action dialog
    /// </summary>
    public class ActionGroup
    {
        /// <summary>
        /// Group header text (e.g., "PROJECT", "WORKSPACE")
        /// </summary>
        public string Header { get; set; }

        /// <summary>
        /// List of action items in this group
        /// </summary>
        public List<ActionItem> Items { get; set; } = new List<ActionItem>();
    }
}
