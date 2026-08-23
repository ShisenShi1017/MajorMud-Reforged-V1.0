using System;
using System.Collections.Generic;
using System.Linq;

namespace Reforged.App.Brains;

public enum BorinRequestIntent
{
    Conversation,
    RepositoryResearch,
    Engineering,
    ActivePlayer,
}

public enum BorinKnowledgeSubsystem
{
    General,
    Architecture,
    RuntimeTrace,
    Commands,
    Rooms,
    Shops,
    Equipment,
    SearchCleanup,
    Combat,
    Npcs,
    QuestsScriptsTriggers,
    Persistence,
    ActivePlayer,
    Packaging,
}

/// <summary>
/// Keeps Borin's local repository capabilities separate from his external-player session.
/// The router is intentionally deterministic: player connection state is supplied only when
/// the user is actually asking about Borin's live character/session. It also selects one
/// compact Reforged subsystem card before broader retrieval begins.
/// </summary>
public static class BorinIntentRouter
{
    private static readonly string[] RepositoryTerms =
    [
        "audit", "repo", "repository", "source", "codebase", "librarium", "decoded",
        "generated data", "data/classic", "runtime", "consumer", "runtime disconnect", "disconnected data", "disconnect list", "parity",
        "validator", "validation report", "build report", "application log", "server log",
        "local server", "host and server", "host runtime", "scan local", "scan the local",
        "trace", "catalog", "projection", "ground objects", "mapped fields",
    ];

    private static readonly string[] EngineeringTerms =
    [
        "fix", "patch", "change code", "edit code", "compile", "build the app", "run tests",
        "create update zip", "package update", "show diff", "approve draft", "workspace",
    ];

    private static readonly string[] ActivePlayerTerms =
    [
        "connect", "login", "log in", "password", "omniverse", "major mud session",
        "majormud session", "player session", "active player", "terminal", "telepath",
        "say to", "talk to player", "talk to gryndak", "conversation with", "current room",
        "what room", "where are you", "current exits", "what are the exits", "visible exits",
        "hp prompt", "see the prompt", "current hp", "current mana", "which server", "where are you playing",
        "move north", "move south", "move east", "move west",
        "buy club", "equip club", "inventory", "equipment", "game prompt", "disconnect from",
    ];

    private static readonly string[] ParityTerms =
    [
        "parity", "decoded", "librarium", "generated", "runtime", "consumer", "persistence",
        "reset", "cleanup", "validator", "validation", "runtime disconnect", "disconnected data", "disconnect list", "classic", "ground objects",
    ];

    public static BorinRequestIntent Classify(string? request)
    {
        var normalized = Normalize(request);
        if (normalized.Length == 0)
            return BorinRequestIntent.Conversation;

        // Explicit local/repository wording wins over generic words such as "server" or
        // "connect", because a local server audit must never be treated as player login work.
        if (ContainsAny(normalized, RepositoryTerms))
            return BorinRequestIntent.RepositoryResearch;
        if (ContainsAny(normalized, EngineeringTerms))
            return BorinRequestIntent.Engineering;
        if (ContainsAny(normalized, ActivePlayerTerms))
            return BorinRequestIntent.ActivePlayer;
        return BorinRequestIntent.Conversation;
    }

    public static BorinKnowledgeSubsystem ClassifySubsystem(string? request)
    {
        var normalized = Normalize(request);
        if (normalized.Length == 0)
            return BorinKnowledgeSubsystem.General;

        if (ContainsAny(normalized, ["zip", "package", "installer", "publish", "rollback", "top folder", "manifest", "checksum"]))
            return BorinKnowledgeSubsystem.Packaging;
        if (ContainsAny(normalized, ["login", "omniverse", "telepath", "gryndak", "active player", "player session", "game prompt", "terminal"]))
            return BorinKnowledgeSubsystem.ActivePlayer;
        if (ContainsAny(normalized, ["quest", "script", "trigger", "event chain"]))
            return BorinKnowledgeSubsystem.QuestsScriptsTriggers;
        if (ContainsAny(normalized, ["search", "hidden", "conceal", "ground item", "cleanup", "del@cleanup"]))
            return BorinKnowledgeSubsystem.SearchCleanup;
        if (ContainsAny(normalized, ["persist", "persistence", "save", "reset", "restart", "gang house", "upkeep"]))
            return BorinKnowledgeSubsystem.Persistence;
        if (ContainsAny(normalized, ["equip", "wield", "weapon slot", "armor slot", "equipment"]))
            return BorinKnowledgeSubsystem.Equipment;
        if (ContainsAny(normalized, ["shop", "merchant", "buy", "sell", "price", "inventory list"]))
            return BorinKnowledgeSubsystem.Shops;
        if (ContainsAny(normalized, ["npc", "monster", "guarded", "faction", "reputation", "hostile npc"]))
            return BorinKnowledgeSubsystem.Npcs;
        if (ContainsAny(normalized, ["combat", "attack", "damage", "critical", "corpse", "loot drop"]))
            return BorinKnowledgeSubsystem.Combat;
        if (ContainsAny(normalized, ["room", "exit", "door", "trap", "movement", "map", "pathing"]))
            return BorinKnowledgeSubsystem.Rooms;
        if (ContainsAny(normalized, ["command", "abbreviation", "brief", "verbose", "say", "speech parser"]))
            return BorinKnowledgeSubsystem.Commands;
        if (IsParityResearch(normalized) || ContainsAny(normalized, ["trace", "catalog", "consumer", "projection", "disconnect chain"]))
            return BorinKnowledgeSubsystem.RuntimeTrace;
        if (ContainsAny(normalized, ["architecture", "project structure", "system design", "module", "service ownership"]))
            return BorinKnowledgeSubsystem.Architecture;
        return BorinKnowledgeSubsystem.General;
    }

    public static bool IsParityResearch(string? request) =>
        ContainsAny(Normalize(request), ParityTerms);

    public static string GetPurpose(BorinRequestIntent intent) => intent switch
    {
        BorinRequestIntent.RepositoryResearch => "Local repository research",
        BorinRequestIntent.Engineering => "Local engineering discussion",
        BorinRequestIntent.ActivePlayer => "Active player conversation",
        _ => "Conversation",
    };

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static bool ContainsAny(string value, IEnumerable<string> terms) =>
        terms.Any(term => value.Contains(term, StringComparison.Ordinal));
}
