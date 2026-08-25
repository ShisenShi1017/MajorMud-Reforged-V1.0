using System.Windows;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

public partial class BorinPlayerAssignmentWindow : Window
{
    public BorinPlayerAssignmentWindow(BorinForgeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
