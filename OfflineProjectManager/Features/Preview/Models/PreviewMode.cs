namespace OfflineProjectManager.Features.Preview.Models
{
    /// <summary>
    /// Preview rendering mode.
    /// </summary>
    public enum PreviewMode
    {
        /// <summary>
        /// Native Shell Preview Handler (fast, no highlight)
        /// </summary>
        Native,

        /// <summary>
        /// Text-based preview with AvalonEdit (highlight enabled)
        /// </summary>
        Text,

        /// <summary>
        /// WebView2-based preview (PDF, HTML)
        /// </summary>
        Web,

        /// <summary>
        /// Image preview
        /// </summary>
        Image
    }
}
