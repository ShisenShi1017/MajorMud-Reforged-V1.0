using System.ComponentModel;
using System.Windows;
using Reforged.App.Models;
using Reforged.App.Services;

namespace Reforged.App;

public partial class MainWindow : Window
{
    private readonly ILayoutPersistenceService _layouts;
    private readonly IWorkspaceManager _workspaces;
    private bool _shutdownRequested;

    public MainWindow(
        ViewModels.MainWindowViewModel viewModel,
        ILayoutPersistenceService layouts,
        IWorkspaceManager workspaces)
    {
        InitializeComponent();
        DataContext = viewModel;
        _layouts = layouts;
        _workspaces = workspaces;
        Closing += OnClosing;
    }

    public async Task RestoreAsync()
    {
        var state = await _layouts.LoadAsync();
        var workArea = SystemParameters.WorkArea;
        var outerMargin = 40d;

        MinWidth = Math.Min(MinWidth, Math.Max(720, workArea.Width - outerMargin));
        MinHeight = Math.Min(MinHeight, Math.Max(480, workArea.Height - outerMargin));

        var maxWidth = Math.Max(MinWidth, workArea.Width - outerMargin);
        var maxHeight = Math.Max(MinHeight, workArea.Height - outerMargin);

        Width = Math.Min(maxWidth, Math.Max(MinWidth, FiniteOrDefault(state.Width, 1180)));
        Height = Math.Min(maxHeight, Math.Max(MinHeight, FiniteOrDefault(state.Height, 760)));

        var requestedLeft = state.Left is { } left && double.IsFinite(left)
            ? left
            : workArea.Left + ((workArea.Width - Width) / 2);
        var requestedTop = state.Top is { } top && double.IsFinite(top)
            ? top
            : workArea.Top + ((workArea.Height - Height) / 2);

        Left = Clamp(requestedLeft, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
        Top = Clamp(requestedTop, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));

        await _workspaces.ActivateAsync(state.ActiveWorkspaceId);
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_shutdownRequested)
            return;

        e.Cancel = true;
        _shutdownRequested = true;
        IsEnabled = false;

        try
        {
            await _layouts.SaveAsync(new LayoutState
            {
                ActiveWorkspaceId = _workspaces.ActiveWorkspace?.Id ?? "home",
                Width = Width,
                Height = Height,
                Left = double.IsFinite(Left) ? Left : null,
                Top = double.IsFinite(Top) ? Top : null,
            });
        }
        catch
        {
            // Shutdown must continue even when layout persistence fails.
        }

        if (Application.Current is App app)
            await app.ShutdownApplicationAsync();
        else
            Application.Current.Shutdown();
    }

    private static double FiniteOrDefault(double value, double fallback) =>
        double.IsFinite(value) && value > 0 ? value : fallback;

    private static double Clamp(double value, double minimum, double maximum) =>
        Math.Min(Math.Max(value, minimum), maximum);
}
