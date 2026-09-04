using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealmGate.App.Services;

namespace RealmGate.App.Brains;

/// <summary>
/// A compact, world-scoped view of lessons Borin learned from verified Client
/// observations. Player advice, remembered stories, and model output never enter
/// this store as proof.
/// </summary>
public sealed record BorinGameplayLearningSnapshot(
    string Server,
    string World,
    string State,
    IReadOnlyList<string> RelevantLessons,
    int VerifiedObservationCount,
    DateTimeOffset? LastObservedAt,
    string StoragePath)
{
    public static BorinGameplayLearningSnapshot Empty { get; } = new(
        "Unknown server",
        "Unknown world",
        "No verified gameplay lessons are available for this world.",
        Array.Empty<string>(),
        0,
        null,
        string.Empty);

    public string Summary =>
        $"{Server}/{World} • verified gameplay observations • {State}";

    public string PromptText => BuildPrompt(externalConversation: false);
    public string ExternalPromptText => BuildPrompt(externalConversation: true);

    private string BuildPrompt(bool externalConversation)
    {
        var lessons = RelevantLessons.Count == 0
            ? "- No verified gameplay lesson is relevant to this turn. Admit uncertainty instead of inventing a route, item effect, monster rule, command result, or strategy."
            : string.Join("\n", RelevantLessons
                .Take(externalConversation ? 4 : 8)
                .Select(item => $"- {item}"));
        var privateState = externalConversation
            ? "- Do not mention storage paths, internal counters, maintenance, raw terminal lines, host details, private Forge context, or lessons from another server/world."
            : $"- Verified Client observations recorded for this world: {VerifiedObservationCount}. Last observation: {(LastObservedAt is null ? "not yet" : LastObservedAt.Value.ToString("yyyy-MM-dd HH:mm:ss zzz"))}.";

        return $"""
VERIFIED GAMEPLAY LEARNING CONTRACT
- Scope: Borin's character on {Server}/{World} only.
- These are observations made by the Client while Borin was verified inside MajorMUD. They are not decoded server rules and may be incomplete, conditional, or server-specific.
- A successful observed transition proves only that the transition occurred under the observed conditions. A blocked exit proves only that it was blocked at that time.
- Repeated danger or recovery patterns are cautions, not guarantees. Current live game state always overrides historical lessons.
- Player advice, Borin's generated replies, goals, memories, and inferred explanations are never evidence here.
- Never turn a lesson into permission to move, fight, buy, equip, rest, send commands, or take any other action.
{privateState}

Relevant verified lessons:
{lessons}
""";
    }
}

public interface IBorinGameplayLearningStore
{
    string StoragePath { get; }

    Task ObserveCommandAsync(
        string server,
        string world,
        string character,
        string command,
        BorinGameStateSnapshot state,
        DateTimeOffset timestamp,
        CancellationToken token = default);

    Task ObserveTerminalAsync(
        string server,
        string world,
        string character,
        string classification,
        string line,
        string command,
        BorinGameStateSnapshot state,
        DateTimeOffset timestamp,
        CancellationToken token = default);

    Task<BorinGameplayLearningSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default);

    Task<BorinGameplayLearningSnapshot> GetSnapshotAsync(
        CancellationToken token = default);
}

/// <summary>
/// Deterministically learns from parsed Client events. The language model is not
/// used to decide what happened, how confident a lesson is, or what is retained.
/// </summary>
public sealed class BorinGameplayLearningStore : IBorinGameplayLearningStore
{
    private const int MaximumRoutes = 160;
    private const int MaximumOutcomes = 160;
    private const int MaximumDangers = 80;
    private const int MaximumRecoveries = 60;
    private const int MaximumItems = 120;
    private const int MaximumMilestones = 80;
    private const int MaximumLevelingAreas = 80;
    private const int MaximumWorldProfiles = 64;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WordRegex = new(
        @"[A-Za-z0-9][A-Za-z0-9'_-]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex VictoryRegex = new(
        @"you\s+(?:(?:have\s+)?(?:killed|defeated|slain)|slew)\s+(?<target>[^.!]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DefeatRegex = new(
        @"(?:you\s+(?:are|have been)\s+(?:dead|killed|defeated|slain)|your\s+adventure\s+is\s+over)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> LearnableCommandVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "BUY", "PURCHASE", "EQUIP", "WEAR", "WIELD", "GET", "TAKE", "PICKUP", "REST",
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "that", "this", "with", "from", "have", "will", "your", "you", "are", "was", "were",
        "for", "but", "not", "can", "could", "would", "should", "into", "about", "what", "when", "where", "why",
        "how", "him", "his", "her", "our", "out", "now", "then", "than", "just", "like", "want", "need", "please",
        "borin", "current", "game", "majormud", "room", "there", "they", "them", "their", "been", "being", "some",
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IAppLogger _logger;
    private string _lastWorldKey = string.Empty;

    public BorinGameplayLearningStore(string repositoryRoot, IAppLogger logger)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        StoragePath = Path.Combine(Path.GetFullPath(repositoryRoot), "AI", "Memory", "borin-gameplay-learning.json");
    }

    public string StoragePath { get; }

