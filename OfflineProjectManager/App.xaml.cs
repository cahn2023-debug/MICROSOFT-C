using System.Windows;
using OfflineProjectManager.Services;
using OfflineProjectManager.Services.FileParsers;
using OfflineProjectManager.ViewModels;
using OfflineProjectManager.Views;
using OfflineProjectManager.Models;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using OfflineProjectManager.Data;
using Microsoft.EntityFrameworkCore;
using OfflineProjectManager.Logging;

namespace OfflineProjectManager
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        static App()
        {
            // Fix: Menu Drop Alignment for Left-Handed (Tablet) logic on Windows
            try
            {
                var menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                menuDropAlignmentField?.SetValue(null, false);
            }
            catch { }

            // Register Syncfusion Community License
            // User must replace this with their valid key from https://www.syncfusion.com/
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("YOUR_COMMUNITY_LICENSE_KEY_HERE");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Phase 3: Initialize logging infrastructure
            LoggingConfiguration.Initialize();

            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            this.DispatcherUnhandledException += (s, exArgs) =>
            {
                // Log the error before showing UI
                PreviewLogger.LogPreviewFailed(exArgs.Exception, "GlobalHandler", "N/A", "N/A");
                System.Diagnostics.Debug.WriteLine($"[FATAL] Unhandled Exception: {exArgs.Exception}");

                System.Windows.MessageBox.Show(
                    $"An unexpected error occurred:\n\n{exArgs.Exception.Message}\n\nPlease check the logs for details.",
                    "Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                exArgs.Handled = true;
                Shutdown(1);
            };

            try
            {
                MainViewModel mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();

                MainWindow window = new() { DataContext = mainViewModel };
                window.Show();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString(), "Startup Error");
                Shutdown(1);
            }

            // Hook up application shutdown
            this.Exit += OnApplicationExit;
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            var cancellationManager = ServiceProvider?.GetService<ICancellationManager>();
            if (cancellationManager is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // Phase 4: Dispose preview manager to cleanup WebView2 and cache
            var previewManager = ServiceProvider?.GetService<IPreviewService>();
            if (previewManager is IDisposable previewDisposable)
            {
                previewDisposable.Dispose();
            }

            // Phase 3: Shutdown logging
            LoggingConfiguration.Shutdown();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Infrastructure
            var dbContextPool = new Data.DbContextPool();
            services.AddSingleton(dbContextPool);

            var cancellationManager = new CancellationManager();
            services.AddSingleton<ICancellationManager>(cancellationManager);

            services.AddSingleton<IProjectService, OfflineProjectManager.Features.Project.Services.ProjectService>();
            services.AddSingleton<IIndexerService, IndexerService>();

            // File Parsers
            services.AddSingleton<IFileParserRegistry>(sp =>
            {
                var parsers = new IFileParser[]
                {
                    new TextFileParser(),
                    new PdfFileParser(),
                    new DocxFileParser(),
                    new ExcelFileParser(),
                    new DwgFileParser()
                };
                return new FileParserRegistry(parsers);
            });

            services.AddSingleton<IContentIndexService, ContentIndexService>();
            services.AddSingleton<ISearchService, SearchService>();
            services.AddSingleton<IPreviewService, OfflineProjectManager.Features.Preview.Services.PreviewService>();
            services.AddSingleton<ITaskService, OfflineProjectManager.Features.Task.Services.TaskService>();

            // Vietnamese text normalization and highlight services
            services.AddSingleton<IVietnameseTextNormalizer, VietnameseTextNormalizerService>();
            services.AddTransient<IHighlightService, HighlightService>();

            // Phase 3: Metadata extraction and thumbnail services
            services.AddSingleton<IMetadataExtractorService, MetadataExtractorService>();
            services.AddSingleton<IThumbnailCacheService, ThumbnailCacheService>();

            // Blazor Hybrid Support
            services.AddWpfBlazorWebView();

            // MediatR (Vertical Slice Handlers)
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(App).Assembly));

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<GanttChartViewModel>();
        }
    }
}
