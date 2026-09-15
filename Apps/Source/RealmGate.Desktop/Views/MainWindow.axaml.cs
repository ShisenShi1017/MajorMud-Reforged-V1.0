using Avalonia.Controls;
using RealmGate.Desktop.ViewModels;

namespace RealmGate.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Closed += async (_, _) =>
        {
            if (DataContext is IAsyncDisposable disposable)
                await disposable.DisposeAsync();
        };
    }
}
