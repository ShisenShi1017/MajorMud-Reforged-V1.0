using System.Windows;
using RealmGate.App.ViewModels;

namespace RealmGate.App.Views;

public partial class ClientOptionsWindow : Window
{
    public ClientOptionsWindow()
    {
        InitializeComponent();
    }

    private ClientTerminalWindowContext? Context =>
        DataContext as ClientTerminalWindowContext;

    private async void SaveProfilesClicked(object sender, RoutedEventArgs e)
    {
        if (Context is null) return;
        await Context.Host.SaveConnectionProfileAsync(Context.Session);
        await Context.Host.SaveParsingProfileAsync();
        await Context.Host.SaveAutomationAsync();
    }

    private async void SelectParsingProfileClicked(object sender, RoutedEventArgs e)
    {
        if (Context is not null)
            await Context.Host.SelectParsingProfileFromListAsync();
    }

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
