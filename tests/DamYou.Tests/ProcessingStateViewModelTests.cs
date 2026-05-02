using DamYou.Data.Analysis;
using DamYou.Services;
using DamYou.ViewModels;
using Moq;
using Xunit;

namespace DamYou.Tests;

/// <summary>
/// Tests for ProcessingStateViewModel — property updates and IProgress handling.
/// 
/// Verifies:
/// - ObservableProperty updates trigger PropertyChanged
/// - ProgressText is computed correctly
/// - ReportProgress marshals to MainThread (UI thread safety)
/// - StartProcessing and StopProcessing work correctly
/// </summary>
public class ProcessingStateViewModelTests
{
    private readonly ProcessingStateViewModel _viewModel;
    private readonly Mock<IProcessingWorker> _processingWorkerMock;

    public ProcessingStateViewModelTests()
    {
        _processingWorkerMock = new Mock<IProcessingWorker>();
        _viewModel = new ProcessingStateViewModel(_processingWorkerMock.Object);
    }

    [Fact]
    public void Initial_State_Should_Be_Idle()
    {
        // Arrange & Act & Assert
        Assert.False(_viewModel.IsProcessing);
        Assert.Equal(0, _viewModel.CurrentProgress);
        Assert.Equal(0, _viewModel.TotalItems);
        Assert.Equal("Ready", _viewModel.StatusText);
        Assert.Equal("Idle", _viewModel.ProgressText);
    }

    [Fact]
    public void StartProcessing_Should_Set_IsProcessing_True()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ProcessingStateViewModel.IsProcessing))
                propertyChangedRaised = true;
        };

        // Act
        _viewModel.StartProcessing(42);

        // Assert
        Assert.True(_viewModel.IsProcessing);
        Assert.Equal(42, _viewModel.TotalItems);
        Assert.Equal(0, _viewModel.CurrentProgress);
        Assert.Contains("Processing", _viewModel.StatusText);
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void StopProcessing_Should_Set_IsProcessing_False()
    {
        // Arrange
        _viewModel.StartProcessing(42);
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ProcessingStateViewModel.IsProcessing))
                propertyChangedRaised = true;
        };

        // Act
        _viewModel.StopProcessing();

        // Assert
        Assert.False(_viewModel.IsProcessing);
        Assert.Equal("Complete", _viewModel.StatusText);
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void ReportProgress_Should_Update_Progress_Properties()
    {
        // Arrange
        _viewModel.StartProcessing(100);
        var progress = new AnalysisProgress(
            Total: 100,
            Completed: 25,
            CurrentFile: "/path/to/photo.jpg",
            CurrentPass: "CLIP Embedding"
        );

        // Act
        _viewModel.ReportProgress(progress);

        // Assert (allow a small delay for MainThread marshaling)
        System.Threading.Thread.Sleep(50);
        Assert.Equal(25, _viewModel.CurrentProgress);
        Assert.Equal(100, _viewModel.TotalItems);
        Assert.Contains("photo.jpg", _viewModel.StatusText);
    }

    [Fact]
    public void ProgressText_Should_Update_When_Progress_Changes()
    {
        // Arrange
        _viewModel.StartProcessing(50);

        // Act
        _viewModel.ReportProgress(new(Total: 50, Completed: 10, null, null));

        // Assert
        System.Threading.Thread.Sleep(50);
        Assert.Equal("10/50", _viewModel.ProgressText);
    }

    [Fact]
    public void ReportProgress_Without_CurrentFile_Should_Use_Pass_Name()
    {
        // Arrange
        _viewModel.StartProcessing(10);
        var progress = new AnalysisProgress(
            Total: 10,
            Completed: 3,
            CurrentFile: null,
            CurrentPass: "YOLO Detection"
        );

        // Act
        _viewModel.ReportProgress(progress);

        // Assert
        System.Threading.Thread.Sleep(50);
        Assert.Contains("YOLO Detection", _viewModel.StatusText);
    }
}
