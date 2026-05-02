namespace DamYou.Services;

/// <summary>
/// Interface for triggering background photo processing.
/// Decouples ViewModels from the IHostedService implementation.
/// </summary>
public interface IProcessingWorker
{
    /// <summary>
    /// Triggers immediate pipeline processing (called after scan completes).
    /// Bypasses the normal timer for immediate feedback.
    /// </summary>
    Task TriggerProcessingAsync();
}
