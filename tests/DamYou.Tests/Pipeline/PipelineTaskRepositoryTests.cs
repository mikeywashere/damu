using DamYou.Data;
using DamYou.Data.Entities;
using DamYou.Data.Pipeline;
using Microsoft.EntityFrameworkCore;

namespace DamYou.Tests.Pipeline;

public sealed class PipelineTaskRepositoryTests : IDisposable
{
    private readonly DamYouDbContext _db;
    private readonly PipelineTaskRepository _sut;

    public PipelineTaskRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DamYouDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DamYouDbContext(options);
        _sut = new PipelineTaskRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task AddTaskDirectlyAsync(PipelineTaskStatus status, string name = "Test Task")
    {
        _db.PipelineTasks.Add(new PipelineTask { TaskName = name, Status = status });
        await _db.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task EnqueueAsync_CreatesTask_WithQueuedStatus()
    {
        var task = await _sut.EnqueueAsync("Test Task", null, CancellationToken.None);

        Assert.Equal(PipelineTaskStatus.Queued, task.Status);
    }

    [Fact]
    public async Task EnqueueAsync_CreatesTask_WithCorrectTaskName()
    {
        var task = await _sut.EnqueueAsync("Scan Library", null, CancellationToken.None);

        Assert.Equal("Scan Library", task.TaskName);
    }

    [Fact]
    public async Task EnqueueAsync_WithPhotoId_SetsPhotoId()
    {
        var task = await _sut.EnqueueAsync("Process Photo", photoId: 42, CancellationToken.None);

        Assert.Equal(42, task.PhotoId);
    }

    [Fact]
    public async Task EnqueueAsync_WithoutPhotoId_PhotoIdIsNull()
    {
        var task = await _sut.EnqueueAsync("Scan Library", null, CancellationToken.None);

        Assert.Null(task.PhotoId);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToRunning_SetsStartedAt()
    {
        var task = await _sut.EnqueueAsync("Test Task", null, CancellationToken.None);

        await _sut.UpdateStatusAsync(task.Id, PipelineTaskStatus.Running, null, CancellationToken.None);

#pragma warning disable HAA0101 // Array allocation for params parameter
        var updated = await _db.PipelineTasks.FindAsync([task.Id], CancellationToken.None);
#pragma warning restore HAA0101 // Array allocation for params parameter
        Assert.NotNull(updated!.StartedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToCompleted_SetsCompletedAt()
    {
        var task = await _sut.EnqueueAsync("Test Task", null, CancellationToken.None);

        await _sut.UpdateStatusAsync(task.Id, PipelineTaskStatus.Completed, null, CancellationToken.None);

#pragma warning disable xUnit1051 // Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken
#pragma warning disable HAA0101 // Array allocation for params parameter
        var updated = await _db.PipelineTasks.FindAsync([task.Id], CancellationToken.None);
#pragma warning restore HAA0101 // Array allocation for params parameter
#pragma warning restore xUnit1051 // Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken
        Assert.NotNull(updated!.CompletedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToFailed_SetsCompletedAtAndErrorMessage()
    {
        var task = await _sut.EnqueueAsync("Test Task", null, CancellationToken.None);
        const string errorMessage = "Something went wrong";

        await _sut.UpdateStatusAsync(task.Id, PipelineTaskStatus.Failed, errorMessage, CancellationToken.None);

#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
#pragma warning disable xUnit1051 // Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken
#pragma warning disable HAA0101 // Array allocation for params parameter
        var updated = await _db.PipelineTasks.FindAsync(task.Id);
#pragma warning restore HAA0101 // Array allocation for params parameter
#pragma warning restore xUnit1051 // Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken
#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation
        Assert.NotNull(updated!.CompletedAt);
        Assert.Equal(errorMessage, updated.ErrorMessage);
    }

    [Fact]
    public async Task GetActiveTasksAsync_ReturnsQueuedAndRunning()
    {
        await AddTaskDirectlyAsync(PipelineTaskStatus.Queued, "Queued Task");
        await AddTaskDirectlyAsync(PipelineTaskStatus.Running, "Running Task");
        await AddTaskDirectlyAsync(PipelineTaskStatus.Completed, "Completed Task");
        await AddTaskDirectlyAsync(PipelineTaskStatus.Failed, "Failed Task");

        var active = await _sut.GetActiveTasksAsync(CancellationToken.None);

        Assert.Equal(2, active.Count);
        Assert.All(active, t =>
            Assert.True(t.Status == PipelineTaskStatus.Queued || t.Status == PipelineTaskStatus.Running));
    }

    [Fact]
    public async Task GetActiveTasksAsync_ExcludesCompletedAndFailed()
    {
        await AddTaskDirectlyAsync(PipelineTaskStatus.Completed, "Completed Task");
        await AddTaskDirectlyAsync(PipelineTaskStatus.Failed, "Failed Task");

        var active = await _sut.GetActiveTasksAsync(CancellationToken.None);

        Assert.Empty(active);
    }

    [Fact]
    public async Task GetQueuedTasksAsync_ReturnsOnlyQueued()
    {
        await AddTaskDirectlyAsync(PipelineTaskStatus.Queued, "Queued Task");
        await AddTaskDirectlyAsync(PipelineTaskStatus.Running, "Running Task");
        await AddTaskDirectlyAsync(PipelineTaskStatus.Completed, "Completed Task");

        var queued = await _sut.GetQueuedTasksAsync(CancellationToken.None);

        Assert.Single(queued);
        Assert.Equal("Queued Task", queued[0].TaskName);
    }

    [Fact]
    public async Task GetQueueDepthAsync_ReturnsCountOfQueuedTasks()
    {
        await AddTaskDirectlyAsync(PipelineTaskStatus.Queued, "AddTaskDirectlyAsync(PipelineTaskStatus.Queued)");
        await AddTaskDirectlyAsync(PipelineTaskStatus.Queued, "AddTaskDirectlyAsync(PipelineTaskStatus.Queued)");
        await AddTaskDirectlyAsync(PipelineTaskStatus.Queued, "AddTaskDirectlyAsync(PipelineTaskStatus.Queued)");
        await AddTaskDirectlyAsync(PipelineTaskStatus.Running, "AddTaskDirectlyAsync(PipelineTaskStatus.Running)");
        await AddTaskDirectlyAsync(PipelineTaskStatus.Running, "AddTaskDirectlyAsync(PipelineTaskStatus.Running)");
        await AddTaskDirectlyAsync(PipelineTaskStatus.Completed, "AddTaskDirectlyAsync(PipelineTaskStatus.Completed)");

        var depth = await _sut.GetQueueDepthAsync(CancellationToken.None);

        Assert.Equal(3, depth);
    }

    [Fact]
    public async Task GetQueueDepthAsync_ReturnsZero_WhenNoQueuedTasks()
    {
        var depth = await _sut.GetQueueDepthAsync(CancellationToken.None);

        Assert.Equal(0, depth);
    }
}
