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
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Database
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DamYou",
            "dam-you.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        builder.Services.AddDbContext<DamYouDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

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
        builder.Services.AddTransient<LibraryViewModel>();
        builder.Services.AddTransient<ManageFoldersViewModel>();
        builder.Services.AddTransient<TasksViewModel>();

        // Views
        builder.Services.AddTransient<LibrarySetupView>();
        builder.Services.AddTransient<LibraryView>();
        builder.Services.AddTransient<ManageFoldersModal>();
        builder.Services.AddTransient<TasksView>();

        // App
        builder.Services.AddSingleton<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Run migrations on startup
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();
        db.Database.Migrate();

        return app;
    }
}
