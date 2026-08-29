using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeWorldStateScope { Global, Area, Zone, Event, Module }
public enum RuntimeWorldStateValueKind { Boolean, Integer, Text }
public enum RuntimeWorldStateResetPolicy { Never, SoftReset, HardReset, EventEnd }

public sealed record RuntimeWorldStateDefinition(
    string StateId,
    RuntimeWorldStateScope Scope,
    string OwnerSubsystem,
    RuntimeWorldStateValueKind ValueKind,
    string DefaultValue,
    RuntimeWorldStateResetPolicy ResetPolicy,
    bool Persistent,
    bool PlayerVisible,
    IReadOnlySet<string> MutationSources);

public sealed record RuntimeWorldStateEntry(string StateId, string Value, long Revision, DateTimeOffset UpdatedUtc, string UpdatedBy);
public sealed record RuntimeWorldStateMutation(string StateId, string PreviousValue, string CurrentValue, long Revision, string OwnerSubsystem);

/// <summary>
/// Generic Reforged typed world-state compatibility infrastructure.
///
/// This service is not evidence of Classic MajorMUD global-state record layout, default values,
/// reset/restart ordering, persistence mapping, replication, or player-visibility semantics.
/// Classic parity claims are gated by RuntimeClassicGlobalWorldStateAuthority.
/// </summary>
public sealed class RuntimeGlobalWorldStateService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeWorldStateDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeWorldStateEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeGlobalWorldStateService(IEnumerable<RuntimeWorldStateDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            ValidateDefinition(definition);
            if (!_definitions.TryAdd(definition.StateId, definition)) throw new InvalidOperationException("duplicate-world-state-id:" + definition.StateId);
            _entries.Add(definition.StateId, new(definition.StateId, Normalize(definition.ValueKind, definition.DefaultValue), 0, DateTimeOffset.UnixEpoch, "catalog-default"));
        }
    }

    public RuntimeWorldStateEntry Read(string stateId, bool privileged = false)
    {
        lock (_gate)
        {
            var definition = Definition(stateId);
            if (!definition.PlayerVisible && !privileged) throw new UnauthorizedAccessException("world-state-not-visible:" + stateId);
            return _entries[stateId];
        }
    }

    public RuntimeWorldStateMutation Set(string stateId, string value, string mutationSource, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var definition = Definition(stateId);
            Authorize(definition, mutationSource);
            var normalized = Normalize(definition.ValueKind, value);
            var previous = _entries[stateId];
            var next = previous with { Value = normalized, Revision = checked(previous.Revision + 1), UpdatedUtc = nowUtc, UpdatedBy = mutationSource };
            _entries[stateId] = next;
            return new(stateId, previous.Value, next.Value, next.Revision, definition.OwnerSubsystem);
        }
    }

    public RuntimeWorldStateMutation Increment(string stateId, long delta, string mutationSource, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var definition = Definition(stateId);
            if (definition.ValueKind != RuntimeWorldStateValueKind.Integer) throw new InvalidOperationException("world-state-not-counter:" + stateId);
            Authorize(definition, mutationSource);
            var previous = _entries[stateId];
            var current = long.Parse(previous.Value, System.Globalization.CultureInfo.InvariantCulture);
            var updated = checked(current + delta).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var next = previous with { Value = updated, Revision = checked(previous.Revision + 1), UpdatedUtc = nowUtc, UpdatedBy = mutationSource };
            _entries[stateId] = next;
            return new(stateId, previous.Value, next.Value, next.Revision, definition.OwnerSubsystem);
        }
    }

    public IReadOnlyList<RuntimeWorldStateMutation> Reset(RuntimeWorldStateResetPolicy policy, string mutationSource, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var results = new List<RuntimeWorldStateMutation>();
            foreach (var definition in _definitions.Values.Where(x => x.ResetPolicy == policy).OrderBy(x => x.StateId, StringComparer.OrdinalIgnoreCase))
            {
                Authorize(definition, mutationSource);
                var previous = _entries[definition.StateId];
                var value = Normalize(definition.ValueKind, definition.DefaultValue);
                var next = previous with { Value = value, Revision = checked(previous.Revision + 1), UpdatedUtc = nowUtc, UpdatedBy = mutationSource };
                _entries[definition.StateId] = next;
                results.Add(new(definition.StateId, previous.Value, next.Value, next.Revision, definition.OwnerSubsystem));
            }
            return results;
        }
    }

    public void Save(string path)
    {
        lock (_gate)
        {
            var snapshot = new RuntimeWorldStateSnapshot(1, _entries.Values.Where(x => _definitions[x.StateId].Persistent).OrderBy(x => x.StateId).ToArray());
            var temporary = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
    }

    public bool Restore(string path)
    {
        lock (_gate)
        {
            if (!File.Exists(path)) return false;
            RuntimeWorldStateSnapshot? snapshot;
            try { snapshot = JsonSerializer.Deserialize<RuntimeWorldStateSnapshot>(File.ReadAllText(path)); }
            catch (JsonException) { return false; }
            if (snapshot is null || snapshot.SchemaVersion != 1) return false;
            foreach (var entry in snapshot.Entries)
            {
                if (!_definitions.TryGetValue(entry.StateId, out var definition) || !definition.Persistent || entry.Revision < 0) continue;
                try { _entries[entry.StateId] = entry with { Value = Normalize(definition.ValueKind, entry.Value) }; }
                catch (Exception) when (entry.Value is not null) { return false; }
            }
            return true;
        }
    }

    public IReadOnlyList<string> ValidateState()
    {
        lock (_gate)
        {
            var failures = new List<string>();
            foreach (var definition in _definitions.Values)
            {
                if (!_entries.TryGetValue(definition.StateId, out var entry)) { failures.Add("missing-world-state:" + definition.StateId); continue; }
                if (entry.Revision < 0) failures.Add("negative-world-state-revision:" + definition.StateId);
                try { Normalize(definition.ValueKind, entry.Value); } catch { failures.Add("invalid-world-state-value:" + definition.StateId); }
            }
            return failures;
        }
    }

    private RuntimeWorldStateDefinition Definition(string id) => _definitions.TryGetValue(id, out var value) ? value : throw new KeyNotFoundException("unknown-world-state:" + id);
    private static void Authorize(RuntimeWorldStateDefinition definition, string source)
    {
        if (string.IsNullOrWhiteSpace(source) || !definition.MutationSources.Contains(source)) throw new UnauthorizedAccessException("world-state-mutation-denied:" + definition.StateId);
    }
    private static void ValidateDefinition(RuntimeWorldStateDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.StateId) || string.IsNullOrWhiteSpace(definition.OwnerSubsystem)) throw new ArgumentException("world-state-definition-invalid");
        if (definition.MutationSources.Count == 0) throw new ArgumentException("world-state-mutation-sources-required:" + definition.StateId);
        _ = Normalize(definition.ValueKind, definition.DefaultValue);
    }
    private static string Normalize(RuntimeWorldStateValueKind kind, string value) => kind switch
    {
        RuntimeWorldStateValueKind.Boolean => bool.Parse(value).ToString().ToLowerInvariant(),
        RuntimeWorldStateValueKind.Integer => long.Parse(value, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture),
        RuntimeWorldStateValueKind.Text when value.Length <= 1024 => value,
        RuntimeWorldStateValueKind.Text => throw new ArgumentOutOfRangeException(nameof(value), "world-state-text-too-long"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private sealed record RuntimeWorldStateSnapshot(int SchemaVersion, IReadOnlyList<RuntimeWorldStateEntry> Entries);
}
