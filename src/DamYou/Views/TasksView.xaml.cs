using DamYou.ViewModels;

namespace DamYou.Views;

public partial class TasksView : ContentPage
{
    private readonly TasksViewModel _vm;
    private IDispatcherTimer? _refreshTimer;

    public TasksView(TasksViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadTasksCommand.Execute(null);

        _refreshTimer = Dispatcher.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(3);
        _refreshTimer.Tick += OnRefreshTick;
        _refreshTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    private void OnRefreshTick(object? sender, EventArgs e)
    {
        _vm.LoadTasksCommand.Execute(null);
    }
}
