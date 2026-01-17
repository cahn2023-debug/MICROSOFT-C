using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Wpf;
using OfflineProjectManager.Features.Preview.Providers;
using OfflineProjectManager.Services;

namespace OfflineProjectManager.Features.Preview.Services
{
    /// <summary>
    /// Unified preview service supporting Office, images, text, code, and PDF files.
    /// Simple flow: File → Converter/Handler → HTML → WebView2
    /// Each preview creates a fresh WebView2 (WPF visual tree limitation)
    /// </summary>
    public class PreviewService : IPreviewService
    {
        private const int MAX_HTML_SIZE = 2_000_000; // 2MB
        private const int MAX_TEXT_FILE_SIZE = 5_000_000; // 5MB for text files
        private const int MAX_IMAGE_SIZE = 50_000_000; // 50MB for images

        public PreviewService()
        {
        }

        /// <summary>
        /// Creates preview for the specified file with optional search keyword highlighting.
        /// Returns WebView2 control with rendered content.
        /// </summary>
        public async Task<UIElement> CreatePreviewAsync(string filePath, string searchKeyword = null)
        {
            System.Diagnostics.Debug.WriteLine($"[PreviewService] CreatePreviewAsync called for: {filePath}, keyword: {searchKeyword ?? "(none)"}");

            // Validate input early
            if (string.IsNullOrEmpty(filePath))
            {
                System.Diagnostics.Debug.WriteLine("[PreviewService] ERROR: No file path specified");
                return CreateErrorElement("No file path specified");
            }

            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"[PreviewService] ERROR: File not found: {filePath}");
                return CreateErrorElement($"File not found: {Path.GetFileName(filePath)}");
            }

            // Check if file type is supported
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            System.Diagnostics.Debug.WriteLine($"[PreviewService] File extension: {ext}");

            if (!IsSupportedFileType(ext))
            {
                System.Diagnostics.Debug.WriteLine($"[PreviewService] Unsupported file type: {ext}");
                return CreateUnsupportedElement(ext);
            }

            System.Diagnostics.Debug.WriteLine($"[PreviewService] File type supported: {ext}");

