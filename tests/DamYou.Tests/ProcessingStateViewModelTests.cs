using DamYou.Data.Analysis;
using DamYou.Services;
using DamYou.ViewModels;
using Moq;
using Xunit;

namespace DamYou.Tests;

/// <summary>
/// Tests for ProcessingStateViewModel — property updates and event handling.
/// 
/// Verifies:
/// - ObservableProperty updates trigger PropertyChanged
/// - ProgressText is computed correctly
/// - Event subscriptions marshal to MainThread (UI thread safety)
/// - ProcessingStarted, ProgressReported, and ProcessingStopped events work correctly
/// </summary>
public class ProcessingStateViewModelTests
{
    private readonly ProcessingStateViewModel _viewModel;
    private readonly Mock<IProcessingWorker> _processingWorkerMock;
    private readonly Mock<IProcessingStateService> _processingStateServiceMock;

    public ProcessingStateViewModelTests()
    {
        _processingWorkerMock = new Mock<IProcessingWorker>();
        _processingStateServiceMock = new Mock<IProcessingStateService>();
        _viewModel = new ProcessingStateViewModel(_processingWorkerMock.Object, _processingStateServiceMock.Object);
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
    public void StartProcessing_Event_Should_Set_IsProcessing_True()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ProcessingStateViewModel.IsProcessing))
                propertyChangedRaised = true;
        };

        // Act
        _processingStateServiceMock.Raise(x => x.ProcessingStarted += null, 42);

        // Assert (allow time for MainThread marshaling)
        System.Threading.Thread.Sleep(50);
        Assert.True(_viewModel.IsProcessing);
        Assert.Equal(42, _viewModel.TotalItems);
        Assert.Equal(0, _viewModel.CurrentProgress);
        Assert.Contains("Processing", _viewModel.StatusText);
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void StopProcessing_Event_Should_Set_IsProcessing_False()
    {
        // Arrange
        _processingStateServiceMock.Raise(x => x.ProcessingStarted += null, 42);
        System.Threading.Thread.Sleep(50); // Wait for main thread marshaling
        
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ProcessingStateViewModel.IsProcessing))
                propertyChangedRaised = true;
        };

        // Act
        _processingStateServiceMock.Raise(x => x.ProcessingStopped += null);

        // Assert
        System.Threading.Thread.Sleep(50); // Wait for main thread marshaling
        Assert.False(_viewModel.IsProcessing);
        Assert.Equal("Complete", _viewModel.StatusText);
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void ProgressReported_Event_Should_Update_Progress_Properties()
    {
        // Arrange
        _processingStateServiceMock.Raise(x => x.ProcessingStarted += null, 100);
        System.Threading.Thread.Sleep(50); // Wait for main thread marshaling
        
        var progress = new AnalysisProgress(
            Total: 100,
            Completed: 25,
            CurrentFile: "/path/to/photo.jpg",
            CurrentPass: "CLIP Embedding"
        );

        // Act
        _processingStateServiceMock.Raise(x => x.ProgressReported += null, progress);

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
        _processingStateServiceMock.Raise(x => x.ProcessingStarted += null, 50);
        System.Threading.Thread.Sleep(50); // Wait for main thread marshaling

        // Act
        _processingStateServiceMock.Raise(x => x.ProgressReported += null, 
            new AnalysisProgress(Total: 50, Completed: 10, null, null));

        // Assert
        System.Threading.Thread.Sleep(50);
        Assert.Equal("10/50", _viewModel.ProgressText);
    }

    [Fact]
    public void ProgressReported_Without_CurrentFile_Should_Use_Pass_Name()
    {
        // Arrange
        _processingStateServiceMock.Raise(x => x.ProcessingStarted += null, 10);
        System.Threading.Thread.Sleep(50); // Wait for main thread marshaling
        
        var progress = new AnalysisProgress(
            Total: 10,
            Completed: 3,
            CurrentFile: null,
            CurrentPass: "YOLO Detection"
        );

        // Act
        _processingStateServiceMock.Raise(x => x.ProgressReported += null, progress);

        // Assert
        System.Threading.Thread.Sleep(50);
        Assert.Contains("YOLO Detection", _viewModel.StatusText);
    }
}
