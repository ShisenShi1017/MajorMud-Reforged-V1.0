using System.Security.Cryptography;
using System.Text;
using RealmGate.App.Forge;

namespace RealmGate.App.Brains;

[Flags]
public enum BorinRole
{
    None = 0,
    Player = 1,
    Sysop = 2,
    Developer = 4
}

public sealed record BorinProviderContext(
    string Purpose,
    BorinRole Role,
    ForgePermissionMode PermissionMode,
    string StateFingerprint,
    string SystemMessage,
    string UserMessage,
    IReadOnlyList<string> AvailableCapabilities,
    int IncludedDurableMemories,
    int IncludedEpisodes);

public sealed class BorinContextBuilderOptions
{
    public int MaximumContextCharacters { get; init; } = 20_000;
    public int MaximumDurableMemories { get; init; } = 8;
    public int MaximumEpisodes { get; init; } = 4;
    public int MaximumInventoryItems { get; init; } = 12;
}

public interface IBorinContextBuilder
{
    BorinProviderContext Build(BrainContext context);
}

/// <summary>
/// Converts Borin's already-retrieved local context into a compact provider package.
/// It creates no facts and grants no authority. Live verified state is kept separate
/// from memory so a provider cannot mistake remembered state for current perception.
/// </summary>
public sealed class BorinContextBuilder(
    BorinContextBuilderOptions? options = null,
    IBorinCapabilityCatalog? capabilityCatalog = null) : IBorinContextBuilder
{
    private readonly BorinContextBuilderOptions _options = options ?? new BorinContextBuilderOptions();
    private readonly IBorinCapabilityCatalog _capabilityCatalog = capabilityCatalog ?? new BorinCapabilityCatalog();

    public BorinProviderContext Build(BrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var profile = context.Conversation.State.Configuration;
        var game = context.Conversation.GameState;
        var role = ResolveRole(context.Purpose);
        var capabilities = _capabilityCatalog.Resolve(role, profile)
            .Where(item => item.Available)
            .Select(item => item.Capability.ToolId)
            .ToArray();
        var memories = context.Conversation.Memory.RelevantMemories
            .Take(Math.Clamp(_options.MaximumDurableMemories, 0, 24))
            .Select(memory => $"[{memory.Category}/{memory.Evidence}] {Limit(memory.Text, 500)}")
            .ToArray();
        var episodes = context.Conversation.EpisodicMemory.RelevantEpisodes
            .Take(Math.Clamp(_options.MaximumEpisodes, 0, 12))
            .Select(episode => $"[{Limit(episode.Title, 100)}] {Limit(episode.Summary, 500)} (source: {Limit(episode.Attribution, 120)})")
            .ToArray();

        var system = AiSecretRedactor.Redact($"""
You are {profile.CharacterName}, {profile.CharacterTitle}, serving in role(s): {role}.
Identity and personality remain local to Borin. Preserve: {profile.SpeechStyle} {profile.Temperament}
Values: {profile.Values}
Mission: {profile.Mission}

RealmGate and Borin's local state are authoritative. Live verified state overrides memory. Missing evidence remains unknown.
For MajorMUD/RealmGate factual claims, search and use the supplied local knowledge first, preserve its confidence, and cite its source location. NVIDIA pretrained knowledge is not authoritative project evidence. Never promote inference to PROVEN.
Keep three connection domains separate: AI provider connectivity, the RealmGate Forge/application, and the MajorMUD terminal/game session. Never infer one domain's state from another.
Memories and ledger entries describe past evidence. Do not present a past error as current unless the verified current state repeats it.
Return reasoning or a proposal only. Do not claim that a command, file edit, build, test, login, or world change occurred unless verified evidence below says it occurred.
Your current permission is {profile.PermissionMode}. Available capabilities are descriptive constraints, not direct tool access: {string.Join(", ", capabilities)}.
The structured speech field is visible dialogue only. Never mention JSON, schema fields, intent names, confidence, state fingerprints, or tool-routing metadata in speech.
For an engineering patch proposal, place the complete fenced unified diff in target and keep speech to a concise Borin summary.
""");

        var inventory = game.HasVerifiedLiveState
            ? FormatList(game.InventoryItems.Take(Math.Clamp(_options.MaximumInventoryItems, 0, 40)), "none observed")
            : "not supplied because live state is unverified";
        var equipment = game.HasVerifiedLiveState
            ? FormatList(game.EquippedItems.Take(Math.Clamp(_options.MaximumInventoryItems, 0, 40)), "none observed")
            : "not supplied because live state is unverified";
        var rules = context.Conversation.State.ApprovedProjectRules.Take(12).Select(rule => Limit(rule, 300));
        var repository = context.Conversation.RepositorySnapshot is { } repositorySnapshot
            ? $"supplied; projects={repositorySnapshot.ProjectCount}; sourceFiles={repositorySnapshot.SourceFileCount}; testProjects={repositorySnapshot.TestProjectCount}; solutions={FormatList(repositorySnapshot.Solutions.Take(6), "none")}" 
            : "not supplied";

        var user = AiSecretRedactor.Redact($"""
OBJECTIVE
Purpose: {Limit(context.Purpose, 300)}
Request: {Limit(context.Request, 4000)}
Relevant objective: {Limit(context.Conversation.SituationalAwareness.RelevantObjective, 600)}

VERIFIED LIVE STATE
Captured at: {game.CapturedAt:O}
Connection: connected={game.IsConnected}; state={game.ConnectionState}
Authentication: login={game.LoginState}; loggedIntoMajorMud={game.IsLoggedIntoMajorMud}
Terminal/game phase: {game.Phase}; verifiedLiveState={game.HasVerifiedLiveState}; promptVerified={game.HasVerifiedPrompt} ({game.PromptFreshness})
Environment: {Limit(game.EnvironmentSummary, 500)}
Server/world/character: {Limit(game.GameServerIdentity, 300)} / {Limit(game.World, 120)} / {Limit(game.CharacterName, 120)}
Room: verified={game.HasVerifiedRoom}; key={Limit(game.CurrentRoomKey, 160)}; title={Limit(game.CurrentRoom, 300)}; freshness={game.RoomFreshness}
Exits: verified={game.HasVerifiedExits}; values={FormatList(game.CurrentExits.Take(20), "none verified")}; freshness={game.ExitFreshness}
Vitals: HP={game.HealthCurrent}/{game.HealthMaximum} ({game.HealthPercent}%); MA={game.ManaCurrent}/{game.ManaMaximum} ({game.ManaPercent}%)
Activity: combat={game.InCombat}; autocombat={game.AutocombatActive}; resting={game.IsResting}; moving={game.IsMoving}
Last command: {game.LastCommandOrigin}; {Limit(game.LastCommand, 300)}; freshness={game.LastCommandFreshness}
Inventory: {inventory}
Equipment: {equipment}

LOCALLY RETRIEVED MEMORY (not live state)
Durable facts ({memories.Length}):
{FormatLines(memories, "- No relevant durable memory was selected.")}
Episodes ({episodes.Length}):
{FormatLines(episodes, "- No relevant episode was selected.")}

REPOSITORY EVIDENCE
Repository snapshot: {repository}
Exact read-only evidence:
{Limit(context.Conversation.Evidence.PromptText, 3000)}
Ranked knowledge evidence:
{Limit(context.Conversation.Knowledge.PromptText, 2200)}
Compact architecture/bootstrap evidence:
{Limit(context.Conversation.Bootstrap.PromptText, 1400)}
Classic provenance trace:
{Limit(context.Conversation.ClassicTrace.PromptText, 1600)}
Approved project rules:
{FormatLines(rules, "- No additional project rules were supplied.")}

RESPONSE BOUNDARY
Use only the evidence above. Do not produce or execute arbitrary shell or terminal commands. Local Borin will parse, validate, permission-check, and execute any later structured proposal.
""");

        var budget = Math.Clamp(_options.MaximumContextCharacters, 2_000, 50_000);
        var reservedSystem = Math.Min(system.Length, budget / 2);
        system = Limit(system, reservedSystem);
        user = Limit(user, Math.Max(1_000, budget - system.Length));
        var fingerprint = Fingerprint(game);

        return new BorinProviderContext(
            context.Purpose,
            role,
            profile.PermissionMode,
            fingerprint,
            system,
            user,
            capabilities,
            memories.Length,
            episodes.Length);
    }

    private static BorinRole ResolveRole(string purpose)
    {
        var value = purpose ?? string.Empty;
        var role = BorinRole.None;
        if (value.Contains("player", StringComparison.OrdinalIgnoreCase) || value.Contains("MajorMUD", StringComparison.OrdinalIgnoreCase)) role |= BorinRole.Player;
        if (value.Contains("sysop", StringComparison.OrdinalIgnoreCase) || value.Contains("world", StringComparison.OrdinalIgnoreCase)) role |= BorinRole.Sysop;
        if (value.Contains("develop", StringComparison.OrdinalIgnoreCase) || value.Contains("engineer", StringComparison.OrdinalIgnoreCase) || value.Contains("repository", StringComparison.OrdinalIgnoreCase) || value.Contains("code", StringComparison.OrdinalIgnoreCase)) role |= BorinRole.Developer;
        return role == BorinRole.None ? BorinRole.Developer : role;
    }

    private static string Fingerprint(BorinGameStateSnapshot game)
    {
        var canonical = string.Join('|', game.CapturedAt.ToUniversalTime().Ticks, game.ConnectionState, game.LoginState, game.Phase,
            game.World, game.CharacterName, game.CurrentRoomKey, game.CurrentRoom, string.Join(',', game.CurrentExits),
            game.HealthCurrent, game.HealthMaximum, game.ManaCurrent, game.ManaMaximum, game.InCombat, game.LastCommand, game.LastCommandOrigin);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string FormatList(IEnumerable<string> values, string empty)
    {
        var selected = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => Limit(value, 240)).ToArray();
        return selected.Length == 0 ? empty : string.Join(", ", selected);
    }

    private static string FormatLines(IEnumerable<string> values, string empty)
    {
        var selected = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => "- " + value).ToArray();
        return selected.Length == 0 ? empty : string.Join('\n', selected);
    }

    private static string Limit(string? value, int maximum)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maximum ? normalized : normalized[..maximum] + " [truncated]";
    }
}
