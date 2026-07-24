using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Reforged.App.Models;

public sealed class ClientLibrariumSnapshot : INotifyPropertyChanged
{
    private string _characterName = "No session selected";
    private string _worldName = "Classic";
    private string _summary = "Select a live session, then refresh the read-only Librarium snapshot.";
    private DateTimeOffset? _capturedAt;

    public string CharacterName { get => _characterName; set => Set(ref _characterName, value); }
    public string WorldName { get => _worldName; set => Set(ref _worldName, value); }
    public string Summary { get => _summary; set => Set(ref _summary, value); }
    public DateTimeOffset? CapturedAt { get => _capturedAt; set => Set(ref _capturedAt, value); }
    public ObservableCollection<ClientLibrariumMetric> Metrics { get; } = [];
    public ObservableCollection<ClientLibrariumItemMatch> EquipmentAndInventory { get; } = [];
    public ObservableCollection<ClientLibrariumComparison> Comparisons { get; } = [];
    public ObservableCollection<ClientLibrariumBuildMetric> BuildMetrics { get; } = [];
    public ObservableCollection<ClientLibrariumUpgradeCandidate> UpgradeCandidates { get; } = [];
    public ObservableCollection<ClientLibrariumMagicMetric> MagicMetrics { get; } = [];
    public ObservableCollection<ClientLibrariumProgressionPlan> ProgressionPlans { get; } = [];
    public ObservableCollection<ClientLibrariumRateSample> RateSamples { get; } = [];
    public ObservableCollection<ClientLibrariumSimulationScenario> SimulationScenarios { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
    }
}

public sealed record ClientLibrariumMetric(string Name, string Value, string Source);

public sealed class ClientLibrariumItemMatch
{
    public string CapturedArea { get; init; } = "Carried";
    public string CapturedText { get; init; } = string.Empty;
    public string LibrariumName { get; init; } = string.Empty;
    public string MatchStatus { get; init; } = "Not matched";
    public string ItemType { get; init; } = string.Empty;
    public string Weight { get; init; } = string.Empty;
    public string Cost { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public sealed record ClientLibrariumComparison(string Area, string CurrentValue, string PlanningNote);

public sealed record ClientLibrariumBuildMetric(string Area, string Value, string Detail, string Confidence);

public sealed record ClientLibrariumUpgradeCandidate(
    string CapturedItem,
    string CandidateName,
    string Category,
    string Improvement,
    string Tradeoff,
    string Source);

public sealed record ClientLibrariumMagicMetric(
    string Area,
    string Value,
    string PlanningNote,
    string Confidence);

public sealed record ClientLibrariumProgressionPlan(
    string Plan,
    string CurrentSnapshot,
    string Estimate,
    string Assumptions);

public sealed record ClientLibrariumRateSample(
    string SampleWindow,
    string XpChange,
    string XpPerHour,
    string CurrencyChange,
    string CurrencyPerHour,
    string Confidence);


public sealed record ClientLibrariumSimulationScenario(
    string Scenario,
    string Readiness,
    string ProjectedOutcome,
    string LimitingFactor,
    string Assumptions,
    string Confidence);

public sealed class ClientSavedBuildSnapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "Saved Build";
    public string CharacterName { get; init; } = string.Empty;
    public string WorldName { get; init; } = string.Empty;
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;
    public List<ClientLibrariumMetric> Metrics { get; init; } = [];
    public List<ClientLibrariumItemMatch> EquipmentAndInventory { get; init; } = [];
    public List<ClientLibrariumBuildMetric> BuildMetrics { get; init; } = [];
    public List<ClientLibrariumMagicMetric> MagicMetrics { get; init; } = [];
    public List<ClientLibrariumProgressionPlan> ProgressionPlans { get; init; } = [];
    public string DisplayLabel => $"{Name} — {CharacterName} ({CapturedAt.LocalDateTime:g})";
}

public sealed class ClientPortableBuildPackage
{
    public string Format { get; init; } = "MajorMudReforged.ClientBuild";
    public int Version { get; init; } = 1;
    public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.Now;
    public ClientSavedBuildSnapshot Build { get; init; } = new();
}

public sealed record ClientBuildComparisonRow(
    string Area,
    string BuildA,
    string BuildB,
    string Difference,
    string PlanningNote);
