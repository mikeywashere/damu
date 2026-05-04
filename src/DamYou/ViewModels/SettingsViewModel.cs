using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Services;

namespace DamYou.ViewModels;

/// <summary>
/// ViewModel for the Settings page. Loads/saves queue processing configuration
/// via IQueueSettings. Parker's multi-queue implementation provides the concrete service.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IQueueSettings _queueSettings;
    private readonly Action<Action> _dispatcher;

    [ObservableProperty]
    private string queueWaitTimeSeconds = string.Empty;

    public SettingsViewModel(IQueueSettings queueSettings, Action<Action>? dispatcher = null)
    {
        _queueSettings = queueSettings;
        _dispatcher = dispatcher ?? MainThread.BeginInvokeOnMainThread;
        LoadSettings();
    }

    private void LoadSettings()
    {
        int seconds = _queueSettings.GetQueueWaitTimeMs() / 1000;
        QueueWaitTimeSeconds = seconds.ToString();
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
}
