using System;
using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace OfflineProjectManager.Features.Preview.Helpers
{
    /// <summary>
    /// Security configuration helper for WebView2.
    /// Implements sandbox restrictions for secure preview rendering.
    /// </summary>
    public static class WebView2SandboxHelper
    {
        private static readonly string UserDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OfflineProjectManager",
            "WebView2Data");

        /// <summary>
        /// Initializes WebView2 with security restrictions for preview mode.
        /// </summary>
        public static async System.Threading.Tasks.Task<bool> InitializeSandboxedAsync(WebView2 webView, bool allowPdfJs = false)
        {
            if (webView == null)
                throw new ArgumentNullException(nameof(webView));

            try
            {
                // Ensure user data folder exists
                if (!Directory.Exists(UserDataFolder))
                {
                    Directory.CreateDirectory(UserDataFolder);
                }

                // Create environment with custom user data folder
                var options = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--disable-features=msWebOOUI,msPdfOOUI"
                };

                var environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: UserDataFolder,
                    options: options);

                await webView.EnsureCoreWebView2Async(environment);

                // Apply security settings
                ApplySecuritySettings(webView.CoreWebView2, allowPdfJs);

                System.Diagnostics.Debug.WriteLine("[WebView2Sandbox] Initialized with security restrictions");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebView2Sandbox] Initialization failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies security restrictions to an already-initialized CoreWebView2.
        /// </summary>
        public static void ApplySecuritySettings(CoreWebView2 coreWebView, bool allowPdfJs = false)
        {
            if (coreWebView == null)
                throw new ArgumentNullException(nameof(coreWebView));

            var settings = coreWebView.Settings;

            // Disable potentially dangerous features
            settings.IsScriptEnabled = true; // Required for highlighting
            settings.AreDefaultScriptDialogsEnabled = false; // Block alert(), confirm(), prompt()
            settings.IsWebMessageEnabled = true;
            settings.AreDevToolsEnabled = false;
            settings.AreDefaultContextMenusEnabled = true;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = true;
            settings.IsPinchZoomEnabled = true;

            // Block file system access
            settings.AreHostObjectsAllowed = false;
            settings.IsBuiltInErrorPageEnabled = false;

            // Block new windows
            coreWebView.NewWindowRequested += OnNewWindowRequested;

            // Block external navigation
            coreWebView.NavigationStarting += OnNavigationStarting;

            // Block JavaScript dialogs
            coreWebView.ScriptDialogOpening += OnScriptDialogOpening;

            // Inject CSP headers
            InjectContentSecurityPolicy(coreWebView, allowPdfJs);

            System.Diagnostics.Debug.WriteLine("[WebView2Sandbox] Security settings applied");
        }

        /// <summary>
        /// Injects Content Security Policy to restrict resource loading.
        /// </summary>
        private static void InjectContentSecurityPolicy(CoreWebView2 coreWebView, bool allowPdfJs)
        {
            string csp = allowPdfJs
                ? "default-src 'self' 'unsafe-inline' blob: data:; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline';"
                : "default-src 'self' 'unsafe-inline' data:; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:;";

            coreWebView.AddScriptToExecuteOnDocumentCreatedAsync($@"
                (function() {{
                    var meta = document.createElement('meta');
                    meta.httpEquiv = 'Content-Security-Policy';
                    meta.content = ""{csp}"";
                    if (document.head) {{
                        document.head.insertBefore(meta, document.head.firstChild);
                    }}
                }})();
            ");
        }

        /// <summary>
        /// Blocks all new window requests (popup blocking).
        /// </summary>
        private static void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            System.Diagnostics.Debug.WriteLine($"[WebView2Sandbox] Blocked new window: {e.Uri}");
        }

        /// <summary>
        /// Blocks navigation to external URLs.
        /// </summary>
        private static void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            var uri = e.Uri;

            // Allow local files and data URIs
            if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                uri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) ||
                uri == "about:blank")
            {
                return;
            }

            // Block all other navigation
            e.Cancel = true;
            System.Diagnostics.Debug.WriteLine($"[WebView2Sandbox] Blocked navigation to: {uri}");
        }

        /// <summary>
        /// Blocks all JavaScript dialog boxes.
        /// </summary>
        private static void OnScriptDialogOpening(object sender, CoreWebView2ScriptDialogOpeningEventArgs e)
        {
            e.Accept();
            System.Diagnostics.Debug.WriteLine($"[WebView2Sandbox] Blocked dialog: {e.Kind}");
        }

        /// <summary>
        /// Removes event handlers to prevent memory leaks.
        /// </summary>
        public static void Detach(CoreWebView2 coreWebView)
        {
            if (coreWebView == null) return;

            try
            {
                coreWebView.NewWindowRequested -= OnNewWindowRequested;
                coreWebView.NavigationStarting -= OnNavigationStarting;
                coreWebView.ScriptDialogOpening -= OnScriptDialogOpening;
            }
            catch { /* Ignore detach errors */ }
        }

        /// <summary>
        /// Clears all WebView2 browsing data for privacy.
        /// </summary>
        public static async System.Threading.Tasks.Task ClearBrowsingDataAsync(CoreWebView2 coreWebView)
        {
            if (coreWebView == null) return;

            try
            {
                await coreWebView.Profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.AllDomStorage |
                    CoreWebView2BrowsingDataKinds.CacheStorage |
                    CoreWebView2BrowsingDataKinds.Cookies);

                System.Diagnostics.Debug.WriteLine("[WebView2Sandbox] Browsing data cleared");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebView2Sandbox] Failed to clear data: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the JavaScript code for secure text highlighting.
        /// </summary>
        public static string GetHighlightScript(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return string.Empty;

            string escapedKeyword = keyword
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            return $@"
                (function() {{
                    try {{
                        window.getSelection().removeAllRanges();
                        if (window.find && window.find('{escapedKeyword}', false, false, true)) {{
                            var sel = window.getSelection();
                            if (sel && sel.rangeCount > 0) {{
                                var range = sel.getRangeAt(0);
                                range.startContainer.parentElement?.scrollIntoView({{
                                    behavior: 'smooth',
                                    block: 'center'
                                }});
                            }}
                        }}
                    }} catch(e) {{ console.error('Highlight error:', e); }}
                }})();
            ";
        }
    }
}
