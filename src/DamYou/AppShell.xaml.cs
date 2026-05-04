namespace DamYou;

using DamYou.Views;
using Serilog;

public partial class AppShell : Shell
{
	public AppShell()
	{
		Log.Debug("AppShell constructor: Initializing component...");
		InitializeComponent();
		Log.Information("AppShell initialization complete");
	}

	/// <summary>
	/// Initializes tab content by resolving views from DI and assigning them to ShellContent items.
	/// Must be called BEFORE assigning AppShell to MainPage to avoid platform rendering errors.
	/// </summary>
	public void InitializeTabContent(IServiceProvider services)
	{
		Log.Debug("AppShell InitializeTabContent: Setting up tab content...");

		try
		{
			var galleryRoute = "gallery";
			var foldersRoute = "folders";
			var tasksRoute = "running-tasks";
			var settingsRoute = "settings";

			foreach (var item in this.Items)
			{
				if (item is ShellItem shellItem)
				{
					foreach (var section in shellItem.Items)
					{
						if (section is ShellSection shellSection)
						{
							foreach (var content in shellSection.Items)
							{
								if (content is ShellContent shellContent)
								{
									if (shellContent.Route == galleryRoute)
									{
										var galleryView = services.GetRequiredService<GalleryView>();
										shellContent.Content = galleryView;
										Log.Debug("AppShell InitializeTabContent: Assigned GalleryView to gallery tab");
									}
									else if (shellContent.Route == foldersRoute)
									{
										var foldersView = services.GetRequiredService<FoldersView>();
										shellContent.Content = foldersView;
										Log.Debug("AppShell InitializeTabContent: Assigned FoldersView to folders tab");
									}
									else if (shellContent.Route == tasksRoute)
									{
										var tasksView = services.GetRequiredService<RunningTasksView>();
										shellContent.Content = tasksView;
										Log.Debug("AppShell InitializeTabContent: Assigned RunningTasksView to tasks tab");
									}
									else if (shellContent.Route == settingsRoute)
									{
										var settingsPage = services.GetRequiredService<SettingsPage>();
										shellContent.Content = settingsPage;
										Log.Debug("AppShell InitializeTabContent: Assigned SettingsPage to settings tab");
									}
								}
							}
						}
					}
				}
			}

			Log.Information("AppShell InitializeTabContent: Tab content setup complete");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "AppShell InitializeTabContent: Error setting up tab content");
			throw;
		}
	}
}

