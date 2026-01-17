using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using OfflineProjectManager.Logging;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Pool of WebView2 instances for reuse to prevent memory leaks
    /// </summary>
    public class WebView2Pool : IDisposable
    {
        private readonly ConcurrentBag<WebView2> _pool = new();
        private readonly int _maxPoolSize;
        private static Microsoft.Web.WebView2.Core.CoreWebView2Environment _sharedEnv;
        private static readonly SemaphoreSlim _envLock = new SemaphoreSlim(1, 1);
        private int _totalCreated = 0;

        public WebView2Pool(int maxPoolSize = 3)
        {
            _maxPoolSize = maxPoolSize;
        }

        private static async Task<Microsoft.Web.WebView2.Core.CoreWebView2Environment> GetSharedEnvironmentAsync()
        {
            if (_sharedEnv != null) return _sharedEnv;

            await _envLock.WaitAsync();
            try
            {
                if (_sharedEnv != null) return _sharedEnv;

                PreviewLogger.LogInfo("[WebView2Pool] Helper: Creating Shared WebView2 Environment...");
                var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OfflineProjectManager_WebView2");
                System.IO.Directory.CreateDirectory(tempDir);

                // Create environment with default options
                _sharedEnv = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(userDataFolder: tempDir);
                PreviewLogger.LogInfo("[WebView2Pool] Shared Environment Created Successfully.");
                return _sharedEnv;
            }
            finally
            {
                _envLock.Release();
            }
        }

        /// <summary>
        /// Get a WebView2 from pool or create new one
        /// </summary>
        public async Task<WebView2> GetOrCreateAsync()
        {
            if (_pool.TryTake(out var webView))
            {
                PreviewLogger.LogInfo("[WebView2Pool] Reusing from pool (Count: {0})", _pool.Count);
                return webView;
            }

            // Create new on UI Thread
            PreviewLogger.LogInfo("[WebView2Pool] Pool empty. Creating new (Total Global: {0})", _totalCreated + 1);

            WebView2 newWebView = null;

            // Create and Initialize on UI Thread
            await await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                newWebView = new WebView2();
                try
                {
                    // Use Shared Environment
                    var env = await GetSharedEnvironmentAsync();

                    PreviewLogger.LogInfo("[WebView2Pool] Initializing CoreWebView2 with Shared Env...");

                    // Init with robust Timeout (increased to 30s)
                    var initTask = newWebView.EnsureCoreWebView2Async(env);
                    if (await Task.WhenAny(initTask, Task.Delay(30000)) != initTask)
                    {
                        // Timed out
                        PreviewLogger.LogError(null, "[WebView2Pool] Initialization TIMED OUT.");
                        newWebView.Dispose();
                        newWebView = null; // Signal failure
                    }
                    else
                    {
                        await initTask;

                        // Verification
                        if (newWebView.CoreWebView2 == null)
                        {
                            PreviewLogger.LogError(null, "[WebView2Pool] CoreWebView2 IS NULL after sync wait!");
                            newWebView.Dispose();
                            newWebView = null;
                        }
                        else
                        {
                            _totalCreated++;
                            PreviewLogger.LogInfo("[WebView2Pool] Helper: Initialization Success. Total created: {0}", _totalCreated);
                        }
                    }
                }
                catch (Exception ex)
                {
                    PreviewLogger.LogError(ex, "[WebView2Pool] Initialization Exception");
                    newWebView?.Dispose();
                    newWebView = null;
                    throw; // Propagate up
                }
            });

            if (newWebView == null)
            {
                throw new TimeoutException("WebView2 initialization timed out (30s).");
            }

            return newWebView;
        }

        /// <summary>
        /// Return WebView2 to pool for reuse
        /// </summary>
        public void Return(WebView2 webView)
        {
            if (webView == null) return;

            try
            {
                // Clear state before returning to pool
                if (webView.CoreWebView2 != null)
                {
                    webView.Source = null;
                }

                // Add to pool if not full
                if (_pool.Count < _maxPoolSize)
                {
                    _pool.Add(webView);
                    System.Diagnostics.Debug.WriteLine("[WebView2Pool] Returned to pool");
                }
                else
                {
                    // Pool full - dispose excess
                    webView.Dispose();
                    System.Diagnostics.Debug.WriteLine("[WebView2Pool] Pool full, disposed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebView2Pool] Error returning: {ex.Message}");
                webView?.Dispose();
            }
        }

        public void Dispose()
        {
            System.Diagnostics.Debug.WriteLine($"[WebView2Pool] Disposing pool with {_pool.Count} instances");

            while (_pool.TryTake(out var webView))
            {
                try
                {
                    webView.Dispose();
                }
                catch { /* Ignore disposal errors */ }
            }
        }
    }
}
