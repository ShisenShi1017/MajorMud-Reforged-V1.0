using System.Windows;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

public partial class ClientOptionsWindow : Window
{
    public ClientOptionsWindow()
    {
        InitializeComponent();
    }

    private PlayViewModel? ViewModel => DataContext as PlayViewModel;

    private async void SaveProfilesClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        await ViewModel.SaveParsingProfileAsync();
        await ViewModel.SaveAutomationAsync();
    }

    private async void SelectParsingProfileClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) await ViewModel.SelectParsingProfileFromListAsync();
    }

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
