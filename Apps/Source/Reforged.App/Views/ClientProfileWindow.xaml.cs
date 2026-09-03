using System.Windows;
using Microsoft.Win32;
using Reforged.App.Services;
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
                $"Delete BBS profile '{ViewModel.SelectedConnectionProfile.ProfileName}'? This removes only the local RealmGate profile.",
                "Delete BBS Profile",
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

    private void BrowseMdbClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedConnectionProfile is not { } profile)
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Choose external BBS MajorMUD database",
            Filter = "Microsoft Access databases (*.mdb;*.accdb)|*.mdb;*.accdb|MDB files (*.mdb)|*.mdb|ACCDB files (*.accdb)|*.accdb|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        profile.GameDataMdbPath = dialog.FileName;
        MdbStatusText.Text = $"Associated {System.IO.Path.GetFileName(dialog.FileName)}. Select Import / Refresh MDB to rebuild this BBS's local difference layer.";
    }

    private async void ImportMdbClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedConnectionProfile is not { } profile || ViewModel.SelectedLiveSession is not { } session)
        {
            MdbStatusText.Text = "Select both a BBS profile and a live session first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.GameDataMdbPath))
        {
            MdbStatusText.Text = "Choose an MDB or ACCDB file first.";
            return;
        }

        // Applying changes only the local session settings. It never connects or sends text,
        // and ensures the BBS-specific overlay key matches the selected profile endpoint.
        await ViewModel.ApplySelectedConnectionProfileAsync();
        await ViewModel.SaveSelectedConnectionProfileAsync();

        var overlay = new MudPlayClientGameDataOverlayService();
        var context = overlay.ResolveContext(session);
        if (!context.IsEditable || context.Mode != MudPlayGameDataAuthorityMode.ExternalBbs)
        {
            MdbStatusText.Text = "This is a RealmGate-hosted session. Its Librarium comes from the server-selected world, so an external MDB is not used.";
            return;
        }

        var importer = new MudPlayMdbOverlayImporter(overlay);
        var progress = new Progress<MudPlayMdbImportProgress>(update =>
            MdbStatusText.Text = update.Message);

        MdbStatusText.Text = "Importing external BBS MDB differences...";
        var result = await importer.ImportAsync(session, profile.GameDataMdbPath, progress);
        MdbStatusText.Text = result.Message;

        MessageBox.Show(
            this,
            result.Message,
            result.Success ? "BBS MDB Import Complete" : "BBS MDB Import Failed",
            MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private async void ClearMdbClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedConnectionProfile is not { } profile)
            return;

        profile.GameDataMdbPath = string.Empty;
        MdbStatusText.Text = "MDB association cleared. Existing local overlay data is left intact until a later cleanup/import replaces it.";
        await ViewModel.SaveSelectedConnectionProfileAsync();
    }

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
