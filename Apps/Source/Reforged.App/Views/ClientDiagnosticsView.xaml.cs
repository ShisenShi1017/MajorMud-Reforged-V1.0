using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

public partial class ClientDiagnosticsView : UserControl
{
    public ClientDiagnosticsView() => InitializeComponent();

    private async void RunDiagnosticsClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClientDiagnosticsViewModel viewModel)
            await viewModel.RunAsync();
    }

    private async void ExportDiagnosticsClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ClientDiagnosticsViewModel viewModel) return;
        var dialog = new SaveFileDialog
        {
            Title = "Export Client Diagnostics",
            Filter = "Text report (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"client-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };
        if (dialog.ShowDialog() == true)
            await viewModel.ExportAsync(dialog.FileName);
    }
}
