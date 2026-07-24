using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

public partial class PlayView : UserControl
{
    private void OpenTerminalClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayViewModel vm) return;
        var window = new ClientTerminalWindow
        {
            Owner = Window.GetWindow(this),
            DataContext = vm
        };
        window.Show();
    }

    private void OpenOptionsClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayViewModel vm) return;
        var window = new ClientOptionsWindow
        {
            Owner = Window.GetWindow(this),
            DataContext = vm
        };
        window.Show();
    }

    private async void SelectParsingProfileClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SelectParsingProfileFromListAsync(); }
    private async void DuplicateParsingProfileClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.DuplicateParsingProfileAsync(); }
    private async void DeleteParsingProfileClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayViewModel vm) return;
        if (MessageBox.Show("Delete this Client-only parsing profile? The Host is not affected.", "Delete Client Profile", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            await vm.DeleteParsingProfileAsync();
    }
    private async void ExportParsingProfileClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayViewModel vm) return;
        var dialog = new SaveFileDialog { Title = "Export Client Parsing Profile", Filter = "Reforged Client Profile (*.mmrclientprofile)|*.mmrclientprofile", FileName = "client-parsing-profile.mmrclientprofile", AddExtension = true };
        if (dialog.ShowDialog() == true) await vm.ExportParsingProfileAsync(dialog.FileName);
    }
    private async void ImportParsingProfileClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayViewModel vm) return;
        var dialog = new OpenFileDialog { Title = "Import Client Parsing Profile", Filter = "Reforged Client Profile (*.mmrclientprofile)|*.mmrclientprofile", Multiselect = false };
        if (dialog.ShowDialog() == true) await vm.ImportParsingProfileAsync(dialog.FileName);
    }

    public PlayView() => InitializeComponent();
    private async void RefreshLibrariumSnapshotClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.RefreshLibrariumSnapshotAsync(); }
    private async void SaveCurrentBuildClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SaveCurrentBuildAsync(); }
    private async void ExportSavedBuildClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayViewModel vm) return;
        var selectedName = vm.SelectedSavedBuildB?.Name ?? vm.SelectedSavedBuildA?.Name ?? "saved-build";
        var safeName = string.Concat(selectedName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var dialog = new SaveFileDialog
        {
            Title = "Export Saved Client Build",
            Filter = "Reforged Saved Build (*.mmrbuild)|*.mmrbuild",
            FileName = $"{safeName}.mmrbuild",
            AddExtension = true
        };
        if (dialog.ShowDialog() == true) await vm.ExportSelectedBuildAsync(dialog.FileName);
    }

    private async void ImportSavedBuildClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayViewModel vm) return;
        var dialog = new OpenFileDialog
        {
            Title = "Import Saved Client Build",
            Filter = "Reforged Saved Build (*.mmrbuild)|*.mmrbuild",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await vm.ImportSavedBuildAsync(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Import Saved Build", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteSavedBuildClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayViewModel vm) return;
        if (MessageBox.Show("Delete the selected Client-only saved build?", "Delete Saved Build", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            await vm.DeleteSelectedBuildAsync();
    }
    private async void SaveParsingProfileClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SaveParsingProfileAsync(); }
    private async void ApplySessionParsingProfileClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.ApplySelectedSessionParsingProfileAsync(); }
    private void TestParsingSampleClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.TestParsingSample(); }
    private void AddParsingRuleClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.AddParsingRule(); }
    private void RemoveParsingRuleClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RemoveParsingRule(); }
    private void RestoreParsingDefaultsClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RestoreParsingDefaults(); }
    private async void SaveClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SaveSelectedAsync(); }
    private async void ResetClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.ResetSelectedAsync(); }
    private async void SaveAutomationClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SaveAutomationAsync(); }
    private async void AutoToggleClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm && sender is ToggleButton toggle) await vm.SetMasterAutomationAsync(toggle.IsChecked == true); }
    private async void AutomationAreaClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm && sender is FrameworkElement element && element.Tag is string area) await vm.ToggleAutomationAreaAsync(area); }
    private async void StopAllClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.StopAllAutomationAsync(); }
    private void AddRuleClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.AddAutomationRule(); }
    private void RemoveRuleClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RemoveAutomationRule(); }
    private async void SaveAutomationOptionsClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SaveAutomationOptionsAsync(); }
    private void AddSpellPriorityClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.AddSpellPriority(); }
    private void RemoveSpellPriorityClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RemoveSpellPriority(); }
    private void AddCombatPriorityClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.AddCombatPriority(); }
    private void RemoveCombatPriorityClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RemoveCombatPriority(); }
    private void AddCurrencyRuleClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.AddCurrencyRule(); }
    private void RemoveCurrencyRuleClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RemoveCurrencyRule(); }
    private void AddScheduledEventClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.AddScheduledEvent(); }
    private void RemoveScheduledEventClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RemoveScheduledEvent(); }
    private async void SaveCoordinationClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SaveCoordinationAsync(); }
    private void AddSessionClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.AddSession(); }
    private void RemoveSessionClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RemoveSession(); }
    private void AddControllerClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.AddTrustedController(); }
    private void RemoveControllerClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RemoveTrustedController(); }
    private void SendCoordinationClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.SendCoordinationCommand(); }
    private void TestChatClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.TestChatCommand(); }
    private void PauseCoordinationClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.PauseCoordination(); }
    private void ResumeCoordinationClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.ResumeCoordination(); }
    private void StopCoordinationClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.StopCoordination(); }
    private void ResyncCoordinationClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.ResyncCoordination(); }
    private async void ImportPathClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.ImportSelectedPathAsync(); }
    private async void RemovePathClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.RemoveSelectedPathAsync(); }
    private async void ConnectSessionClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.ConnectSelectedSessionAsync(); }
    private async void DisconnectSessionClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.DisconnectSelectedSessionAsync(); }
    private async void SendTerminalClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SendTerminalCommandAsync(); }
    private void ClearTerminalClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.ClearSelectedTerminal(); }
    private async void ExportTerminalLogClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.ExportSelectedTerminalLogAsync(); }
    private async void TerminalCommandKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Enter && DataContext is PlayViewModel vm) { e.Handled = true; await vm.SendTerminalCommandAsync(); } }
    private async void StartRouteClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.StartSelectedRouteAsync(); }
    private void PauseRouteClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.PauseSelectedRoute(); }
    private void ResumeRouteClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.ResumeSelectedRoute(); }
    private void StopRouteClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.StopSelectedRoute(); }
    private void PrepareRouteRecoveryClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.PrepareSelectedRouteRecovery(); }
    private async void AutoRecoverRouteClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.CalculateAndRunRouteRecoveryAsync(); }
    private void StartRouteRecordingClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.StartRouteRecording(); }
    private async void FinishRouteRecordingClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.FinishRouteRecordingAsync(); }
    private void CancelRouteRecordingClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.CancelRouteRecording(); }
    private async void SaveLocalPathClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SaveLocalPathAsync(); }
    private async void DuplicateLocalPathClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.DuplicateLocalPathAsync(); }
    private async void DeleteLocalPathClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.DeleteLocalPathAsync(); }
    private void AddLocalPathCommandClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.AddLocalPathCommand(); }
    private void UpdateLocalPathCommandClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.UpdateLocalPathCommand(); }
    private void RemoveLocalPathCommandClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RemoveLocalPathCommand(); }
    private void MoveLocalPathCommandUpClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.MoveLocalPathCommand(-1); }
    private void MoveLocalPathCommandDownClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.MoveLocalPathCommand(1); }
    private async void RunLocalPathClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.RunLocalPathAsync(); }
    private async void SaveMapProfileClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SaveMapProfileAsync(); }
    private void SelectCurrentMapRoomClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.SelectCurrentMapRoom(); }
    private void RefreshMapRoomsClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RefreshMapRooms(); }
    private void ToggleMapFavoriteClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.ToggleSelectedMapFavorite(); }
    private void MapLayerChanged(object sender, SelectionChangedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RefreshMapRooms(); }
    private async void SetSpawnTimerClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SetSpawnTimerAsync(); }
    private async void ClearSpawnTimerClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.ClearSpawnTimerAsync(); }
    private async void SaveSysGotoProfileClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SaveSysGotoProfileAsync(); }
    private void AddSysGotoDestinationClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.AddSysGotoDestination(); }
    private void RemoveSysGotoDestinationClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) vm.RemoveSelectedSysGotoDestination(); }
    private async void TravelWithSysGotoClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayViewModel vm) return;
        if (vm.SysGotoProfile.ConfirmBeforeTravel)
        {
            var city = vm.SelectedSysGotoDestination?.DisplayName ?? "the selected city";
            if (MessageBox.Show($"Use sys goto to travel to {city}?", "Confirm City Travel", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        }
        await vm.TravelWithSysGotoAsync();
    }
    private void TravelGroupWithSysGotoClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayViewModel vm) return;
        if (vm.SysGotoProfile.ConfirmBeforeTravel)
        {
            var city = vm.SelectedSysGotoDestination?.DisplayName ?? "the selected city";
            if (MessageBox.Show($"Send sys goto travel to coordinated sessions for {city}?", "Confirm Group City Travel", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        }
        vm.TravelGroupWithSysGoto();
    }
    private async void SaveWorkspaceProfileClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.SaveWorkspaceProfileAsync(); }
    private async void ApplyWorkspacePresetClicked(object sender, RoutedEventArgs e) { if (DataContext is PlayViewModel vm) await vm.ApplyWorkspacePresetAsync(); }
}