            try
            {
                // Special handling for text/code files - use native AvalonEdit control for better highlighting
                if (IsTextOrCodeFile(ext))
                {
                    System.Diagnostics.Debug.WriteLine("[PreviewService] Creating native text preview with AvalonEdit...");
                    return CreateTextPreviewControl(filePath, searchKeyword);
                }

                // Special handling for PDF - use pdfjs
                if (ext == ".pdf")
                {
                    System.Diagnostics.Debug.WriteLine("[PreviewService] Creating PDF preview...");
                    return await CreatePdfPreviewAsync(filePath, searchKeyword);
                }

                // Get HTML from converter (inside try-catch)
                string html;
                try
                {
                    System.Diagnostics.Debug.WriteLine("[PreviewService] Getting preview HTML...");
                    html = GetPreviewHtml(filePath);
                    System.Diagnostics.Debug.WriteLine($"[PreviewService] HTML generated, length: {html?.Length ?? 0}");
                }
                catch (Exception convEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[PreviewService] Converter error: {convEx}");
                    return CreateErrorElement($"Error converting file: {convEx.Message}");
                }

                // Ensure we're on UI thread for WebView2 creation
                var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                System.Diagnostics.Debug.WriteLine($"[PreviewService] On UI thread: {dispatcher.CheckAccess()}");

                if (!dispatcher.CheckAccess())
                {
                    // Invoke on UI thread
                    System.Diagnostics.Debug.WriteLine("[PreviewService] Invoking on UI thread...");
                    return await dispatcher.InvokeAsync(async () =>
                    {
                        return await CreateWebViewPreviewAsync(html, searchKeyword);
                    }).Task.Unwrap();
                }

                System.Diagnostics.Debug.WriteLine("[PreviewService] Creating WebView preview...");
                var result = await CreateWebViewPreviewAsync(html, searchKeyword);
                System.Diagnostics.Debug.WriteLine($"[PreviewService] Preview created: {result?.GetType().Name ?? "null"}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PreviewService] Error: {ex}");
                return CreateErrorElement($"Preview error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates WebView2 preview with HTML content and optional keyword highlighting.
        /// Always creates a fresh WebView2 instance.
        /// </summary>
        private async Task<UIElement> CreateWebViewPreviewAsync(string html, string searchKeyword = null)
        {
            System.Diagnostics.Debug.WriteLine($"[PreviewService] CreateWebViewPreviewAsync starting, keyword: {searchKeyword ?? "(none)"}");

            try
            {
                // Create fresh WebView2 for each preview
                System.Diagnostics.Debug.WriteLine("[PreviewService] Creating WebView2 instance...");
                var webView = new WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    MinHeight = 300,
                    MinWidth = 400
                };
                System.Diagnostics.Debug.WriteLine("[PreviewService] WebView2 instance created");

                // Initialize WebView2 core
                System.Diagnostics.Debug.WriteLine("[PreviewService] Initializing WebView2 core...");
                await webView.EnsureCoreWebView2Async(null);
                System.Diagnostics.Debug.WriteLine("[PreviewService] WebView2 core initialized successfully");

                // Setup navigation completion tracking
                var navigationTcs = new TaskCompletionSource<bool>();
                string keywordForClosure = searchKeyword; // Capture for closure

                async void OnNavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
                {
                    webView.NavigationCompleted -= OnNavigationCompleted;
                    System.Diagnostics.Debug.WriteLine($"[PreviewService] Navigation completed: Success={e.IsSuccess}");

                    if (e.IsSuccess && !string.IsNullOrWhiteSpace(keywordForClosure))
                    {
                        // Inject highlight script after navigation completes
                        try
                        {
                            await InjectHighlightScriptAsync(webView, keywordForClosure);
                        }
                        catch (Exception scriptEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PreviewService] Highlight script error: {scriptEx.Message}");
                        }
                    }

                    navigationTcs.TrySetResult(e.IsSuccess);
                }

                webView.NavigationCompleted += OnNavigationCompleted;

                // Navigate based on size
                System.Diagnostics.Debug.WriteLine($"[PreviewService] HTML length: {html.Length}, MAX: {MAX_HTML_SIZE}");
                if (html.Length > MAX_HTML_SIZE)
                {
                    // Large file: save to temp and navigate
                    var tempPath = Path.Combine(Path.GetTempPath(), $"preview_{Guid.NewGuid()}.html");
                    System.Diagnostics.Debug.WriteLine($"[PreviewService] Large file, saving to: {tempPath}");
                    await File.WriteAllTextAsync(tempPath, html);
                    webView.Source = new Uri(tempPath, UriKind.Absolute);
                }
                else
                {
                    // Small file: direct navigation
                    System.Diagnostics.Debug.WriteLine("[PreviewService] Using NavigateToString...");
                    webView.NavigateToString(html);
                }

                // Wait for navigation with timeout (10 seconds)
                System.Diagnostics.Debug.WriteLine("[PreviewService] Waiting for navigation to complete...");
                var timeoutTask = System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(10));
                var completedTask = await System.Threading.Tasks.Task.WhenAny(navigationTcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    webView.NavigationCompleted -= OnNavigationCompleted;
                    System.Diagnostics.Debug.WriteLine("[PreviewService] Navigation timeout, but returning WebView anyway");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[PreviewService] Navigation completed within timeout");
                }

                System.Diagnostics.Debug.WriteLine("[PreviewService] Returning WebView2 preview");
                return webView;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PreviewService] WebView2 Error: {ex}");
                return CreateErrorElement($"WebView2 error: {ex.Message}");
            }
        }

        /// <summary>
        /// Injects JavaScript to highlight search keyword and scroll to first match.
        /// Supports Vietnamese accent-insensitive matching.
        /// </summary>
        private static async System.Threading.Tasks.Task InjectHighlightScriptAsync(WebView2 webView, string keyword)
        {
            if (webView?.CoreWebView2 == null || string.IsNullOrWhiteSpace(keyword))
                return;

            // Escape keyword for JavaScript string
            var escapedKeyword = keyword
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            var script = $@"
(function() {{
    const keyword = '{escapedKeyword}';
    if (!keyword) return;
    
    // Add highlight CSS
    if (!document.getElementById('search-highlight-style')) {{
        const style = document.createElement('style');
        style.id = 'search-highlight-style';
        style.textContent = `
            .search-highlight {{
                background-color: #FFEB3B !important;
                padding: 1px 2px;
                border-radius: 2px;
                color: #000 !important;
            }}
            .search-highlight-first {{
                background-color: #FF9800 !important;
                outline: 2px solid #E65100;
            }}
        `;
        document.head.appendChild(style);
    }}
    
    // Remove Vietnamese accents for matching
    function removeAccents(str) {{
        if (!str) return '';
        return str.normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '')
            .replace(/đ/g, 'd')
            .replace(/Đ/g, 'D')
            .toLowerCase();
    }}
    
    const keywordNorm = removeAccents(keyword);
    let firstMatch = null;
    let matchCount = 0;
    const maxMatches = 500; // Limit for performance
    
    // Use TreeWalker for efficient text node traversal
    const walker = document.createTreeWalker(
        document.body,
        NodeFilter.SHOW_TEXT,
        {{
            acceptNode: function(node) {{
                if (node.parentElement && ['SCRIPT', 'STYLE', 'NOSCRIPT', 'MARK'].includes(node.parentElement.tagName)) {{
                    return NodeFilter.FILTER_REJECT;
                }}
                return NodeFilter.FILTER_ACCEPT;
            }}
        }}
    );
    
    const nodesToProcess = [];
    while (walker.nextNode() && matchCount < maxMatches) {{
        const node = walker.currentNode;
        const textNorm = removeAccents(node.textContent);
        if (textNorm.includes(keywordNorm)) {{
            nodesToProcess.push(node);
        }}
    }}
    
    // Process collected nodes
    for (const node of nodesToProcess) {{
        if (matchCount >= maxMatches) break;
        
        const text = node.textContent;
        const textNorm = removeAccents(text);
        let idx = textNorm.indexOf(keywordNorm);
        
        if (idx >= 0) {{
            const parent = node.parentNode;
            const frag = document.createDocumentFragment();
            let lastEnd = 0;
            
            while (idx >= 0 && matchCount < maxMatches) {{
                // Add text before match
                if (idx > lastEnd) {{
                    frag.appendChild(document.createTextNode(text.substring(lastEnd, idx)));
                }}
                
                // Create highlight mark
                const mark = document.createElement('mark');
                mark.className = matchCount === 0 ? 'search-highlight search-highlight-first' : 'search-highlight';
                mark.textContent = text.substring(idx, idx + keyword.length);
                frag.appendChild(mark);
                
                if (!firstMatch) firstMatch = mark;
                matchCount++;
                
                lastEnd = idx + keyword.length;
                idx = textNorm.indexOf(keywordNorm, lastEnd);
            }}
            
            // Add remaining text
            if (lastEnd < text.length) {{
                frag.appendChild(document.createTextNode(text.substring(lastEnd)));
            }}
            
            parent.replaceChild(frag, node);
        }}
    }}
    
    // Scroll to first match
    if (firstMatch) {{
        setTimeout(() => {{
            firstMatch.scrollIntoView({{ behavior: 'smooth', block: 'center' }});
        }}, 100);
    }}
    
    console.log('[PreviewService] Highlighted ' + matchCount + ' matches for: ' + keyword);
}})();";

            System.Diagnostics.Debug.WriteLine($"[PreviewService] Injecting highlight script for keyword: {keyword}");
            await webView.CoreWebView2.ExecuteScriptAsync(script);
            System.Diagnostics.Debug.WriteLine("[PreviewService] Highlight script injected successfully");
        }

        /// <summary>
        /// Creates PDF preview using pdfjs library with optional search keyword.
        /// </summary>
        private async Task<UIElement> CreatePdfPreviewAsync(string filePath, string searchKeyword = null)
        {
            try
            {
                var webView = new WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    MinHeight = 300,
                    MinWidth = 400
                };

                await webView.EnsureCoreWebView2Async(null);

                // Find pdfjs viewer
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var pdfJsViewer = Path.Combine(baseDir, "pdfjs", "web", "viewer.html");

                if (File.Exists(pdfJsViewer))
                {
                    var encodedPath = Uri.EscapeDataString(filePath.Replace("\\", "/"));
                    var viewerUri = $"file:///{pdfJsViewer.Replace("\\", "/")}?file=file:///{encodedPath}";

                    // Add search query to PDF.js if keyword provided
                    if (!string.IsNullOrWhiteSpace(searchKeyword))
                    {
                        viewerUri += $"#search={Uri.EscapeDataString(searchKeyword)}";
                    }

                    webView.Source = new Uri(viewerUri);
                }
                else
                {
                    // Fallback: show PDF info
                    var html = GetPdfFallbackHtml(filePath);
                    webView.NavigateToString(html);
                }

                return webView;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PreviewService] PDF Error: {ex}");
                return CreateErrorElement($"PDF preview error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets HTML preview using appropriate converter or handler.
        /// </summary>
        private string GetPreviewHtml(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            return ext switch
            {
                // Office documents
                ".docx" => new WordToHtmlConverter().ConvertToHtmlWithBase64Images(filePath),
                ".doc" => new DocToHtmlConverter().ConvertToHtml(filePath),
                ".xlsx" => new ExcelToHtmlConverter().ConvertToHtml(filePath),
                ".xls" => new XlsToHtmlConverter().ConvertToHtml(filePath),

                // Images
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".ico" or ".svg"
                    => GetImageHtml(filePath),

                // Text and code files
                ".txt" or ".md" or ".json" or ".xml" or ".csv" or ".log" or
                ".cs" or ".py" or ".js" or ".ts" or ".html" or ".css" or ".sql" or
                ".yaml" or ".yml" or ".ini" or ".cfg" or ".config" or ".sh" or ".bat" or ".ps1"
                    => GetTextHtml(filePath),

                _ => GetUnsupportedHtml(ext)
            };
        }

        /// <summary>
        /// Generates HTML for image preview with base64 embedding.
        /// </summary>
        private string GetImageHtml(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MAX_IMAGE_SIZE)
            {
                return GetFileTooLargeHtml(filePath, "image");
            }

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var mimeType = GetImageMimeType(ext);
            bool isDark = ThemeService.IsDarkMode;

            string bgColor = isDark ? "#1e1e1e" : "#f0f0f0";
            string checkPattern = isDark ? "#2d2d30" : "#e0e0e0";
            string infoColor = isDark ? "#888888" : "#666666";

            // For SVG, we can embed directly
            if (ext == ".svg")
            {
                var svgContent = File.ReadAllText(filePath);
                return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ 
            margin: 0; 
            display: flex; 
            flex-direction: column;
            justify-content: center; 
            align-items: center; 
            min-height: 100vh;
            background: {bgColor};
            background-image: linear-gradient(45deg, {checkPattern} 25%, transparent 25%), 
                              linear-gradient(-45deg, {checkPattern} 25%, transparent 25%), 
                              linear-gradient(45deg, transparent 75%, {checkPattern} 75%), 
                              linear-gradient(-45deg, transparent 75%, {checkPattern} 75%);
            background-size: 20px 20px;
            background-position: 0 0, 0 10px, 10px -10px, -10px 0px;
        }}
        .svg-container {{
            max-width: 95%;
            max-height: 85vh;
            overflow: auto;
            background: {bgColor};
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.3);
        }}
        .svg-container svg {{
            max-width: 100%;
            height: auto;
        }}
        .info {{
            color: {infoColor};
            font-family: 'Segoe UI', sans-serif;
            font-size: 12px;
            margin-top: 10px;
        }}
    </style>
