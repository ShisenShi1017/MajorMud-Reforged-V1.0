using Reforged.App.Models;
using Reforged.App.Services;

namespace Reforged.App.ViewModels;

public sealed class ClientDiagnosticsViewModel : ViewModelBase
{
    private readonly IClientDiagnosticsService _diagnostics;
    private string _status = "Run diagnostics to verify Client readiness.";
    private bool _isRunning;

    public ClientDiagnosticsViewModel(IClientDiagnosticsService diagnostics) => _diagnostics = diagnostics;
    public ClientDiagnosticsSnapshot Snapshot => _diagnostics.Snapshot;
    public string Status { get => _status; private set => Set(ref _status, value); }
    public bool IsRunning { get => _isRunning; private set => Set(ref _isRunning, value); }

    public async Task RunAsync()
    {
        if (IsRunning) return;
        IsRunning = true;
        Status = "Running Client readiness checks…";
        try
        {
            await _diagnostics.RunAsync();
            Status = Snapshot.Summary;
        }
        catch (Exception exception)
        {
            Status = $"Diagnostics failed: {exception.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    public async Task<string?> ExportAsync(string? destinationPath = null)
    {
        try
        {
            var path = await _diagnostics.ExportAsync(destinationPath);
            Status = $"Diagnostics exported to {path}";
            return path;
        }
        catch (Exception exception)
        {
            Status = $"Export failed: {exception.Message}";
            return null;
        }
    }
}
