using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RealmGate.App.Models;

public enum ClientDiagnosticSeverity
{
    Information,
    Warning,
    Error
}

public sealed class ClientDiagnosticCheck : INotifyPropertyChanged
{
    private string _status = "Pending";
    private string _details = string.Empty;
    private ClientDiagnosticSeverity _severity = ClientDiagnosticSeverity.Information;

    public string Name { get; init; } = string.Empty;
    public string Area { get; init; } = string.Empty;
    public string Status { get => _status; set => Set(ref _status, value); }
    public string Details { get => _details; set => Set(ref _details, value); }
    public ClientDiagnosticSeverity Severity { get => _severity; set => Set(ref _severity, value); }
    public bool Passed => string.Equals(Status, "Passed", StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
        if (name == nameof(Status)) Raise(nameof(Passed));
    }
}

public sealed class ClientDiagnosticsSnapshot : INotifyPropertyChanged
{
    private DateTimeOffset? _lastRun;
    private string _summary = "Diagnostics have not been run.";

    public ObservableCollection<ClientDiagnosticCheck> Checks { get; } = [];
    public DateTimeOffset? LastRun { get => _lastRun; set => Set(ref _lastRun, value); }
    public string Summary { get => _summary; set => Set(ref _summary, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
    }
}