</head>
<body>
    <div class='svg-container'>{svgContent}</div>
    <div class='info'>{Path.GetFileName(filePath)} • SVG</div>
</body>
</html>";
            }

            // For other images, use base64
            var bytes = File.ReadAllBytes(filePath);
            var base64 = Convert.ToBase64String(bytes);

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ 
            margin: 0; 
            display: flex; 
            flex-direction: column;
            justify-content: center; 
            align-items: center; 
            min-height: 100vh;
            background: {bgColor};
            background-image: linear-gradient(45deg, {checkPattern} 25%, transparent 25%), 
                              linear-gradient(-45deg, {checkPattern} 25%, transparent 25%), 
                              linear-gradient(45deg, transparent 75%, {checkPattern} 75%), 
                              linear-gradient(-45deg, transparent 75%, {checkPattern} 75%);
            background-size: 20px 20px;
            background-position: 0 0, 0 10px, 10px -10px, -10px 0px;
        }}
        img {{ 
            max-width: 95%; 
            max-height: 85vh;
            object-fit: contain;
            box-shadow: 0 4px 12px rgba(0,0,0,0.3);
            border-radius: 4px;
        }}
        .info {{
            color: {infoColor};
            font-family: 'Segoe UI', sans-serif;
            font-size: 12px;
            margin-top: 10px;
        }}
    </style>
