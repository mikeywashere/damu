using DamYou.Data;
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

        // Import
        builder.Services.AddScoped<IPhotoImportService, PhotoImportService>();

        // Pipeline
        builder.Services.AddScoped<IPipelineTaskRepository, PipelineTaskRepository>();
        builder.Services.AddScoped<ILibraryScanService, LibraryScanService>();

        // Services
        builder.Services.AddSingleton<IFolderPickerService, FolderPickerService>();

        // ViewModels
        builder.Services.AddTransient<LibrarySetupViewModel>();
        builder.Services.AddTransient<LibraryViewModel>();

        // Views
        builder.Services.AddTransient<LibrarySetupView>();
        builder.Services.AddTransient<LibraryView>();

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
