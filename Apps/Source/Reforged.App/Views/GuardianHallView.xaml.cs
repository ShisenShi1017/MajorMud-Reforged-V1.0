using System.Windows;
using System.Windows.Controls;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

public partial class GuardianHallView : UserControl
{
    public GuardianHallView() => InitializeComponent();
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not GuardianHallViewModel vm || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) vm.QueueDroppedFiles(paths);
    }
}
