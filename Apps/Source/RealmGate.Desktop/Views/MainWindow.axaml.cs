using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RealmGate.Desktop.ViewModels;

namespace RealmGate.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = new MainWindowViewModel();
        Closed += async (_, _) =>
        {
            if (DataContext is IAsyncDisposable disposable)
                await disposable.DisposeAsync();
        };
    }
}
