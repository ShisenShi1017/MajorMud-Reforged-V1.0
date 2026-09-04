namespace RealmGate.Server;

public enum RuntimeCharacterLifecyclePhase
{
    Detached,
    Loading,
    Restoring,
    EnteringWorld,
    Active,
    Disconnecting,
    Saving,
    Failed
}

public sealed record RuntimeCharacterLifecycleContext(
    string CharacterId,
    string SessionId,
    RuntimeCharacterLifecyclePhase Phase,
    DateTimeOffset StartedAtUtc,
    IReadOnlyList<string> CompletedSteps,
    string? FailureCode = null);

public sealed record RuntimeCharacterLifecycleHooks(
    Func<RuntimeCharacterRecord, RuntimeCharacterRecord> RestoreAbilityOwnership,
    Func<RuntimeCharacterRecord, RuntimeCharacterRecord> RestoreFlagsAndStatusEffects,
    Func<RuntimeCharacterRecord, RuntimeCharacterRecord> RestoreTimers,
    Func<RuntimeCharacterRecord, RuntimeCharacterRecord> RebuildStatistics,
    Func<RuntimeCharacterRecord, bool> ValidatePlacement,
    Action<RuntimeCharacterRecord, string> RegisterWorldMembership,
    Action<RuntimeCharacterRecord, string> RegisterRoomMembership,
    Action<string> CancelTrade,
    Action<string> DetachCombat,
    Action<string> DetachMovementAndResting,
    Action<string> RemoveRoomMembership,
    Action<string> RemoveWorldMembership,
    Action<string> DetachParty,
    Action<string> ClearTransientReferences)
{
    public static RuntimeCharacterLifecycleHooks NoExternalRegistries { get; } = new(
        character => character,
        character => character,
        character => character,
        character => character,
        character => !string.IsNullOrWhiteSpace(character.WorldId) && !string.IsNullOrWhiteSpace(character.RoomId),
        (_, _) => { }, (_, _) => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
}

public enum RuntimeCharacterNativeDependency
{
    WelcomeEventOrdering,
    ReconnectGraceAndSocketTakeover,
    LinkDeadCombatRetention,
    PartyLeadershipSuccession,
    TradeEscrowDisposition,
    OfflineCorpseDecayReconciliation
}

public sealed record RuntimeCharacterNativeDependencyDecision(
    RuntimeCharacterNativeDependency Dependency,
    string Classification,
    bool Executable,
    string EvidenceRequirement,
    string FailClosedBehavior);

public sealed partial class RuntimeCharacterLifecycleCoordinator
{
    public const string ContractVersion = "0166";
    public const string PhaseMarker = "character.lifecycle.phase";
    public const string SessionMarker = "character.lifecycle.session";
    public const string EventMarker = "character.lifecycle.last_event";
    public const string ProvenanceMarker = "character.lifecycle.provenance";
    public const string ReconnectMarker = "character.lifecycle.reconnect";
    public const string CleanupMarker = "character.lifecycle.cleanup";
    public const string SequenceMarker = "character.lifecycle.sequence";
    public const string TransitionMarker = "character.lifecycle.transition";


    private static readonly IReadOnlyDictionary<RuntimeCharacterNativeDependency, RuntimeCharacterNativeDependencyDecision> NativeDependencyDecisions =
        new Dictionary<RuntimeCharacterNativeDependency, RuntimeCharacterNativeDependencyDecision>
        {
            [RuntimeCharacterNativeDependency.WelcomeEventOrdering] = new(
                RuntimeCharacterNativeDependency.WelcomeEventOrdering, "UNKNOWN", false,
                "native executable message-dispatch trace",
                "use deterministic Reforged lifecycle completion without asserting native welcome ordering"),
            [RuntimeCharacterNativeDependency.ReconnectGraceAndSocketTakeover] = new(
                RuntimeCharacterNativeDependency.ReconnectGraceAndSocketTakeover, "EXCLUDED_WORLDGROUP", false,
                "Worldgroup host_session_user_state ownership is proven by retained native import/callsite evidence",
                "not scored as MajorMUD parity; raw socket/session takeover remains a Worldgroup host responsibility"),
            [RuntimeCharacterNativeDependency.LinkDeadCombatRetention] = new(
                RuntimeCharacterNativeDependency.LinkDeadCombatRetention, "UNKNOWN", false,
                "native combat/world-tick trace across link loss",
                "detach combat through the established ungraceful-disconnect cleanup pipeline"),
            [RuntimeCharacterNativeDependency.PartyLeadershipSuccession] = new(
                RuntimeCharacterNativeDependency.PartyLeadershipSuccession, "UNKNOWN", false,
                "native party dispatcher trace",
                "detach the Character without electing or inventing a successor"),
            [RuntimeCharacterNativeDependency.TradeEscrowDisposition] = new(
                RuntimeCharacterNativeDependency.TradeEscrowDisposition, "EXCLUDED_REFORGED_EXTENSION", false,
                "official MajorMUD command help exposes GIVE and AUCTION-as-chat, not a native escrow TRADE command",
                "not scored as MajorMUD parity; Reforged trade escrow remains Custom/AI-only"),
            [RuntimeCharacterNativeDependency.OfflineCorpseDecayReconciliation] = new(
                RuntimeCharacterNativeDependency.OfflineCorpseDecayReconciliation, "UNKNOWN", false,
                "native corpse timer/world-tick trace while the owner is offline",
                "preserve validated corpse state and reject speculative offline decay mutation")
        };

    public IReadOnlyList<RuntimeCharacterNativeDependencyDecision> NativeDependencies =>
        NativeDependencyDecisions.Values.OrderBy(x => x.Dependency).ToArray();

    public RuntimeCharacterNativeDependencyDecision GetNativeDependencyDecision(RuntimeCharacterNativeDependency dependency) =>
        NativeDependencyDecisions.TryGetValue(dependency, out var decision)
            ? decision
            : throw new InvalidDataException($"Unknown Character native dependency '{dependency}'.");

    public void RequireExecutableNativeDependency(RuntimeCharacterNativeDependency dependency)
    {
        var decision = GetNativeDependencyDecision(dependency);
        if (!decision.Executable)
            throw new NotSupportedException($"Character native dependency '{dependency}' remains {decision.Classification}: {decision.FailClosedBehavior}. Required evidence: {decision.EvidenceRequirement}.");
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeCharacterLifecycleContext> _active = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<RuntimeCharacterLifecycleContext> Active =>
        _active.Values.OrderBy(x => x.CharacterId, StringComparer.OrdinalIgnoreCase).ToArray();

    public RuntimeCharacterRecord Login(
        string characterId,
        string sessionId,
        Func<string, RuntimeCharacterRecord?> load,
        RuntimeCharacterLifecycleHooks hooks,
        DateTimeOffset now,
        bool reconnect = false)
    {
        var canonicalId = RequireCanonicalCharacterId(characterId);
        if (string.IsNullOrWhiteSpace(sessionId)) throw new InvalidDataException("Lifecycle session id is required.");
        var canonicalSessionId = sessionId.Trim();
        lock (_gate)
        {
            if (_active.ContainsKey(canonicalId)) throw new InvalidOperationException($"Character '{canonicalId}' already has active ownership.");
            _active[canonicalId] = new(canonicalId, canonicalSessionId, RuntimeCharacterLifecyclePhase.Loading, now, Array.Empty<string>());
        }

        var completed = new List<string>();
        var worldRegistered = false;
        var roomRegistered = false;
        try
        {
            var character = load(canonicalId) ?? throw new InvalidDataException($"Character '{canonicalId}' was not found.");
            completed.Add("load.validated-save");
            if (!character.CharacterId.Equals(canonicalId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Loaded CharacterId does not match the requested canonical id.");
            RuntimeCharacterRecordValidator.Validate(character);
            ValidatePersistentOwnership(character);
            completed.Add("ownership.inventory-equipment");

            character = hooks.RestoreAbilityOwnership(character);
            completed.Add("restore.abilities");
            character = hooks.RestoreFlagsAndStatusEffects(character);
            completed.Add("restore.flags-status");
            character = hooks.RestoreTimers(character);
            completed.Add("restore.timers");
            character = hooks.RebuildStatistics(character);
            completed.Add("restore.statistics");
            RuntimeCharacterDeathResolver.Validate(character);
            completed.Add("restore.death-corpse");
            if (!hooks.ValidatePlacement(character)) throw new InvalidDataException("Character room/world placement is contradictory or unresolved.");
            completed.Add("placement.validated");

            hooks.RegisterWorldMembership(character, canonicalSessionId);
            worldRegistered = true;
            completed.Add("membership.world");
            hooks.RegisterRoomMembership(character, canonicalSessionId);
            roomRegistered = true;
            completed.Add("membership.room");

            character = Mark(character, now, "login", canonicalSessionId, reconnect, "STRONGLY_CORRELATED", "load>ownership>abilities>status>timers>statistics>death>placement>membership");
            ValidateLifecycleMarkers(character, expectedActive: true, expectedSessionId: canonicalSessionId);
            RuntimeCharacterRecordValidator.Validate(character);
            completed.Add("provenance.persisted");
            lock (_gate) _active[canonicalId] = new(canonicalId, canonicalSessionId, RuntimeCharacterLifecyclePhase.Active, now, completed.ToArray());
            return character;
        }
        catch
        {
            if (roomRegistered) Safe(() => hooks.RemoveRoomMembership(canonicalId));
            if (worldRegistered) Safe(() => hooks.RemoveWorldMembership(canonicalId));
            Safe(() => hooks.DetachCombat(canonicalId));
            Safe(() => hooks.CancelTrade(canonicalId));
            Safe(() => hooks.ClearTransientReferences(canonicalId));
            lock (_gate) _active.Remove(canonicalId);
            throw;
        }
    }

    public RuntimeCharacterRecord Logout(
        RuntimeCharacterRecord character,
        string sessionId,
        RuntimeCharacterLifecycleHooks hooks,
        Action<RuntimeCharacterRecord, string> save,
        DateTimeOffset now,
        bool graceful)
    {
        var canonicalId = RequireCanonicalCharacterId(character.CharacterId);
        RuntimeCharacterLifecycleContext ownership;
        lock (_gate)
        {
            if (!_active.TryGetValue(canonicalId, out ownership!) || !ownership.SessionId.Equals(sessionId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Character '{canonicalId}' is not owned by session '{sessionId}'.");
            _active[canonicalId] = ownership with { Phase = RuntimeCharacterLifecyclePhase.Disconnecting };
        }

        var cleanup = new List<string>();
        try
        {
            hooks.CancelTrade(canonicalId); cleanup.Add("detach.trade");
            hooks.DetachCombat(canonicalId); cleanup.Add("detach.combat");
            hooks.DetachMovementAndResting(canonicalId); cleanup.Add("detach.movement-resting");
            hooks.RemoveRoomMembership(canonicalId); cleanup.Add("membership.room.removed");
            hooks.RemoveWorldMembership(canonicalId); cleanup.Add("membership.world.removed");
            hooks.DetachParty(canonicalId); cleanup.Add("detach.party-session");
            hooks.ClearTransientReferences(canonicalId); cleanup.Add("cleanup.transient");

            var marked = Mark(character, now, graceful ? "logout" : "disconnect", sessionId, false, "STRONGLY_CORRELATED", string.Join('>', cleanup));
            marked = MarkCleanup(marked, cleanup, now);
            ValidateLifecycleMarkers(marked, expectedActive: false, expectedSessionId: null);
            RuntimeCharacterRecordValidator.Validate(marked);
            lock (_gate) _active[canonicalId] = ownership with { Phase = RuntimeCharacterLifecyclePhase.Saving, CompletedSteps = cleanup.ToArray() };
            save(marked, graceful ? "lifecycle-logout" : "lifecycle-disconnect");
            cleanup.Add("save.final");
            lock (_gate) _active.Remove(canonicalId);
            return marked;
        }
        catch
        {
            Safe(() => hooks.RemoveRoomMembership(canonicalId));
            Safe(() => hooks.RemoveWorldMembership(canonicalId));
            Safe(() => hooks.DetachCombat(canonicalId));
            Safe(() => hooks.CancelTrade(canonicalId));
            Safe(() => hooks.ClearTransientReferences(canonicalId));
            lock (_gate) _active.Remove(canonicalId);
            throw;
        }
    }

    public void ValidateNoOrphans(IEnumerable<string> roomMembers, IEnumerable<string> worldMembers, IEnumerable<string> combatMembers, IEnumerable<string> sessionMembers)
    {
        var activeIds = Active.Select(x => x.CharacterId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in new[] { ("room", roomMembers), ("world", worldMembers), ("combat", combatMembers), ("session", sessionMembers) })
        {
            var orphan = pair.Item2.FirstOrDefault(id => !activeIds.Contains(id));
            if (orphan is not null) throw new InvalidDataException($"Orphaned {pair.Item1} lifecycle reference '{orphan}'.");
        }
    }

    public static void ValidatePersistentOwnership(RuntimeCharacterRecord character)
    {
        var state = character.ItemState;
        if (state is null) return;
        var items = state.Items.ToDictionary(x => x.InstanceId, StringComparer.OrdinalIgnoreCase);
        foreach (var item in state.Items)
        {
            if (string.IsNullOrWhiteSpace(item.InstanceId)) throw new InvalidDataException("Inventory contains an item without an instance id.");
            if (item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) || item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(item.OwnerId, character.CharacterId, StringComparison.OrdinalIgnoreCase) || !string.Equals(item.LocationId, character.CharacterId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Item '{item.InstanceId}' has invalid character ownership.");
            }
            if (item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(item.EquippedSlot))
                throw new InvalidDataException($"Equipped item '{item.InstanceId}' has no equipped slot.");
            if (item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase))
            {
                if (!items.TryGetValue(item.LocationId, out var parent)) throw new InvalidDataException($"Nested item '{item.InstanceId}' references a missing container.");
                if (!string.Equals(parent.OwnerId, character.CharacterId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Nested item '{item.InstanceId}' has contradictory container ownership.");
            }
        }
        foreach (var corpse in state.Corpses ?? Array.Empty<RuntimeCorpseState>())
        {
            if (!corpse.CharacterId.Equals(character.CharacterId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Corpse '{corpse.CorpseId}' is linked to another character.");
            if (string.IsNullOrWhiteSpace(corpse.RoomId)) throw new InvalidDataException($"Corpse '{corpse.CorpseId}' has no room linkage.");
        }
    }

    public RuntimeCharacterRecord RecoverStaleSession(
        RuntimeCharacterRecord character,
        string staleSessionId,
        RuntimeCharacterLifecycleHooks hooks,
        Action<RuntimeCharacterRecord, string> save,
        DateTimeOffset now)
    {
        var canonicalId = RequireCanonicalCharacterId(character.CharacterId);
        lock (_gate)
        {
            if (!_active.TryGetValue(canonicalId, out var ownership))
                throw new InvalidOperationException($"Character '{canonicalId}' has no active ownership to recover.");
            if (!ownership.SessionId.Equals(staleSessionId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Stale session '{staleSessionId}' does not own Character '{canonicalId}'.");
        }
        return Logout(character, staleSessionId, hooks, save, now, graceful: false);
    }

    public static void ValidateLifecycleMarkers(RuntimeCharacterRecord character, bool expectedActive, string? expectedSessionId)
    {
        var markers = character.SemanticState?.RuntimeMarkers ?? throw new InvalidDataException("Character lifecycle markers are missing.");
        var expectedPhase = expectedActive ? "active" : "detached";
        if (!markers.TryGetValue(PhaseMarker, out var phase) || !phase.Equals(expectedPhase, StringComparison.Ordinal))
            throw new InvalidDataException($"Character lifecycle phase is not '{expectedPhase}'.");
        if (!markers.TryGetValue(SessionMarker, out var session))
            throw new InvalidDataException("Character lifecycle session marker is missing.");
        if (expectedActive)
        {
            if (string.IsNullOrWhiteSpace(expectedSessionId) || !session.Equals(expectedSessionId, StringComparison.Ordinal))
                throw new InvalidDataException("Character lifecycle session ownership is contradictory.");
        }
        else if (!string.IsNullOrEmpty(session))
            throw new InvalidDataException("Detached Character retains active session ownership.");
        if (!markers.TryGetValue(SequenceMarker, out var rawSequence) || !long.TryParse(rawSequence, out var sequence) || sequence <= 0)
            throw new InvalidDataException("Character lifecycle sequence is missing or invalid.");
        if (!markers.TryGetValue(EventMarker, out var eventMarker) ||
            (!eventMarker.EndsWith("|login", StringComparison.Ordinal) && !eventMarker.EndsWith("|logout", StringComparison.Ordinal) && !eventMarker.EndsWith("|disconnect", StringComparison.Ordinal)))
            throw new InvalidDataException("Character lifecycle event marker is missing or invalid.");
        var expectedEvent = expectedActive ? "login" : eventMarker.EndsWith("|logout", StringComparison.Ordinal) ? "logout" : "disconnect";
        if (!markers.TryGetValue(TransitionMarker, out var transition) || !transition.Equals($"{sequence}|{expectedEvent}|complete", StringComparison.Ordinal))
            throw new InvalidDataException("Character lifecycle transition is incomplete or contradictory.");
        if (!markers.TryGetValue(ProvenanceMarker, out var provenance) || !provenance.StartsWith($"{ContractVersion}|", StringComparison.Ordinal))
            throw new InvalidDataException("Character lifecycle provenance is missing or stale.");
    }

    private static RuntimeCharacterRecord Mark(RuntimeCharacterRecord character, DateTimeOffset now, string evt, string sessionId, bool reconnect, string classification, string provenance)
    {
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase);
        var previousSequence = 0L;
        if (markers.TryGetValue(SequenceMarker, out var rawSequence) &&
            (!long.TryParse(rawSequence, out previousSequence) || previousSequence < 0))
            throw new InvalidDataException("Character lifecycle sequence marker is invalid.");
        var nextSequence = checked(previousSequence + 1L);
        if (markers.TryGetValue(EventMarker, out var previousEvent))
        {
            var separator = previousEvent.IndexOf('|');
            if (separator <= 0 || !DateTimeOffset.TryParse(previousEvent[..separator], out var previousAt))
                throw new InvalidDataException("Character lifecycle event marker is invalid.");
            if (previousAt > now.ToUniversalTime())
                throw new InvalidDataException("Character lifecycle event time moved backwards.");
        }
        markers[PhaseMarker] = evt is "login" ? "active" : "detached";
        markers[SessionMarker] = evt is "login" ? sessionId : string.Empty;
        markers[EventMarker] = $"{now.ToUniversalTime():O}|{evt}";
        markers[ProvenanceMarker] = $"{ContractVersion}|{classification}|{provenance}";
        markers[SequenceMarker] = nextSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        markers[TransitionMarker] = $"{nextSequence}|{evt}|complete";
        if (reconnect) markers[ReconnectMarker] = $"{now.ToUniversalTime():O}|validated-clean-ownership";
        return character with { SemanticState = semantic with { RuntimeMarkers = markers }, UpdatedAtUtc = now };
    }

    private static RuntimeCharacterRecord MarkCleanup(RuntimeCharacterRecord character, IReadOnlyList<string> cleanup, DateTimeOffset now)
    {
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
        {
            [CleanupMarker] = $"{now.ToUniversalTime():O}|{string.Join(',', cleanup)}"
        };
        return character with { SemanticState = semantic with { RuntimeMarkers = markers } };
    }

    private static string RequireCanonicalCharacterId(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)) throw new InvalidDataException("Character id is required.");
        var trimmed = characterId.Trim();
        if (!trimmed.Contains(':') || trimmed.Any(char.IsWhiteSpace) || trimmed != trimmed.ToLowerInvariant())
            throw new InvalidDataException($"Character id '{characterId}' is not canonical.");
        return trimmed;
    }

    private static void Safe(Action action) { try { action(); } catch { } }
}
