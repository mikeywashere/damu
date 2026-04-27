namespace DamYou.Services;

public interface IFolderPickerService
{
    /// <summary>Opens the OS folder picker. Returns null if the user cancels.</summary>
    Task<string?> PickFolderAsync();
}
