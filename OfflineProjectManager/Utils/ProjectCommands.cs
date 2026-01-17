using System.Windows.Input;

namespace OfflineProjectManager.Utils
{
    public static class ProjectCommands
    {
        public static readonly RoutedUICommand AddToTask = new RoutedUICommand(
            "Add to Task",
            "AddToTask",
            typeof(ProjectCommands)
        );

        public static readonly RoutedUICommand AddToNote = new RoutedUICommand(
            "Add to Note",
            "AddToNote",
            typeof(ProjectCommands)
        );
    }
}