    public async Task ObserveCommandAsync(
        string server,
        string world,
        string character,
        string command,
        BorinGameStateSnapshot state,
        DateTimeOffset timestamp,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        command = NormalizeCommand(command);
        if (command.Length == 0 || command.Equals("[HIDDEN CREDENTIAL]", StringComparison.OrdinalIgnoreCase))
            return;

        if (!state.HasVerifiedLiveState || !IsLearnableCommand(command))
            return;

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var profile = GetOrCreateWorld(document, server, world, character);
            _lastWorldKey = profile.Key;
            document.LastWorldKey = profile.Key;
            document.LastUpdatedAt = timestamp;

            profile.PendingCommand = TrimTo(command, 180);
            profile.PendingCommandAt = timestamp;
            profile.PendingOriginRoom = state.HasVerifiedLiveState ? NormalizeText(state.CurrentRoom) : string.Empty;
            profile.PendingOriginRoomKey = state.HasVerifiedLiveState ? NormalizeText(state.CurrentRoomKey) : string.Empty;
            profile.LastObservedAt = timestamp;
            if (state.HasVerifiedLiveState)
                UpdateLiveBaseline(profile, state);

            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ObserveTerminalAsync(
        string server,
        string world,
        string character,
        string classification,
        string line,
        string command,
        BorinGameStateSnapshot state,
        DateTimeOffset timestamp,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        classification = NormalizeText(classification);
        line = TrimTo(NormalizeText(line), 600);
        command = NormalizeCommand(command);
        if (classification.Length == 0 || IsUnlearnableClassification(classification))
            return;

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var profile = GetOrCreateWorld(document, server, world, character);
            _lastWorldKey = profile.Key;
            document.LastWorldKey = profile.Key;
            document.LastUpdatedAt = timestamp;
            profile.LastObservedAt = timestamp;

            if (!state.HasVerifiedLiveState)
            {
                if (classification.Equals("Disconnected", StringComparison.OrdinalIgnoreCase))
                    ClearPending(profile);
                await SaveUnsafeAsync(document, token);
                return;
            }

            profile.VerifiedObservationCount++;
            var pending = ResolvePendingCommand(profile, command, timestamp);
            var currentRoom = NormalizeText(state.CurrentRoom);
            var currentRoomKey = NormalizeText(state.CurrentRoomKey);
            var changed = false;

            if (classification.Equals("RoomEntered", StringComparison.OrdinalIgnoreCase))
            {
                var direction = CanonicalDirection(pending);
                var origin = NormalizeText(profile.PendingOriginRoom);
                var originKey = NormalizeText(profile.PendingOriginRoomKey);
                if (direction.Length > 0 && origin.Length > 0 && currentRoom.Length > 0 &&
                    !origin.Equals(currentRoom, StringComparison.OrdinalIgnoreCase))
                {
                    changed |= UpsertRoute(profile, origin, originKey, direction, currentRoom, currentRoomKey, timestamp);
                }
                ClearPending(profile);
            }
            else if (classification.Equals("MovementFailed", StringComparison.OrdinalIgnoreCase))
            {
                var direction = CanonicalDirection(pending);
                if (direction.Length > 0)
                {
                    changed |= UpsertOutcome(
                        profile,
                        "BlockedExit",
                        $"{NormalizeRoom(currentRoom)}|{direction}",
                        pending,
                        currentRoom,
                        false,
                        $"Movement {direction} was rejected by the server in {NormalizeRoom(currentRoom)}.",
                        timestamp);
                }
                ClearPending(profile);
            }
            else if (classification.Equals("PurchaseSucceeded", StringComparison.OrdinalIgnoreCase))
            {
                var item = CommandArgument(pending, "BUY", "PURCHASE");
                if (item.Length > 0)
                {
                    changed |= UpsertOutcome(profile, "Purchase", OutcomeKey(pending, currentRoom), pending, currentRoom, true,
                        $"The server confirmed {DisplayCommand(pending)} in {NormalizeRoom(currentRoom)}.", timestamp);
                    changed |= UpsertItem(profile, item, "obtained through a confirmed purchase", currentRoom, timestamp);
                }
                ClearPending(profile);
            }
            else if (classification.Equals("PurchaseFailed", StringComparison.OrdinalIgnoreCase))
            {
                var item = CommandArgument(pending, "BUY", "PURCHASE");
                if (item.Length > 0)
                {
                    changed |= UpsertOutcome(profile, "Purchase", OutcomeKey(pending, currentRoom), pending, currentRoom, false,
                        $"The server rejected {DisplayCommand(pending)} in {NormalizeRoom(currentRoom)}.", timestamp);
                }
                ClearPending(profile);
            }
            else if (classification.Equals("ItemEquipped", StringComparison.OrdinalIgnoreCase))
            {
                var item = CommandArgument(pending, "EQUIP", "WEAR", "WIELD");
                if (item.Length > 0)
                {
                    changed |= UpsertOutcome(profile, "Equipment", OutcomeKey(pending, currentRoom), pending, currentRoom, true,
                        $"The server confirmed {DisplayCommand(pending)} in {NormalizeRoom(currentRoom)}.", timestamp);
                    changed |= UpsertItem(profile, item, "confirmed equipped", currentRoom, timestamp);
                }
                ClearPending(profile);
            }
            else if (classification.Equals("InventoryUpdated", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in state.InventoryItems.Take(20))
                    changed |= UpsertItem(profile, item, "observed in inventory", currentRoom, timestamp);
            }
            else if (classification.Equals("EquipmentUpdated", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in state.EquippedItems.Take(20))
                    changed |= UpsertItem(profile, item, "observed equipped", currentRoom, timestamp);
            }
            else if (classification.Equals("Combat", StringComparison.OrdinalIgnoreCase) ||
                     classification.Equals("CombatVictory", StringComparison.OrdinalIgnoreCase) ||
                     classification.Equals("CombatDefeat", StringComparison.OrdinalIgnoreCase))
            {
                changed |= ObserveCombat(profile, line, currentRoom, timestamp);
            }
            else if (classification.Equals("ExperienceGained", StringComparison.OrdinalIgnoreCase) ||
                     classification.Equals("LevelGained", StringComparison.OrdinalIgnoreCase))
            {
                changed |= UpsertMilestone(profile, classification, line, currentRoom, state, timestamp);
            }
            else if (classification.Equals("ItemAcquired", StringComparison.OrdinalIgnoreCase))
            {
                var item = CommandArgument(pending, "GET", "TAKE", "PICKUP");
                if (item.Length > 0)
                {
                    changed |= UpsertOutcome(profile, "Acquisition", OutcomeKey(pending, currentRoom), pending, currentRoom, true,
                        $"The server confirmed an item acquisition in {NormalizeRoom(currentRoom)}.", timestamp);
                    changed |= UpsertItem(profile, item, "confirmed acquired", currentRoom, timestamp);
                }
                ClearPending(profile);
            }
            else if (classification.Equals("RestStarted", StringComparison.OrdinalIgnoreCase))
            {
                profile.RestStartedAt = timestamp;
                profile.RestStartedHealth = state.HealthPercent;
                profile.RestStartedRoom = currentRoom;
                changed = true;
            }
            else if (classification.Equals("RestEnded", StringComparison.OrdinalIgnoreCase))
            {
                changed |= ObserveRestOutcome(profile, currentRoom, state.HealthPercent, timestamp);
            }

            changed |= ObserveExperienceProgress(profile, currentRoom, state, timestamp);
            changed |= ObserveVitalChange(profile, currentRoom, state, timestamp);
            if (changed)
                profile.LastLearnedAt = timestamp;
            UpdateLiveBaseline(profile, state);
            ExpirePending(profile, timestamp);
            TrimProfile(profile);

            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinGameplayLearningSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var state = context.Conversation.GameState;
            var hasNamedScope = HasNamedScope(state.Server, state.World, state.CharacterName);
            var profile = hasNamedScope
                ? FindWorld(document, state.Server, state.World, state.CharacterName)
                : FindWorldByKey(document, document.LastWorldKey)
                  ?? FindWorldByKey(document, _lastWorldKey);
            if (profile is null)
                return BorinGameplayLearningSnapshot.Empty;

            _lastWorldKey = profile.Key;
            return CreateSnapshot(profile, context.Request, state);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinGameplayLearningSnapshot> GetSnapshotAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var profile = FindWorldByKey(document, _lastWorldKey)
                ?? FindWorldByKey(document, document.LastWorldKey)
                ?? document.Worlds.Values.OrderByDescending(item => item.LastObservedAt).FirstOrDefault();
            return profile is null
                ? BorinGameplayLearningSnapshot.Empty
                : CreateSnapshot(profile, string.Empty, BorinGameStateSnapshot.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    private BorinGameplayLearningSnapshot CreateSnapshot(
        WorldLearningState profile,
        string request,
        BorinGameStateSnapshot state)
    {
        var room = state.HasVerifiedLiveState ? NormalizeText(state.CurrentRoom) : string.Empty;
        var terms = ExtractTerms(request);
        var candidates = new List<(int Score, string Text)>();

        foreach (var route in profile.Routes)
        {
            var score = 35 + Math.Min(30, route.Observations * 6) + RecencyScore(route.LastObservedAt);
            if (MatchesRoom(route.FromRoom, room) || MatchesRoom(route.ToRoom, room)) score += 80;
            if (MatchesTerms($"{route.FromRoom} {route.Direction} {route.ToRoom}", terms)) score += 45;
            candidates.Add((score,
                $"Verified transition: from {route.FromRoom}, command {route.Direction} reached {route.ToRoom}. Observed {route.Observations} time(s); last seen {AgeLabel(route.LastObservedAt)}."));
        }

        foreach (var outcome in profile.Outcomes)
        {
            var score = 25 + Math.Min(30, outcome.EvidenceCount * 5) + RecencyScore(outcome.LastObservedAt);
            if (MatchesRoom(outcome.Room, room)) score += 70;
            if (MatchesTerms($"{outcome.Command} {outcome.Detail} {outcome.Room}", terms)) score += 45;
            var result = outcome.Successes > 0 && outcome.Failures == 0
                ? "confirmed successful"
                : outcome.Failures > 0 && outcome.Successes == 0
                    ? "observed failing or blocked"
                    : "observed with mixed outcomes";
            candidates.Add((score,
                $"Observed command outcome: {DisplayCommand(outcome.Command)} was {result} in {NormalizeRoom(outcome.Room)}. Evidence: {outcome.EvidenceCount} event(s). {outcome.Detail}"));
        }

        foreach (var danger in profile.Dangers.Where(item => item.EvidenceCount >= 2 || item.MaximumHealthDrop >= 35 || item.Defeats > 0))
        {
            var score = 40 + Math.Min(35, danger.EvidenceCount * 7) + danger.MaximumHealthDrop + RecencyScore(danger.LastObservedAt);
            if (MatchesRoom(danger.Room, room)) score += 100;
            if (MatchesTerms($"{danger.Room} {danger.Subject}", terms)) score += 45;
            var defeat = danger.Defeats > 0 ? $" Defeat was observed {danger.Defeats} time(s)." : string.Empty;
            candidates.Add((score,
                $"Repeated danger signal: {NormalizeRoom(danger.Room)} produced {danger.EvidenceCount} verified danger event(s), with a maximum observed HP drop of {danger.MaximumHealthDrop} percentage points.{defeat}"));
        }

        foreach (var recovery in profile.Recoveries.Where(item => item.Observations >= 1))
        {
            var score = 18 + recovery.Observations * 4 + RecencyScore(recovery.LastObservedAt);
            if (MatchesRoom(recovery.Room, room)) score += 45;
            if (MatchesTerms($"{recovery.Room} rest recovery health", terms)) score += 35;
            candidates.Add((score,
                $"Observed recovery: resting in {NormalizeRoom(recovery.Room)} restored about {recovery.MaximumHealthGain} HP percentage points in the best verified observation ({recovery.Observations} observation(s))."));
        }

        foreach (var item in profile.Items)
        {
            var score = 12 + Math.Min(20, item.Observations * 3) + RecencyScore(item.LastObservedAt);
            if (MatchesRoom(item.Room, room)) score += 25;
            if (MatchesTerms($"{item.Item} {item.Status} {item.Room}", terms)) score += 65;
            candidates.Add((score,
                $"Verified item observation: {item.Item} was {item.Status} in {NormalizeRoom(item.Room)}. This does not prove hidden effects or permanent ownership."));
        }

        foreach (var milestone in profile.Milestones)
        {
            var score = 28 + Math.Min(30, milestone.Observations * 5) + RecencyScore(milestone.LastObservedAt);
            if (MatchesRoom(milestone.Room, room)) score += 30;
            if (MatchesTerms($"{milestone.Kind} {milestone.Detail} level experience {milestone.Room}", terms)) score += 55;
            candidates.Add((score,
                $"Verified progression event: {milestone.Detail} Observed {milestone.Observations} time(s) in {NormalizeRoom(milestone.Room)}."));
        }

        foreach (var area in profile.LevelingAreas)
        {
            var score = 34 + Math.Min(36, area.Observations * 6) + RecencyScore(area.LastObservedAt);
            if (MatchesRoom(area.Room, room)) score += 75;
            if (MatchesTerms($"{area.Room} experience xp leveling level", terms)) score += 60;
            var average = area.Observations == 0 ? 0 : area.TotalObservedExperienceIncrease / area.Observations;
            var levelRange = area.MinimumCharacterLevel > 0 && area.MaximumCharacterLevel > 0
                ? area.MinimumCharacterLevel == area.MaximumCharacterLevel
                    ? $"level {area.MinimumCharacterLevel}"
                    : $"levels {area.MinimumCharacterLevel}-{area.MaximumCharacterLevel}"
                : "an uncaptured level";
            candidates.Add((score,
                $"Verified leveling experience: at {levelRange} in {NormalizeRoom(area.Room)}, captured experience increased by {area.TotalObservedExperienceIncrease:N0} across {area.Observations} verified increase event(s) (average {average:N0}, largest {area.MaximumObservedExperienceIncrease:N0}). This is Borin's historical character experience, not a guarantee of future XP or safety."));
        }

        var lessons = candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
        var stateText = profile.VerifiedObservationCount == 0
            ? "No verified gameplay observations have been retained yet."
            : lessons.Length == 0
                ? "Verified observations exist, but none are relevant enough for this turn."
                : "Relevant lessons selected from deterministic Client evidence.";

        return new BorinGameplayLearningSnapshot(
            profile.Server,
            profile.World,
            stateText,
            lessons,
            profile.VerifiedObservationCount,
            profile.LastObservedAt == default ? null : profile.LastObservedAt,
            StoragePath);
    }

    private static bool ObserveCombat(
        WorldLearningState profile,
        string line,
        string room,
        DateTimeOffset timestamp)
    {
        var changed = false;
        var victory = VictoryRegex.Match(line);
        if (victory.Success)
        {
            var target = NormalizeText(victory.Groups["target"].Value);
            var danger = GetOrCreateDanger(profile, room, target.Length == 0 ? "combat opponent" : target);
            danger.Victories++;
            danger.EvidenceCount++;
            danger.LastObservedAt = timestamp;
            changed = true;
        }

        if (DefeatRegex.IsMatch(line))
        {
            var danger = GetOrCreateDanger(profile, room, "combat defeat");
            danger.Defeats++;
            danger.EvidenceCount++;
            danger.MaximumHealthDrop = Math.Max(danger.MaximumHealthDrop, Math.Max(0, profile.LastHealthPercent));
            danger.LastObservedAt = timestamp;
            changed = true;
        }

        return changed;
    }

    private static bool ObserveExperienceProgress(
        WorldLearningState profile,
        string room,
        BorinGameStateSnapshot state,
        DateTimeOffset timestamp)
    {
        if (!state.HasVerifiedLiveState || state.ExperienceCurrent <= 0 || profile.LastExperienceCurrent <= 0)
            return false;
        if (profile.LastCharacterLevel > 0 && state.CharacterLevel > 0 && state.CharacterLevel != profile.LastCharacterLevel)
            return false;

        var increase = state.ExperienceCurrent - profile.LastExperienceCurrent;
        if (increase <= 0 || increase > 50_000_000)
            return false;

        var normalizedRoom = NormalizeRoom(room);
        var area = profile.LevelingAreas.FirstOrDefault(item => MatchesRoom(item.Room, normalizedRoom));
        if (area is null)
        {
            area = new LevelingAreaObservation { Room = normalizedRoom };
            profile.LevelingAreas.Add(area);
        }

        area.Observations++;
        area.TotalObservedExperienceIncrease += increase;
        area.MaximumObservedExperienceIncrease = Math.Max(area.MaximumObservedExperienceIncrease, increase);
        if (state.CharacterLevel > 0)
        {
            area.MinimumCharacterLevel = area.MinimumCharacterLevel <= 0
                ? state.CharacterLevel
                : Math.Min(area.MinimumCharacterLevel, state.CharacterLevel);
            area.MaximumCharacterLevel = Math.Max(area.MaximumCharacterLevel, state.CharacterLevel);
        }
        area.LastObservedAt = timestamp;
        return true;
    }

    private static bool ObserveVitalChange(
        WorldLearningState profile,
        string room,
        BorinGameStateSnapshot state,
        DateTimeOffset timestamp)
    {
        if (profile.LastHealthPercent <= 0 || state.HealthPercent >= profile.LastHealthPercent)
            return false;

        var drop = profile.LastHealthPercent - state.HealthPercent;
        if (drop < 15)
            return false;

        var danger = GetOrCreateDanger(profile, room, state.InCombat ? "combat damage" : "health loss");
        danger.EvidenceCount++;
        danger.MaximumHealthDrop = Math.Max(danger.MaximumHealthDrop, drop);
        danger.LastObservedAt = timestamp;
        return true;
    }

    private static bool ObserveRestOutcome(
        WorldLearningState profile,
        string room,
        int currentHealth,
        DateTimeOffset timestamp)
    {
        if (profile.RestStartedAt == default)
            return false;

        var gain = Math.Max(0, currentHealth - profile.RestStartedHealth);
        var restRoom = NormalizeText(profile.RestStartedRoom);
        if (restRoom.Length == 0)
            restRoom = room;
        profile.RestStartedAt = default;
        profile.RestStartedHealth = 0;
        profile.RestStartedRoom = string.Empty;
        if (gain <= 0)
            return false;

        var recovery = profile.Recoveries.FirstOrDefault(item => MatchesRoom(item.Room, restRoom));
        if (recovery is null)
        {
            recovery = new RecoveryObservation { Room = NormalizeRoom(restRoom) };
            profile.Recoveries.Add(recovery);
        }
        recovery.Observations++;
        recovery.MaximumHealthGain = Math.Max(recovery.MaximumHealthGain, gain);
        recovery.LastObservedAt = timestamp;
        return true;
    }

    private static bool UpsertRoute(
        WorldLearningState profile,
        string fromRoom,
        string fromRoomKey,
        string direction,
        string toRoom,
        string toRoomKey,
        DateTimeOffset timestamp)
    {
        var route = profile.Routes.FirstOrDefault(item =>
            MatchesRoom(item.FromRoom, fromRoom) &&
            item.Direction.Equals(direction, StringComparison.OrdinalIgnoreCase) &&
            MatchesRoom(item.ToRoom, toRoom));
        if (route is null)
        {
            route = new RouteObservation
            {
                FromRoom = NormalizeRoom(fromRoom),
                FromRoomKey = TrimTo(NormalizeText(fromRoomKey), 120),
                Direction = direction,
                ToRoom = NormalizeRoom(toRoom),
                ToRoomKey = TrimTo(NormalizeText(toRoomKey), 120),
            };
            profile.Routes.Add(route);
        }
        route.Observations++;
        route.LastObservedAt = timestamp;
        return true;
    }

    private static bool UpsertOutcome(
        WorldLearningState profile,
        string category,
        string key,
        string command,
        string room,
        bool success,
        string detail,
        DateTimeOffset timestamp)
    {
        command = NormalizeCommand(command);
        if (command.Length == 0)
            command = "uncaptured command";
        var outcome = profile.Outcomes.FirstOrDefault(item =>
            item.Category.Equals(category, StringComparison.OrdinalIgnoreCase) &&
            item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (outcome is null)
        {
            outcome = new CommandOutcome
            {
                Category = category,
                Key = TrimTo(key, 260),
                Command = TrimTo(command, 180),
                Room = NormalizeRoom(room),
            };
            profile.Outcomes.Add(outcome);
        }
        if (success) outcome.Successes++; else outcome.Failures++;
        outcome.EvidenceCount++;
        outcome.Detail = TrimTo(detail, 320);
        outcome.LastObservedAt = timestamp;
        return true;
    }

    private static bool UpsertMilestone(
        WorldLearningState profile,
        string kind,
        string line,
        string room,
        BorinGameStateSnapshot state,
        DateTimeOffset timestamp)
    {
        kind = TrimTo(NormalizeText(kind), 80);
        var detail = kind.Equals("LevelGained", StringComparison.OrdinalIgnoreCase) && state.CharacterLevel > 0
            ? $"The Client observed Borin reaching level {state.CharacterLevel}."
            : kind.Equals("ExperienceGained", StringComparison.OrdinalIgnoreCase)
                ? $"The server reported experience gain; captured XP is {state.ExperienceCurrent:N0}" +
                  (state.ExperienceNeeded > 0 ? $"/{state.ExperienceNeeded:N0}." : ".")
                : TrimTo(line, 260);
        var key = $"{kind}|{NormalizeRoom(room)}|{detail}";
        var milestone = profile.Milestones.FirstOrDefault(existing =>
            existing.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (milestone is null)
        {
            milestone = new MilestoneObservation
            {
                Key = TrimTo(key, 400),
                Kind = kind,
                Detail = TrimTo(detail, 300),
                Room = NormalizeRoom(room),
            };
            profile.Milestones.Add(milestone);
        }
        milestone.Observations++;
        milestone.LastObservedAt = timestamp;
        return true;
    }

    private static bool UpsertItem(
        WorldLearningState profile,
        string item,
        string status,
        string room,
        DateTimeOffset timestamp)
    {
        item = TrimTo(NormalizeText(item), 120);
        if (item.Length == 0)
            return false;
        var observation = profile.Items.FirstOrDefault(existing =>
            existing.Item.Equals(item, StringComparison.OrdinalIgnoreCase) &&
            existing.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        if (observation is null)
        {
            observation = new ItemObservation { Item = item, Status = status };
            profile.Items.Add(observation);
        }
        observation.Room = NormalizeRoom(room);
        observation.Observations++;
        observation.LastObservedAt = timestamp;
        return true;
    }

    private static DangerObservation GetOrCreateDanger(WorldLearningState profile, string room, string subject)
    {
        room = NormalizeRoom(room);
        subject = TrimTo(NormalizeText(subject), 120);
        if (subject.Length == 0)
            subject = "danger";
        var danger = profile.Dangers.FirstOrDefault(item =>
            MatchesRoom(item.Room, room) && item.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase));
        if (danger is not null)
            return danger;
        danger = new DangerObservation { Room = room, Subject = subject };
        profile.Dangers.Add(danger);
        return danger;
    }

    private static void UpdateLiveBaseline(WorldLearningState profile, BorinGameStateSnapshot state)
    {
        if (!state.HasVerifiedLiveState)
            return;
        profile.LastRoom = NormalizeText(state.CurrentRoom);
        profile.LastRoomKey = NormalizeText(state.CurrentRoomKey);
        profile.LastHealthPercent = state.HealthPercent;
        profile.LastManaPercent = state.ManaPercent;
        profile.LastCharacterLevel = state.CharacterLevel;
        profile.LastExperienceCurrent = state.ExperienceCurrent;
        profile.LastExperienceNeeded = state.ExperienceNeeded;
        profile.LastExperienceObservedAt = DateTimeOffset.Now;
    }

    private static string ResolvePendingCommand(WorldLearningState profile, string supplied, DateTimeOffset timestamp)
    {
        if (profile.PendingCommandAt != default)
        {
            var pendingAge = timestamp - profile.PendingCommandAt;
            if (pendingAge >= TimeSpan.Zero && pendingAge <= TimeSpan.FromSeconds(45))
                return NormalizeCommand(profile.PendingCommand);
        }

        supplied = NormalizeCommand(supplied);
        return supplied.Length > 0 && IsLearnableCommand(supplied)
            ? supplied
            : string.Empty;
    }

    private static void ExpirePending(WorldLearningState profile, DateTimeOffset timestamp)
    {
        if (profile.PendingCommandAt != default && timestamp - profile.PendingCommandAt > TimeSpan.FromSeconds(45))
            ClearPending(profile);
    }

    private static void ClearPending(WorldLearningState profile)
    {
        profile.PendingCommand = string.Empty;
        profile.PendingCommandAt = default;
        profile.PendingOriginRoom = string.Empty;
        profile.PendingOriginRoomKey = string.Empty;
    }

    private static void TrimProfile(WorldLearningState profile)
    {
        TrimOldest(profile.Routes, MaximumRoutes, item => item.LastObservedAt);
        TrimOldest(profile.Outcomes, MaximumOutcomes, item => item.LastObservedAt);
        TrimOldest(profile.Dangers, MaximumDangers, item => item.LastObservedAt);
        TrimOldest(profile.Recoveries, MaximumRecoveries, item => item.LastObservedAt);
        TrimOldest(profile.Items, MaximumItems, item => item.LastObservedAt);
        TrimOldest(profile.Milestones, MaximumMilestones, item => item.LastObservedAt);
        TrimOldest(profile.LevelingAreas, MaximumLevelingAreas, item => item.LastObservedAt);
    }


    private static void TrimDocument(GameplayLearningDocument document)
    {
        document.Worlds ??= new Dictionary<string, WorldLearningState>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in document.Worlds.Values)
            TrimProfile(profile);

        if (document.Worlds.Count <= MaximumWorldProfiles)
            return;

        var keep = document.Worlds.Values
            .OrderByDescending(item => item.LastObservedAt)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumWorldProfiles)
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in document.Worlds.Keys.Where(key => !keep.Contains(key)).ToArray())
            document.Worlds.Remove(key);

        if (document.LastWorldKey.Length > 0 && !document.Worlds.ContainsKey(document.LastWorldKey))
            document.LastWorldKey = document.Worlds.Values.OrderByDescending(item => item.LastObservedAt).FirstOrDefault()?.Key ?? string.Empty;
    }
    private static void TrimOldest<T>(List<T> items, int maximum, Func<T, DateTimeOffset> timestamp)
    {
        if (items.Count <= maximum)
            return;
        var retained = items.OrderByDescending(timestamp).Take(maximum).ToList();
        items.Clear();
        items.AddRange(retained);
    }

    private async Task<GameplayLearningDocument> LoadUnsafeAsync(CancellationToken token)
    {
        if (!File.Exists(StoragePath))
            return new GameplayLearningDocument();
        try
        {
            await using var stream = File.OpenRead(StoragePath);
            return await JsonSerializer.DeserializeAsync<GameplayLearningDocument>(stream, Json, token)
                   ?? new GameplayLearningDocument();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.Error("Borin gameplay-learning state could not be read; starting with an empty safe document", exception);
            return new GameplayLearningDocument();
        }
    }

    private async Task SaveUnsafeAsync(GameplayLearningDocument document, CancellationToken token)
    {
        TrimDocument(document);
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        var temporary = StoragePath + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(stream, document, Json, token);
            File.Move(temporary, StoragePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static WorldLearningState GetOrCreateWorld(
        GameplayLearningDocument document,
        string server,
        string world,
        string character)
    {
        server = NormalizeText(server);
        world = NormalizeText(world);
        character = NormalizeText(character);
        if (server.Length == 0) server = "Unknown server";
        if (world.Length == 0) world = "Unknown world";
        if (character.Length == 0) character = "Borin";
        var key = WorldKey(server, world, character);
        var existing = FindWorldByKey(document, key);
        if (existing is not null)
            return existing;
        var profile = new WorldLearningState
        {
            Key = key,
            Server = server,
            World = world,
            Character = character,
        };
        document.Worlds[key] = profile;
        return profile;
    }

    private static WorldLearningState? FindWorld(
        GameplayLearningDocument document,
        string server,
        string world,
        string character)
    {
        server = NormalizeText(server);
        world = NormalizeText(world);
        character = NormalizeText(character);
        if (!HasNamedScope(server, world, character))
            return null;
        return document.Worlds.Values
            .Where(item => item.Server.Equals(server, StringComparison.OrdinalIgnoreCase) &&
                           item.World.Equals(world, StringComparison.OrdinalIgnoreCase) &&
                           item.Character.Equals(character, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.LastObservedAt)
            .FirstOrDefault();
    }

    private static bool HasNamedScope(string server, string world, string character)
    {
        server = NormalizeText(server);
        world = NormalizeText(world);
        character = NormalizeText(character);
        return server.Length > 0 && world.Length > 0 && character.Length > 0 &&
               !server.Equals("Unknown server", StringComparison.OrdinalIgnoreCase) &&
               !world.Equals("Unknown world", StringComparison.OrdinalIgnoreCase) &&
               !character.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static WorldLearningState? FindWorldByKey(GameplayLearningDocument document, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;
        if (document.Worlds.TryGetValue(key, out var exact))
            return exact;
        return document.Worlds.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static string WorldKey(string server, string world, string character) =>
        $"{NormalizeText(server).ToLowerInvariant()}|{NormalizeText(world).ToLowerInvariant()}|{NormalizeText(character).ToLowerInvariant()}";

    private static bool IsUnlearnableClassification(string classification) =>
        classification.Equals("Terminal", StringComparison.OrdinalIgnoreCase) ||
        classification.Equals("System", StringComparison.OrdinalIgnoreCase) ||
        classification.Equals("Speech", StringComparison.OrdinalIgnoreCase) ||
        classification.Equals("Telepath", StringComparison.OrdinalIgnoreCase) ||
        classification.Equals("Group", StringComparison.OrdinalIgnoreCase) ||
        classification.Equals("CommandSent", StringComparison.OrdinalIgnoreCase);

    private static string CanonicalDirection(string command)
    {
        var verb = CommandVerb(command);
        return verb switch
        {
            "N" or "NORTH" => "NORTH",
            "S" or "SOUTH" => "SOUTH",
            "E" or "EAST" => "EAST",
            "W" or "WEST" => "WEST",
            "U" or "UP" => "UP",
            "D" or "DOWN" => "DOWN",
            "NE" or "NORTHEAST" => "NORTHEAST",
            "NW" or "NORTHWEST" => "NORTHWEST",
            "SE" or "SOUTHEAST" => "SOUTHEAST",
            "SW" or "SOUTHWEST" => "SOUTHWEST",
            _ => string.Empty,
        };
    }

    private static bool IsLearnableCommand(string command) =>
        CanonicalDirection(command).Length > 0 || LearnableCommandVerbs.Contains(CommandVerb(command));

    private static string CommandArgument(string command, params string[] expectedVerbs)
    {
        command = NormalizeCommand(command);
        var verb = CommandVerb(command);
        if (!expectedVerbs.Any(expected => verb.Equals(expected, StringComparison.OrdinalIgnoreCase)))
            return string.Empty;
        var separator = command.IndexOf(' ');
        return separator < 0 ? string.Empty : TrimTo(command[(separator + 1)..].Trim(), 120);
    }

    private static string OutcomeKey(string command, string room) =>
        $"{NormalizeCommand(command)}|{NormalizeRoom(room)}";

    private static string CommandVerb(string command)
    {
        command = NormalizeCommand(command);
        if (command.Length == 0)
            return string.Empty;
        var separator = command.IndexOf(' ');
        return separator < 0 ? command : command[..separator];
    }

    private static string DisplayCommand(string command) =>
        NormalizeCommand(command).Length == 0 ? "an uncaptured command" : NormalizeCommand(command);

    private static string NormalizeCommand(string? value) =>
        TrimTo(NormalizeText(value).ToUpperInvariant(), 180);

    private static string NormalizeRoom(string? value)
    {
        var room = TrimTo(NormalizeText(value), 160);
        return room.Length == 0 || room.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? "an unknown room"
            : room;
    }

    private static string NormalizeText(string? value) =>
        WhitespaceRegex.Replace(value ?? string.Empty, " ").Trim();

    private static string TrimTo(string? value, int maximum)
    {
        var text = NormalizeText(value);
        return text.Length <= maximum ? text : text[..maximum].TrimEnd();
    }

    private static bool MatchesRoom(string left, string right)
    {
        left = NormalizeText(left);
        right = NormalizeText(right);
        return left.Length > 0 && right.Length > 0 && left.Equals(right, StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> ExtractTerms(string text) =>
        WordRegex.Matches(text ?? string.Empty)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(word => word.Length >= 3 && !StopWords.Contains(word))
            .Take(24)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool MatchesTerms(string text, HashSet<string> terms)
    {
        if (terms.Count == 0)
            return false;
        var haystack = NormalizeText(text);
        return terms.Any(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static int RecencyScore(DateTimeOffset timestamp)
    {
        if (timestamp == default)
            return 0;
        var age = DateTimeOffset.Now - timestamp;
        if (age <= TimeSpan.FromHours(1)) return 25;
        if (age <= TimeSpan.FromDays(1)) return 18;
        if (age <= TimeSpan.FromDays(7)) return 12;
        if (age <= TimeSpan.FromDays(30)) return 6;
        return 0;
    }

    private static string AgeLabel(DateTimeOffset timestamp)
    {
        if (timestamp == default)
            return "at an unknown time";
        var age = DateTimeOffset.Now - timestamp;
        if (age < TimeSpan.Zero) return "just now";
        if (age < TimeSpan.FromMinutes(2)) return "just now";
        if (age < TimeSpan.FromHours(1)) return $"{Math.Max(1, (int)age.TotalMinutes)} minutes ago";
        if (age < TimeSpan.FromDays(1)) return $"{Math.Max(1, (int)age.TotalHours)} hours ago";
        if (age < TimeSpan.FromDays(30)) return $"{Math.Max(1, (int)age.TotalDays)} days ago";
        return timestamp.ToString("yyyy-MM-dd");
    }

    private sealed class GameplayLearningDocument
    {
        public int SchemaVersion { get; set; } = 2;
        public string LastWorldKey { get; set; } = string.Empty;
        public DateTimeOffset LastUpdatedAt { get; set; }
        public Dictionary<string, WorldLearningState> Worlds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class WorldLearningState
    {
        public string Key { get; set; } = string.Empty;
        public string Server { get; set; } = "Unknown server";
        public string World { get; set; } = "Unknown world";
        public string Character { get; set; } = "Borin";
        public int VerifiedObservationCount { get; set; }
        public DateTimeOffset LastObservedAt { get; set; }
        public DateTimeOffset LastLearnedAt { get; set; }
        public string LastRoom { get; set; } = string.Empty;
        public string LastRoomKey { get; set; } = string.Empty;
        public int LastHealthPercent { get; set; }
        public int LastManaPercent { get; set; }
        public int LastCharacterLevel { get; set; }
        public long LastExperienceCurrent { get; set; }
        public long LastExperienceNeeded { get; set; }
        public DateTimeOffset LastExperienceObservedAt { get; set; }
        public string PendingCommand { get; set; } = string.Empty;
        public DateTimeOffset PendingCommandAt { get; set; }
        public string PendingOriginRoom { get; set; } = string.Empty;
        public string PendingOriginRoomKey { get; set; } = string.Empty;
        public DateTimeOffset RestStartedAt { get; set; }
        public int RestStartedHealth { get; set; }
        public string RestStartedRoom { get; set; } = string.Empty;
        public List<RouteObservation> Routes { get; set; } = [];
        public List<CommandOutcome> Outcomes { get; set; } = [];
        public List<DangerObservation> Dangers { get; set; } = [];
        public List<RecoveryObservation> Recoveries { get; set; } = [];
        public List<ItemObservation> Items { get; set; } = [];
        public List<MilestoneObservation> Milestones { get; set; } = [];
        public List<LevelingAreaObservation> LevelingAreas { get; set; } = [];
    }

    private sealed class LevelingAreaObservation
    {
        public string Room { get; set; } = string.Empty;
        public int Observations { get; set; }
        public long TotalObservedExperienceIncrease { get; set; }
        public long MaximumObservedExperienceIncrease { get; set; }
        public int MinimumCharacterLevel { get; set; }
        public int MaximumCharacterLevel { get; set; }
        public DateTimeOffset LastObservedAt { get; set; }
    }

    private sealed class RouteObservation
    {
        public string FromRoom { get; set; } = string.Empty;
        public string FromRoomKey { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string ToRoom { get; set; } = string.Empty;
        public string ToRoomKey { get; set; } = string.Empty;
        public int Observations { get; set; }
        public DateTimeOffset LastObservedAt { get; set; }
    }

    private sealed class CommandOutcome
    {
        public string Category { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public int Successes { get; set; }
        public int Failures { get; set; }
        public int EvidenceCount { get; set; }
        public string Detail { get; set; } = string.Empty;
        public DateTimeOffset LastObservedAt { get; set; }
    }

    private sealed class DangerObservation
    {
        public string Room { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public int EvidenceCount { get; set; }
        public int MaximumHealthDrop { get; set; }
        public int Victories { get; set; }
        public int Defeats { get; set; }
        public DateTimeOffset LastObservedAt { get; set; }
    }

    private sealed class RecoveryObservation
    {
        public string Room { get; set; } = string.Empty;
        public int Observations { get; set; }
        public int MaximumHealthGain { get; set; }
        public DateTimeOffset LastObservedAt { get; set; }
    }

    private sealed class ItemObservation
    {
        public string Item { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public int Observations { get; set; }
        public DateTimeOffset LastObservedAt { get; set; }
    }

    private sealed class MilestoneObservation
    {
        public string Key { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public int Observations { get; set; }
        public DateTimeOffset LastObservedAt { get; set; }
    }
}
