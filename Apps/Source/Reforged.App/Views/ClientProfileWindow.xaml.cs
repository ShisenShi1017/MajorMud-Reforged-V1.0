using System.Windows;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

public partial class ClientProfileWindow : Window
{
    public ClientProfileWindow()
    {
        InitializeComponent();
    }

    private PlayViewModel? ViewModel => DataContext as PlayViewModel;

    private void NewClicked(object sender, RoutedEventArgs e) =>
        ViewModel?.CreateNewConnectionProfile();

    private async void DuplicateClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.DuplicateSelectedConnectionProfileAsync();
    }

    private async void DeleteClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedConnectionProfile is null)
            return;

        if (MessageBox.Show(
                $"Delete '{ViewModel.SelectedConnectionProfile.ProfileName}'? This removes only the local Client profile.",
                "Delete Character Profile",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            await ViewModel.DeleteSelectedConnectionProfileAsync();
        }
    }

    private async void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.SaveSelectedConnectionProfileAsync();
    }

    private async void SaveSessionAsClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.SaveCurrentConnectionProfileAsAsync();
    }

    private async void ApplyClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.ApplySelectedConnectionProfileAsync();
    }

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