</head>
<body>
    <img src='data:{mimeType};base64,{base64}' alt='Image preview' />
    <div class='info'>{Path.GetFileName(filePath)} • {fileInfo.Length / 1024:N0} KB</div>
</body>
</html>";
        }

        /// <summary>
        /// Gets MIME type for image extension.
        /// </summary>
        private static string GetImageMimeType(string ext)
        {
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".ico" => "image/x-icon",
                ".svg" => "image/svg+xml",
                _ => "image/png"
            };
        }

        /// <summary>
        /// Generates HTML for text/code preview with syntax highlighting.
        /// </summary>
        private string GetTextHtml(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MAX_TEXT_FILE_SIZE)
            {
                return GetFileTooLargeHtml(filePath, "text");
            }

            var content = File.ReadAllText(filePath);
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var language = GetLanguageFromExtension(ext);
            bool isDark = ThemeService.IsDarkMode;

            string bgColor = isDark ? "#1e1e1e" : "#ffffff";
            string textColor = isDark ? "#d4d4d4" : "#333333";
            string lineNumColor = isDark ? "#6e7681" : "#999999";
            string lineNumBg = isDark ? "#161616" : "#f7f7f7";
            string headerBg = isDark ? "#252526" : "#f0f0f0";
            string headerColor = isDark ? "#cccccc" : "#444444";
            string borderColor = isDark ? "#3e3e42" : "#e0e0e0";

            // Escape HTML
            var escapedContent = HtmlEncode(content);

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/{(isDark ? "vs2015" : "vs")}.min.css'>
    <script src='https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js'></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
            background: {bgColor};
            color: {textColor};
            line-height: 1.5;
        }}
        .header {{
            background: {headerBg};
            padding: 10px 15px;
            border-bottom: 1px solid {borderColor};
            font-size: 13px;
            color: {headerColor};
            display: flex;
            justify-content: space-between;
            position: sticky;
            top: 0;
            z-index: 100;
        }}
        .content {{
            padding: 0;
            overflow-x: auto;
        }}
        pre {{
            margin: 0;
            padding: 15px;
            overflow-x: auto;
            tab-size: 4;
        }}
        code {{
            font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
            font-size: 13px;
        }}
        .hljs {{
            background: {bgColor} !important;
            padding: 15px !important;
        }}
        .line-numbers {{
            counter-reset: line;
        }}
        .line-numbers code {{
            counter-increment: line;
        }}
        .line-numbers code::before {{
            content: counter(line);
            display: inline-block;
            width: 3em;
            margin-right: 1em;
            padding-right: 0.5em;
            text-align: right;
            color: {lineNumColor};
            border-right: 1px solid {borderColor};
            user-select: none;
        }}
    </style>
