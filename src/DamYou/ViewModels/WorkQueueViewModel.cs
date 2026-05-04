using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Data.Entities;
using DamYou.Services;
using System.Collections.ObjectModel;

namespace DamYou.ViewModels;

public sealed partial class WorkQueueViewModel : ObservableObject
{
    private readonly IFolderQueueService _folderQueue;
    private readonly IFileQueueService _fileQueue;
    private CancellationTokenSource? _refreshCts;

    public ObservableCollection<QueuedFolder> QueuedFolders { get; } = new();
    public ObservableCollection<QueuedFile> QueuedFiles { get; } = new();

    public bool HasFolders => QueuedFolders.Count > 0;
    public bool HasFiles => QueuedFiles.Count > 0;
    public bool IsEmpty => QueuedFolders.Count == 0 && QueuedFiles.Count == 0;

    public WorkQueueViewModel(IFolderQueueService folderQueue, IFileQueueService fileQueue)
    {
        _folderQueue = folderQueue;
        _fileQueue = fileQueue;
        QueuedFolders.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasFolders));
            OnPropertyChanged(nameof(IsEmpty));
        };
        QueuedFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasFiles));
            OnPropertyChanged(nameof(IsEmpty));
        };
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await LoadQueuesAsync();

        _refreshCts = new CancellationTokenSource();
        _ = RefreshPeriodicAsync(_refreshCts.Token);
    }

    [RelayCommand]
    private async Task LoadQueuesAsync(CancellationToken ct = default)
    {
        try
        {
            var folders = await _folderQueue.GetActiveItemsAsync(ct);
            var files = await _fileQueue.GetActiveItemsAsync(ct);

            QueuedFolders.Clear();
            foreach (var folder in folders)
                QueuedFolders.Add(folder);

            QueuedFiles.Clear();
            foreach (var file in files)
                QueuedFiles.Add(file);
        }
        catch (OperationCanceledException)
        {
            // expected on cancellation
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading work queues: {ex.Message}");
        }
    }

    private async Task RefreshPeriodicAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(2000, ct);
                await LoadQueuesAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // expected when view is unloaded
        }
    }

    public void Cleanup()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
    }
}
