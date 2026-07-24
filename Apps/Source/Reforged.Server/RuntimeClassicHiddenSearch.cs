namespace Reforged.Server;

public sealed record RuntimeClassicHiddenTarget(
    string TargetId,
    string RoomId,
    string TargetKind,
    int ConcealmentDifficulty,
    bool Gettable,
    bool DeleteAtCleanup,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record RuntimeClassicSearchActor(
    string CharacterId,
    string RoomId,
    int EffectivePerception,
    int SearchSkill,
    int Agility,
    int Intellect,
    int Charm,
    IReadOnlyDictionary<string, int>? Modifiers = null);

public sealed record RuntimeClassicSearchRoll(
    string TargetId,
    int Roll,
    int EffectivePerception,
    int ConcealmentDifficulty,
    bool Discovered,
    string EvidenceClassification,
    string FormulaId);

public sealed record RuntimeClassicSearchResult(
    string CharacterId,
    string RoomId,
    IReadOnlyList<RuntimeClassicSearchRoll> Rolls,
    IReadOnlyList<string> NewlyDiscoveredTargetIds,
    IReadOnlyList<string> PreviouslyDiscoveredTargetIds,
    string Message);

/// <summary>
/// Transient current-search visibility only. Classic SEARCH does not permanently remember a
/// discovered hidden object. Each SEARCH replaces this character's prior result set, allowing a
/// full room of hidden objects to produce different visible results on every attempt.
/// </summary>
public sealed class RuntimeClassicHiddenDiscoveryRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, HashSet<string>> _currentResults = new(StringComparer.OrdinalIgnoreCase);

    // Retained for source compatibility with earlier Bundle C revisions. No path is read or written.
    public RuntimeClassicHiddenDiscoveryRegistry(string? persistencePath = null) { }

    public static RuntimeClassicHiddenDiscoveryRegistry ForWorld(string projectRoot, string worldId) => new();

    public bool IsDiscovered(string characterId, string targetId)
    {
        lock (_gate)
            return _currentResults.TryGetValue(CharacterKey(characterId), out var results) && results.Contains(TargetKey(targetId));
    }

    /// <summary>
    /// Compatibility hook for Bundle D hidden exits and world objects. This records a target only
    /// in the current in-memory visibility set; it is never persisted. Item SEARCH still calls
    /// ReplaceSearchResults so each new search replaces the prior item result set.
    /// </summary>
    public bool Discover(string characterId, string targetId)
    {
        var character = CharacterKey(characterId);
        var target = TargetKey(targetId);
        lock (_gate)
        {
            if (!_currentResults.TryGetValue(character, out var results))
            {
                results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _currentResults[character] = results;
            }
            return results.Add(target);
        }
    }

    /// <summary>Replaces, rather than accumulates, the targets found by the character's latest SEARCH.</summary>
    public void ReplaceSearchResults(string characterId, IEnumerable<string> targetIds)
    {
        ArgumentNullException.ThrowIfNull(targetIds);
        var key = CharacterKey(characterId);
        var replacement = targetIds.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(TargetKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            if (replacement.Count == 0) _currentResults.Remove(key);
            else _currentResults[key] = replacement;
        }
    }

    public void ForgetTarget(string targetId)
    {
        var target = TargetKey(targetId);
        lock (_gate)
        {
            foreach (var results in _currentResults.Values) results.Remove(target);
            foreach (var empty in _currentResults.Where(x => x.Value.Count == 0).Select(x => x.Key).ToArray())
                _currentResults.Remove(empty);
        }
    }

    public IReadOnlyList<string> GetDiscoveredTargets(string characterId)
    {
        lock (_gate)
            return _currentResults.TryGetValue(CharacterKey(characterId), out var results)
                ? results.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
                : Array.Empty<string>();
    }

    private static string CharacterKey(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)) throw new ArgumentException("Character id is required.", nameof(characterId));
        return characterId.Trim();
    }

    private static string TargetKey(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("Target id is required.", nameof(targetId));
        return targetId.Trim();
    }
}

/// <summary>
/// Classic SEARCH behavior:
/// - every command rerolls every eligible hidden target in the room;
/// - the latest successful result set replaces the prior result set for that character;
/// - search never globally unhides an object and never persists discovery;
/// - a later SEARCH can show a different subset when the room contains multiple hidden objects;
/// - pickup clears hidden state, while redrop creates a normal visible room object.
///
/// The caller supplies authoritative effective Perception and concealment values. The exact native
/// primary-stat derivation remains external and is not invented here.
/// </summary>
public sealed class RuntimeClassicHiddenSearchService
{
    public const string FormulaId = "classic.search.perception_vs_concealment.strongly_correlated.0167";
    private readonly RuntimeClassicHiddenDiscoveryRegistry _discoveries;
    private readonly Func<int, int, int, bool> _comparison;

