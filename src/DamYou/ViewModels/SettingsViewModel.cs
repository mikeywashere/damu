using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Services;
using Microsoft.Maui.Storage;

namespace DamYou.ViewModels;

/// <summary>
/// ViewModel for the Settings page. Loads/saves queue processing configuration
/// via IQueueSettings. Parker's multi-queue implementation provides the concrete service.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IQueueSettings _queueSettings;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IPreferences _preferences;
    private readonly Action<Action> _dispatcher;

    private const string VerboseLoggingKey = "verbose_logging_enabled";
    private const string LogFolderPathKey = "log_folder_path";

    [ObservableProperty]
    private string queueWaitTimeSeconds = string.Empty;

    [ObservableProperty]
    private bool isVerboseLoggingEnabled;

    [ObservableProperty]
    private string logFolderPath = string.Empty;

    public SettingsViewModel(
        IQueueSettings queueSettings,
        IFolderPickerService folderPickerService,
        Action<Action>? dispatcher = null,
        IPreferences? preferences = null)
    {
        _queueSettings = queueSettings;
        _folderPickerService = folderPickerService;
        _dispatcher = dispatcher ?? MainThread.BeginInvokeOnMainThread;
        _preferences = preferences ?? Preferences.Default;
        LoadSettings();
    }

    private void LoadSettings()
    {
        int seconds = _queueSettings.GetQueueWaitTimeMs() / 1000;
        QueueWaitTimeSeconds = seconds.ToString();
        
        IsVerboseLoggingEnabled = _preferences.Get(VerboseLoggingKey, false);
        LogFolderPath = _preferences.Get(LogFolderPathKey, string.Empty);
    }

    [RelayCommand]
    private void SaveSettings()
    {
        if (int.TryParse(QueueWaitTimeSeconds, out int seconds))
        {
            if (seconds < 1) seconds = 1;
            _queueSettings.SetQueueWaitTimeMs(seconds * 1000);
            QueueWaitTimeSeconds = seconds.ToString();

            _dispatcher(async () =>
            {
                if (Application.Current?.MainPage is not null)
                    await Application.Current.MainPage.DisplayAlert(
                        "Saved",
                        $"Queue wait time set to {seconds} second{(seconds == 1 ? "" : "s")}",
                        "OK");
            });
        }
    }

    partial void OnIsVerboseLoggingEnabledChanged(bool value)
    {
        _preferences.Set(VerboseLoggingKey, value);
    }

    [RelayCommand]
    private async Task PickLogFolderAsync()
    {
        var path = await _folderPickerService.PickFolderAsync();
        if (path is not null)
        {
            LogFolderPath = path;
            _preferences.Set(LogFolderPathKey, path);

            _dispatcher(async () =>
            {
                if (Application.Current?.MainPage is not null)
                    await Application.Current.MainPage.DisplayAlert(
                        "Folder Selected",
                        $"Log folder set to:\n{path}",
                        "OK");
            });
        }
    }
}
