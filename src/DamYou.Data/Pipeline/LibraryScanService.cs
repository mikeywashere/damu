using DamYou.Data.Analysis;
using DamYou.Data.Entities;
using DamYou.Data.Repositories;

namespace DamYou.Data.Pipeline;

public sealed class LibraryScanService : ILibraryScanService
{
    private readonly DamYouDbContext _db;
    private readonly IFolderRepository _folderRepository;
    private readonly IPipelineTaskRepository _taskRepository;
    private readonly IPipelineProcessorService _pipelineProcessor;

    public LibraryScanService(
        DamYouDbContext db,
        IFolderRepository folderRepository,
        IPipelineTaskRepository taskRepository,
        IPipelineProcessorService pipelineProcessor)
    {
        _db = db;
        _folderRepository = folderRepository;
        _taskRepository = taskRepository;
        _pipelineProcessor = pipelineProcessor;
    }

    public async Task ScanAsync(IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        foreach (var folder in _db.WatchedFolders.ToList())
        {
            if (_db.QueuedFolders.Where(q => q.FolderPath == folder.Path).Any())
                continue;
            QueuedFolder searchFolder = new()
            {
                AddedAt = DateTime.UtcNow,
                FolderPath = folder.Path,
                Id = Guid.NewGuid().ToString("n"),
                Priority = 0,
                Status = QueueStatus.Pending
            };
            _db.QueuedFolders.Add(searchFolder);
            await _db.SaveChangesAsync(ct);
        }
    }
}