    public RuntimeClassicHiddenSearchService(
        RuntimeClassicHiddenDiscoveryRegistry discoveries,
        Func<int, int, int, bool>? comparison = null)
    {
        _discoveries = discoveries ?? throw new ArgumentNullException(nameof(discoveries));
        _comparison = comparison ?? ((perception, difficulty, roll) => checked(perception + roll) >= difficulty);
    }

    public RuntimeClassicSearchResult Search(
        RuntimeClassicSearchActor actor,
        IEnumerable<RuntimeClassicHiddenTarget> roomTargets,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(roomTargets);
        ArgumentNullException.ThrowIfNull(random);
        if (actor.EffectivePerception < 0) throw new ArgumentOutOfRangeException(nameof(actor), "Effective Perception cannot be negative.");

        var rolls = new List<RuntimeClassicSearchRoll>();
        var found = new List<string>();
        foreach (var target in roomTargets.Where(x => x.RoomId.Equals(actor.RoomId, StringComparison.OrdinalIgnoreCase)))
        {
            var roll = random.Next(1, 101);
            var discovered = _comparison(actor.EffectivePerception, target.ConcealmentDifficulty, roll);
            rolls.Add(new(target.TargetId, roll, actor.EffectivePerception, target.ConcealmentDifficulty, discovered,
                "strongly_correlated", FormulaId));
            if (discovered) found.Add(target.TargetId);
        }

        _discoveries.ReplaceSearchResults(actor.CharacterId, found);
        return new(actor.CharacterId, actor.RoomId, rolls, found, Array.Empty<string>(),
            found.Count > 0 ? "You find something hidden." : "You find nothing.");
    }

    public bool CanCharacterSeeOrTake(string characterId, RuntimeItemInstance item)
    {
        if (!IsHidden(item)) return true;
        return _discoveries.IsDiscovered(characterId, item.InstanceId);
    }

    public static bool IsHidden(RuntimeItemInstance item) =>
        item.Metadata?.TryGetValue("hidden_at_cleanup", out var value) == true &&
        bool.TryParse(value, out var hidden) && hidden;
}

public sealed partial class RuntimeItemEngine
{
    /// <summary>Take a hidden room item only when it was in this character's latest SEARCH result.</summary>
    public RuntimeItemInstance TakeClassicHiddenRoomItem(
        string instanceId,
        string characterId,
        string roomId,
        RuntimeClassicHiddenDiscoveryRegistry discoveries) => Atomic(() =>
    {
        var item = Require(instanceId);
        if (!item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase) ||
            !item.LocationId.Equals(roomId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Hidden item is not on the specified room ground.");
        if (!RuntimeClassicHiddenSearchService.IsHidden(item))
            throw new InvalidOperationException("Item is not hidden by Classic cleanup.");
        if (!discoveries.IsDiscovered(characterId, instanceId))
            throw new InvalidOperationException("Item was not found by the character's latest search.");

        var metadata = item.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : item.Metadata.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        metadata.Remove("hidden_at_cleanup");
        metadata.Remove("hidden_at_cleanup_utc");
        metadata["revealed_by_pickup"] = "true";
        _items[instanceId] = item with { Metadata = metadata };
        var moved = TransferCore(instanceId, RuntimeItemLocations.Character, characterId, characterId, null, false);
        discoveries.ForgetTarget(instanceId);
        return moved;
    });

    /// <summary>
    /// Drops a carried item as a normal visible room object. Hidden metadata was removed on pickup,
    /// so this visible stack cannot merge with a still-hidden matching stack.
    /// </summary>
    public RuntimeItemInstance DropClassicVisible(string instanceId, string characterId, string roomId) => Atomic(() =>
    {
        var item = RequireCarried(instanceId, characterId);
        EnforceTransferRestrictions(item, "drop");
        var metadata = item.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : item.Metadata.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        metadata.Remove("hidden_at_cleanup");
        metadata.Remove("hidden_at_cleanup_utc");
        metadata.Remove("revealed_by_pickup");
        _items[instanceId] = item with { Metadata = metadata };
        return TransferCore(instanceId, RuntimeItemLocations.Room, roomId, null, null, false);
    });
}