</head>
<body>
    <div class='header'>
        <span>📄 {Path.GetFileName(filePath)}</span>
        <span>{language.ToUpperInvariant()} • {fileInfo.Length / 1024.0:N1} KB • {content.Split('\n').Length} lines</span>
    </div>
    <div class='content'>
        <pre class='line-numbers'><code class='language-{language}'>{escapedContent}</code></pre>
    </div>
    <script>hljs.highlightAll();</script>
</body>
</html>";
        }

        /// <summary>
        /// Maps file extension to highlight.js language identifier.
        /// </summary>
        private static string GetLanguageFromExtension(string ext)
        {
            return ext switch
            {
                ".cs" => "csharp",
                ".py" => "python",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".html" or ".htm" => "html",
                ".css" => "css",
                ".json" => "json",
                ".xml" => "xml",
                ".sql" => "sql",
                ".yaml" or ".yml" => "yaml",
                ".md" => "markdown",
                ".sh" => "bash",
                ".bat" or ".cmd" => "dos",
                ".ps1" => "powershell",
                ".ini" or ".cfg" or ".config" => "ini",
                ".csv" => "plaintext",
                ".log" => "plaintext",
                ".txt" => "plaintext",
                _ => "plaintext"
            };
        }

        /// <summary>
        /// Generates HTML for PDF fallback when pdfjs is not available.
        /// </summary>
        private string GetPdfFallbackHtml(string filePath)
        {
            bool isDark = ThemeService.IsDarkMode;
            var fileInfo = new FileInfo(filePath);

            string bgGradient = isDark
                ? "linear-gradient(135deg, #2d2d30 0%, #1e1e1e 100%)"
                : "linear-gradient(135deg, #667eea 0%, #764ba2 100%)";
            string cardBg = isDark ? "#252526" : "white";
            string textColor = isDark ? "#cccccc" : "#333";

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: 'Segoe UI', sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: {bgGradient};
        }}
        .card {{
            background: {cardBg};
            border-radius: 12px;
            padding: 40px;
            text-align: center;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            max-width: 400px;
        }}
        .icon {{ font-size: 64px; margin-bottom: 20px; }}
        h1 {{ color: {textColor}; margin: 10px 0; font-size: 1.5em; }}
        p {{ color: {textColor}; opacity: 0.8; }}
        .info {{ font-size: 13px; opacity: 0.6; margin-top: 15px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='icon'>📄</div>
        <h1>PDF Document</h1>
        <p><strong>{HtmlEncode(Path.GetFileName(filePath))}</strong></p>
        <p class='info'>{fileInfo.Length / 1024:N0} KB</p>
        <p style='margin-top: 20px; font-size: 14px;'>
            PDF.js viewer not found.<br/>
            Double-click to open in default viewer.
        </p>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Generates HTML for files that are too large to preview.
        /// </summary>
        private string GetFileTooLargeHtml(string filePath, string fileType)
        {
            bool isDark = ThemeService.IsDarkMode;
            var fileInfo = new FileInfo(filePath);

            string bgColor = isDark ? "#1e1e1e" : "#f5f5f5";
            string cardBg = isDark ? "#252526" : "white";
            string textColor = isDark ? "#cccccc" : "#333";
            string warnColor = isDark ? "#f4c771" : "#e65100";

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: 'Segoe UI', sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: {bgColor};
        }}
        .card {{
            background: {cardBg};
            border-radius: 12px;
            padding: 40px;
            text-align: center;
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }}
        .icon {{ font-size: 48px; margin-bottom: 15px; }}
        h2 {{ color: {warnColor}; margin: 10px 0; }}
        p {{ color: {textColor}; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='icon'>⚠️</div>
        <h2>File Too Large</h2>
        <p>This {fileType} file is too large to preview.</p>
        <p><strong>{Path.GetFileName(filePath)}</strong></p>
        <p style='font-size: 14px; opacity: 0.7;'>{fileInfo.Length / (1024 * 1024):N1} MB</p>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Checks if file type is supported for preview.
        /// </summary>
        public bool IsSupportedFileType(string extension)
        {
            var ext = extension?.ToLowerInvariant();
            return ext switch
            {
                // Office documents
                ".docx" or ".doc" or ".xlsx" or ".xls" => true,
                // Images
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".ico" or ".svg" => true,
                // Text and code
                ".txt" or ".md" or ".json" or ".xml" or ".csv" or ".log" => true,
                ".cs" or ".py" or ".js" or ".ts" or ".html" or ".css" or ".sql" => true,
                ".yaml" or ".yml" or ".ini" or ".cfg" or ".config" or ".sh" or ".bat" or ".ps1" => true,
                // PDF
                ".pdf" => true,
                _ => false
            };
        }

        /// <summary>
        /// Simple HTML encoding.
        /// </summary>
        private static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        private string GetUnsupportedHtml(string extension)
        {
            bool isDark = ThemeService.IsDarkMode;
            string bgGradient = isDark
                ? "linear-gradient(135deg, #2d2d30 0%, #1e1e1e 100%)"
                : "linear-gradient(135deg, #667eea 0%, #764ba2 100%)";
            string cardBg = isDark ? "#252526" : "white";
            string titleColor = isDark ? "#cccccc" : "#333";
            string textColor = isDark ? "#999999" : "#666";
            string codeBg = isDark ? "#3e3e42" : "#f5f5f5";
            string codeColor = isDark ? "#f48771" : "#e74c3c";

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: 'Segoe UI', sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: {bgGradient};
        }}
        .message {{
            background: {cardBg};
            border-radius: 12px;
            padding: 40px;
            text-align: center;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
        }}
        .icon {{ font-size: 64px; margin-bottom: 20px; }}
        h1 {{ color: {titleColor}; margin: 10px 0; }}
        p {{ color: {textColor}; }}
        code {{ background: {codeBg}; padding: 4px 8px; border-radius: 4px; color: {codeColor}; }}
    </style>
</head>
<body>
    <div class='message'>
        <div class='icon'>⚠️</div>
        <h1>Unsupported File Type</h1>
        <p>Cannot preview <code>{extension}</code> files</p>
        <p style='font-size: 14px;'>Supported: .docx, .doc, .xlsx, .xls</p>
    </div>
</body>
</html>";
        }

        private UIElement CreateUnsupportedElement(string extension)
        {
            bool isDark = ThemeService.IsDarkMode;

            return new Border
            {
                Background = isDark
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(40),
                Margin = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "⚠️",
                            FontSize = 48,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 15)
                        },
                        new TextBlock
                        {
                            Text = "Unsupported File Type",
                            FontSize = 20,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new System.Windows.Media.SolidColorBrush(isDark
                                ? System.Windows.Media.Color.FromRgb(204, 204, 204)
                                : System.Windows.Media.Color.FromRgb(51, 51, 51)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 10)
                        },
                        new TextBlock
                        {
                            Text = $"Cannot preview {extension} files",
                            FontSize = 14,
                            Foreground = new System.Windows.Media.SolidColorBrush(isDark
                                ? System.Windows.Media.Color.FromRgb(153, 153, 153)
                                : System.Windows.Media.Color.FromRgb(102, 102, 102)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 5)
                        },
                        new TextBlock
                        {
                            Text = "Supported: .docx, .doc, .xlsx, .xls",
                            FontSize = 12,
                            Foreground = new System.Windows.Media.SolidColorBrush(isDark
                                ? System.Windows.Media.Color.FromRgb(120, 120, 120)
                                : System.Windows.Media.Color.FromRgb(150, 150, 150)),
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            };
        }

        private UIElement CreateErrorElement(string message)
        {
            bool isDark = ThemeService.IsDarkMode;

            return new Border
            {
                Background = isDark
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(90, 29, 29))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 235, 238)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 135, 113)),
                BorderThickness = new Thickness(0, 0, 0, 3),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(30),
                Margin = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "⚠️ Preview Error",
                            FontSize = 18,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new System.Windows.Media.SolidColorBrush(isDark
                                ? System.Windows.Media.Color.FromRgb(244, 135, 113)
                                : System.Windows.Media.Color.FromRgb(211, 47, 47)),
                            Margin = new Thickness(0, 0, 0, 10)
                        },
                        new TextBlock
                        {
                            Text = message,
                            FontSize = 14,
                            Foreground = new System.Windows.Media.SolidColorBrush(isDark
                                ? System.Windows.Media.Color.FromRgb(204, 204, 204)
                                : System.Windows.Media.Color.FromRgb(51, 51, 51)),
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 400
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Checks if the file extension is a text or code file that should use native AvalonEdit preview.
        /// </summary>
        private static bool IsTextOrCodeFile(string ext)
        {
            return ext is
                ".txt" or ".md" or ".json" or ".xml" or ".csv" or ".log" or
                ".cs" or ".py" or ".js" or ".ts" or ".html" or ".css" or ".sql" or
                ".yaml" or ".yml" or ".ini" or ".cfg" or ".config" or ".sh" or ".bat" or ".ps1" or
                ".jsx" or ".tsx" or ".java" or ".cpp" or ".c" or ".h" or ".hpp" or
                ".go" or ".rs" or ".rb" or ".php" or ".swift" or ".kt" or ".scala" or
                ".xaml" or ".csproj" or ".sln" or ".props" or ".targets";
        }

        /// <summary>
        /// Creates a native TextPreviewControl using AvalonEdit for text/code files.
        /// Provides better syntax highlighting and accurate search highlighting.
        /// </summary>
        private UIElement CreateTextPreviewControl(string filePath, string searchKeyword)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[PreviewService] Creating TextPreviewControl for: {filePath}");
                var control = new Controls.TextPreviewControl(filePath, searchKeyword);
                System.Diagnostics.Debug.WriteLine("[PreviewService] TextPreviewControl created successfully");
                return control.View;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PreviewService] Error creating TextPreviewControl: {ex.Message}");
                return CreateErrorElement($"Error loading text file: {ex.Message}");
            }
        }

        public Task<UIElement> CreatePreviewAsync(string filePath)
        {
            return CreatePreviewAsync(filePath, null);
        }

        public void Dispose()
        {
            // Nothing to dispose - WebView2 instances are owned by the visual tree
        }
    }
}
