using DamYou.Data;
using DamYou.Data.Analysis;
using DamYou.Data.Import;
using DamYou.Data.Pipeline;
using DamYou.Data.Repositories;
using DamYou.Services;
using DamYou.ViewModels;
using DamYou.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DamYou;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var logger = LoggingService.GetLogger();

        logger.Information("=== DAMu App Initialization Started ===");

        var builder = MauiApp.CreateBuilder();
#pragma warning disable HAA0301 // Closure Allocation Source
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
#pragma warning restore HAA0301 // Closure Allocation Source

        // Database
        logger.Debug("Initializing database context...");
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DamYou",
            "dam-you.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        logger.Debug("Database path: {DbPath}", dbPath);

#pragma warning disable HAA0301 // Closure Allocation Source
        builder.Services.AddDbContext<DamYouDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));
#pragma warning restore HAA0301 // Closure Allocation Source

        // Repositories
        builder.Services.AddScoped<IFolderRepository, FolderRepository>();
        builder.Services.AddScoped<IPhotoRepository, PhotoRepository>();
        builder.Services.AddScoped<IPhotoFolderRepository, PhotoFolderRepository>();

        // Import
        builder.Services.AddScoped<IPhotoImportService, PhotoImportService>();

        // Pipeline
        builder.Services.AddScoped<IPipelineTaskRepository, PipelineTaskRepository>();
        builder.Services.AddScoped<ILibraryScanService, LibraryScanService>();

        // Services
        builder.Services.AddSingleton<IFolderPickerService, FolderPickerService>();
        builder.Services.AddSingleton<IQueueSettings, DefaultQueueSettings>();

        // Analysis services (singletons — expensive ONNX sessions)
        builder.Services.AddSingleton<IHardwareDetectionService, HardwareDetectionService>();
        builder.Services.AddSingleton<IModelManagerService, ModelManagerService>();
        builder.Services.AddSingleton<IClipService, ClipService>();
        builder.Services.AddSingleton<IYoloDetectionService, YoloDetectionService>();
        builder.Services.AddSingleton<IOcrService, WindowsOcrService>();
        builder.Services.AddSingleton<IDistilBertService, DistilBertService>();
        builder.Services.AddSingleton<IColorExtractionService, ColorExtractionService>();

        // Orchestration (scoped — shares DbContext)
        builder.Services.AddScoped<IPhotoAnalysisService, PhotoAnalysisService>();
        builder.Services.AddScoped<IPipelineProcessorService, PipelineProcessorService>();

        // ViewModels
        builder.Services.AddTransient<LibrarySetupViewModel>();
        builder.Services.AddTransient<ManageFoldersViewModel>();
        builder.Services.AddTransient<GalleryViewModel>();
        builder.Services.AddTransient<FoldersViewModel>();
        builder.Services.AddTransient<WorkQueueViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<IProcessingStateService, ProcessingStateService>(); // Event broadcaster for processing state
        builder.Services.AddSingleton<IImportProgressService, ImportProgressService>(); // Event broadcaster for import progress
        builder.Services.AddSingleton<ProcessingStateViewModel>(); // UI state ViewModel

        // Background processing
        builder.Services.AddLogging(c => c.AddDebug());
        builder.Services.AddSingleton<IProcessingWorker, ProcessingHostedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ProcessingHostedService>());

        // Dedicated file processor (optional, runs independently from folder scanning)
        builder.Services.AddHostedService<DedicatedFileProcessorService>();

        // Multi-queue processor (folder scan queue + file processing queue)
        builder.Services.AddSingleton<IFolderQueueService, FolderQueueService>();
        builder.Services.AddSingleton<IFileQueueService, FileQueueService>();
        builder.Services.AddSingleton<IQueueSettingsService>(_ => new QueueSettingsService());
        builder.Services.AddSingleton<QueueStatusViewModel>();
        builder.Services.AddHostedService<QueueProcessorService>();

        // Views
        builder.Services.AddTransient<SplashScreenView>();
        builder.Services.AddTransient<LibrarySetupModal>();
        builder.Services.AddTransient<ManageFoldersModal>();
        builder.Services.AddTransient<GalleryView>();
        builder.Services.AddTransient<FoldersView>();
        builder.Services.AddTransient<WorkQueueView>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddSingleton<StatusBar>(); // Status bar (singleton for global state binding)
        builder.Services.AddSingleton<AppShell>();

        // App
        builder.Services.AddSingleton<App>();

        builder.Services.BuildServiceProvider(new ServiceProviderOptions()
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Run migrations on startup
        logger.Debug("Running database migrations...");
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();
            db.Database.Migrate();
            logger.Information("Database migrations completed successfully.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Database migration failed");
            throw;
        }

        // Register shell routes for DI-aware navigation
        logger.Debug("Registering shell routes...");
        Routing.RegisterRoute("gallery", typeof(GalleryView));
        Routing.RegisterRoute("folders", typeof(FoldersView));
        Routing.RegisterRoute("work-queue", typeof(WorkQueueView));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        logger.Information("=== MauiProgram initialization completed ===");

        return app;
    }
}
