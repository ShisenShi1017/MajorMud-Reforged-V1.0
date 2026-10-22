using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RealmGate.Desktop.Controls;
using RealmGate.Desktop.ViewModels;

namespace RealmGate.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = new MainWindowViewModel();
        var terminal = this.FindControl<ClassicTerminalView>("PlayTerminal")
            ?? throw new InvalidOperationException("The Play terminal control was not created.");
        terminal.CommandSubmitted += OnTerminalCommandSubmitted;
        terminal.KeySubmitted += OnTerminalKeySubmitted;
        terminal.TerminalSizeChanged += OnTerminalSizeChanged;
        Closed += async (_, _) =>
        {
            if (DataContext is IAsyncDisposable disposable)
                await disposable.DisposeAsync();
        };
    }

    private async void OnTerminalCommandSubmitted(
        object? sender,
        ClassicTerminalCommandSubmittedEventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel)
            await viewModel.SendTerminalCommandAsync(args.Command, args.Sensitive);
    }

    private async void OnTerminalKeySubmitted(
        object? sender,
        ClassicTerminalKeySubmittedEventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel)
            await viewModel.SendTerminalKeyAsync(args.Sequence);
    }

    private async void OnTerminalSizeChanged(
        object? sender,
        ClassicTerminalSizeChangedEventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel)
            await viewModel.ResizeTerminalAsync(args.Columns, args.Rows);
    }
}
