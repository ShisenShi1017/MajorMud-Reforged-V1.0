using System.Windows;
using RealmGate.App.ViewModels;

namespace RealmGate.App.Views;

public partial class BorinPlayerAssignmentWindow : Window
{
    public BorinPlayerAssignmentWindow(BorinForgeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
