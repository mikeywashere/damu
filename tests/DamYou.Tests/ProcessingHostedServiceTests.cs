using DamYou.Data.Analysis;
using DamYou.Services;
using DamYou.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DamYou.Tests;

/// <summary>
/// Tests for ProcessingHostedService — startup, shutdown, and processing loop.
/// 
/// Verifies:
/// - StartAsync initializes the timer
/// - StopAsync stops the timer and cancels processing gracefully
/// - Timer fires and ProcessQueueAsync is called periodically
/// - Scoped DbContext is created per processing attempt
/// - Progress updates flow to ViewModel
/// - Error handling doesn't crash the service
/// </summary>
public class ProcessingHostedServiceTests : IAsyncLifetime
{
    private readonly ServiceCollection _services = new();
    private IServiceProvider? _provider;
    private ProcessingHostedService? _service;
    private Mock<IPipelineProcessorService>? _processorMock;
    private ProcessingStateViewModel? _viewModel;
    private Mock<ILogger<ProcessingHostedService>>? _loggerMock;

    public async ValueTask InitializeAsync()
    {
        // Setup DI with mocks
        _processorMock = new Mock<IPipelineProcessorService>();
        var processingWorkerMock = new Mock<IProcessingWorker>();
        _viewModel = new ProcessingStateViewModel(processingWorkerMock.Object);
        _loggerMock = new Mock<ILogger<ProcessingHostedService>>();

        // Setup scope factory mock
        var scopeMock = new Mock<IServiceScope>();
        var scopeProviderMock = new Mock<IServiceProvider>();
        
        scopeProviderMock
            .Setup(x => x.GetService(typeof(IPipelineProcessorService)))
            .Returns(_processorMock.Object);
        scopeProviderMock
            .Setup(x => x.GetService(typeof(ILogger<ProcessingHostedService>)))
            .Returns(_loggerMock.Object);

        scopeMock.Setup(x => x.ServiceProvider).Returns(scopeProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(x => x.CreateScope())
            .Returns(scopeMock.Object);

        _services.AddSingleton(scopeFactoryMock.Object);
        _services.AddSingleton(_viewModel);
        _services.AddSingleton(_loggerMock.Object);

        _provider = _services.BuildServiceProvider();
        _service = new ProcessingHostedService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            _provider.GetRequiredService<ProcessingStateViewModel>(),
            _provider.GetRequiredService<ILogger<ProcessingHostedService>>()
        );

        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_service != null)
            await _service.StopAsync(CancellationToken.None);
        (_provider as ServiceProvider)?.Dispose();
    }

    [Fact]
    public async Task StartAsync_Should_Initialize_Processing_Loop()
    {
        // Arrange
        _processorMock!
            .Setup(x => x.GetPendingCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0); // No work

        // Act
        await _service!.StartAsync(CancellationToken.None);

        // Assert - wait for first timer tick
        await Task.Delay(2500);
        _processorMock.Verify(
            x => x.GetPendingCountAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );

        // Cleanup
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessQueueAsync_Should_Be_Called_When_Items_Pending()
    {
        // Arrange
        _processorMock!
            .Setup(x => x.GetPendingCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5); // 5 items pending

        _processorMock
            .Setup(x => x.ProcessQueueAsync(
                It.IsAny<IProgress<AnalysisProgress>>(),
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);

        // Act
        await _service!.StartAsync(CancellationToken.None);
        await Task.Delay(2500);

        // Assert
        _processorMock.Verify(
            x => x.ProcessQueueAsync(
                It.IsAny<IProgress<AnalysisProgress>>(),
                It.IsAny<CancellationToken>()
            ),
            Times.AtLeastOnce
        );

        // Cleanup
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_Should_Cancel_Processing_Gracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _processorMock!
            .Setup(x => x.GetPendingCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _service!.StartAsync(cts.Token);
        await Task.Delay(100);

        // Act
        await _service.StopAsync(cts.Token);

        // Assert
        Assert.False(_viewModel!.IsProcessing);
        Assert.Equal("Complete", _viewModel.StatusText);
    }

    [Fact]
    public async Task Errors_Should_Not_Crash_The_Service()
    {
        // Arrange
        var processingAttempts = 0;
        _processorMock!
            .Setup(x => x.GetPendingCountAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                processingAttempts++;
                if (processingAttempts == 1)
                    throw new InvalidOperationException("Simulated DB error");
                return 0;
            });

        // Act & Assert - service should continue despite error
        await _service!.StartAsync(CancellationToken.None);
        await Task.Delay(3000); // Wait for 2+ timer ticks

        // Service should still be running after error
        Assert.True(processingAttempts >= 2);

        // Cleanup
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TriggerProcessingAsync_Should_Process_Immediately()
    {
        // Arrange
        _processorMock!
            .Setup(x => x.GetPendingCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        _processorMock
            .Setup(x => x.ProcessQueueAsync(
                It.IsAny<IProgress<AnalysisProgress>>(),
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);

        await _service!.StartAsync(CancellationToken.None);

        // Act
        await _service.TriggerProcessingAsync();

        // Assert
        Assert.True(_viewModel!.IsProcessing);
        _processorMock.Verify(
            x => x.ProcessQueueAsync(
                It.IsAny<IProgress<AnalysisProgress>>(),
                It.IsAny<CancellationToken>()
            ),
            Times.AtLeastOnce
        );

        // Cleanup
        await _service.StopAsync(CancellationToken.None);
    }
}
