using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeWorldConditionMode { Disabled, PresentationOnly, Simulated }
public enum RuntimeDayPhase { Unknown, Dawn, Day, Dusk, Night }
public enum RuntimeWeatherKind { Clear, Rain, Storm, Fog, Wind, Snow, Custom }

public sealed record RuntimeWorldConditionDefinition(
    string ConditionId,
    RuntimeWorldConditionMode Mode,
    RuntimeDayPhase DefaultDayPhase,
    RuntimeWeatherKind DefaultWeather,
    bool AffectsVisibility,
    bool AffectsDescriptions,
    bool AffectsSpawns,
    bool AffectsNpcBehavior,
    bool AffectsSpells,
    bool AffectsHazards,
    bool PersistState);

public sealed record RuntimeWorldConditionState(
    string ConditionId,
    RuntimeDayPhase DayPhase,
    RuntimeWeatherKind Weather,
    string? CustomWeatherId,
    long Revision);

public sealed record RuntimeWorldConditionRequest(
    string OwnerSubsystem,
    string ActionId,
    string ConditionId,
    RuntimeDayPhase DayPhase,
    RuntimeWeatherKind Weather,
    long Revision);

public sealed record RuntimeWorldConditionDecision(
    bool Allowed,
    string Code,
    RuntimeWorldConditionState? State,
    IReadOnlyList<RuntimeWorldConditionRequest> Requests,
    bool EvidenceOnly);

public sealed record RuntimeWorldConditionSnapshot(int SchemaVersion, IReadOnlyList<RuntimeWorldConditionState> States, DateTimeOffset SavedUtc);

public sealed class RuntimeWeatherDayNightService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeWorldConditionDefinition> _definitions;
    private readonly Dictionary<string, RuntimeWorldConditionState> _states;
    private readonly string? _persistencePath;

    public RuntimeWeatherDayNightService(IEnumerable<RuntimeWorldConditionDefinition> definitions, string? persistencePath = null)
    {
        _definitions = definitions.ToDictionary(x => x.ConditionId, StringComparer.OrdinalIgnoreCase);
        if (_definitions.Values.Any(x => string.IsNullOrWhiteSpace(x.ConditionId)))
            throw new ArgumentException("Condition IDs are required.", nameof(definitions));
        _states = _definitions.Values.ToDictionary(
            x => x.ConditionId,
            x => new RuntimeWorldConditionState(x.ConditionId, x.DefaultDayPhase, x.DefaultWeather, null, 0),
            StringComparer.OrdinalIgnoreCase);
        _persistencePath = persistencePath;
        Restore();
    }

    public RuntimeWorldConditionDecision Inspect(string conditionId)
    {
        lock (_gate)
        {
            if (!_definitions.TryGetValue(conditionId, out var definition) || !_states.TryGetValue(conditionId, out var state))
                return new(false, "condition-unknown", null, Array.Empty<RuntimeWorldConditionRequest>(), false);
            return new(true, definition.Mode == RuntimeWorldConditionMode.Disabled ? "condition-disabled" : "condition-inspected", state, Array.Empty<RuntimeWorldConditionRequest>(), definition.Mode != RuntimeWorldConditionMode.Simulated);
        }
    }

    public RuntimeWorldConditionDecision SetState(string conditionId, RuntimeDayPhase dayPhase, RuntimeWeatherKind weather, string? customWeatherId = null)
    {
        lock (_gate)
        {
            if (!_definitions.TryGetValue(conditionId, out var definition) || !_states.TryGetValue(conditionId, out var state))
                return new(false, "condition-unknown", null, Array.Empty<RuntimeWorldConditionRequest>(), false);
            if (definition.Mode != RuntimeWorldConditionMode.Simulated)
                return new(false, definition.Mode == RuntimeWorldConditionMode.Disabled ? "condition-disabled" : "condition-presentation-only", state, Array.Empty<RuntimeWorldConditionRequest>(), true);
            if (dayPhase == RuntimeDayPhase.Unknown || (weather == RuntimeWeatherKind.Custom && string.IsNullOrWhiteSpace(customWeatherId)))
                return new(false, "condition-value-invalid", state, Array.Empty<RuntimeWorldConditionRequest>(), false);
            state = state with { DayPhase = dayPhase, Weather = weather, CustomWeatherId = weather == RuntimeWeatherKind.Custom ? customWeatherId : null, Revision = checked(state.Revision + 1) };
            _states[conditionId] = state;
            SaveLocked();
            return new(true, "condition-updated", state, BuildRequests(definition, state), true);
        }
    }

    public IReadOnlyList<string> ValidateState()
    {
        lock (_gate)
        {
            var errors = new List<string>();
            foreach (var (id, state) in _states)
            {
                if (!_definitions.ContainsKey(id)) errors.Add("orphan-state:" + id);
                if (state.Revision < 0) errors.Add("negative-revision:" + id);
                if (state.Weather == RuntimeWeatherKind.Custom && string.IsNullOrWhiteSpace(state.CustomWeatherId)) errors.Add("missing-custom-weather-id:" + id);
            }
            return errors;
        }
    }

    private static IReadOnlyList<RuntimeWorldConditionRequest> BuildRequests(RuntimeWorldConditionDefinition definition, RuntimeWorldConditionState state)
    {
        var requests = new List<RuntimeWorldConditionRequest>();
        void Add(bool enabled, string owner, string action) { if (enabled) requests.Add(new(owner, action, state.ConditionId, state.DayPhase, state.Weather, state.Revision)); }
        Add(definition.AffectsVisibility, "environmental-hazards", "apply-visibility-condition");
        Add(definition.AffectsDescriptions, "rooms", "refresh-condition-description");
        Add(definition.AffectsSpawns, "npc-ai", "reevaluate-condition-spawns");
        Add(definition.AffectsNpcBehavior, "npc-ai", "reevaluate-condition-behavior");
        Add(definition.AffectsSpells, "spells", "reevaluate-condition-spells");
        Add(definition.AffectsHazards, "environmental-hazards", "reevaluate-condition-hazards");
        return requests;
    }

    private void Restore()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath) || !File.Exists(_persistencePath)) return;
        try
        {
            var snapshot = JsonSerializer.Deserialize<RuntimeWorldConditionSnapshot>(File.ReadAllText(_persistencePath));
            if (snapshot?.SchemaVersion != 1) return;
            foreach (var state in snapshot.States)
                if (_definitions.TryGetValue(state.ConditionId, out var definition) && definition.PersistState)
                    _states[state.ConditionId] = state;
        }
        catch { }
    }

    private void SaveLocked()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath)) return;
        var states = _states.Values.Where(x => _definitions[x.ConditionId].PersistState).OrderBy(x => x.ConditionId, StringComparer.OrdinalIgnoreCase).ToArray();
        var snapshot = new RuntimeWorldConditionSnapshot(1, states, DateTimeOffset.UtcNow);
        var directory = Path.GetDirectoryName(_persistencePath); if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temp = _persistencePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _persistencePath, true);
    }
}
