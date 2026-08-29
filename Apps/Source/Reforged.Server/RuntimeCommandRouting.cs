namespace Reforged.Server;

internal enum RuntimeCommandRouteKind
{
    Movement,
    RoomPresentation,
    CharacterState,
    ItemRuntime,
    CombatRuntime,
    PowerRuntime,
    NarrativeRuntime,
    ServiceRuntime,
    BankRuntime,
    LivingWorldRuntime,
    CommunicationRuntime,
    MajorMudPlusRuntime,
    SysopRuntime,
    RecoveryRuntime,
    NativeEvidenceGate,
    RuntimeDiagnostics,
    Help,
    SessionLifecycle,
    SessionExit
}

internal sealed record RuntimeCommandRouteDefinition(
    string Canonical,
    RuntimeCommandRouteKind Kind,
    string RuntimeOwner,
    string LiveConsumer);

internal static class RuntimeCommandRuntimeRouteCatalog
{
    private static readonly IReadOnlyDictionary<string, RuntimeCommandRouteDefinition> Routes = Build();

    public static IReadOnlyCollection<RuntimeCommandRouteDefinition> Definitions => Routes.Values.ToArray();

    public static bool TryGet(string canonical, out RuntimeCommandRouteDefinition definition)
    {
        if (Routes.TryGetValue(canonical, out var resolved))
        {
            definition = resolved;
            return true;
        }

        definition = null!;
        return false;
    }

    public static RuntimeCommandRouteDefinition Require(string canonical) =>
        Routes.TryGetValue(canonical, out var definition)
            ? definition
            : throw new InvalidOperationException($"Registered command '{canonical}' has no runtime route.");

    private static IReadOnlyDictionary<string, RuntimeCommandRouteDefinition> Build()
    {
        var routes = new Dictionary<string, RuntimeCommandRouteDefinition>(StringComparer.OrdinalIgnoreCase);

        Add(routes, RuntimeCommandRouteKind.Movement, "RuntimeCommandDispatcher", "RuntimeSession.TryMove",
            "north", "south", "east", "west", "northeast", "northwest", "southeast", "southwest", "up", "down");
        Add(routes, RuntimeCommandRouteKind.RoomPresentation, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade room/search renderers",
            "look", "brief", "verbose", "statline", "set", "search", "examine", "exits");
        Add(routes, RuntimeCommandRouteKind.CharacterState, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade character/item-state renderers",
            "score", "health", "character", "wealth", "experience", "inventory", "keys", "equipment", "party", "profile", "scan", "whohome", "librarium", "roomdata", "clientdata", "save", "rest", "meditate");
        Add(routes, RuntimeCommandRouteKind.ItemRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade.ExecuteItemCommand",
            "get", "drop", "put", "give", "equip", "wear", "wield", "ready", "remove");
        Add(routes, RuntimeCommandRouteKind.CombatRuntime, "RuntimeLiveGameplayFacade.ExecutePolicyCommand", "RuntimeLiveGameplayFacade combat/stealth runtime",
            "attack", "kill", "flee", "combat", "round", "assist", "break", "bash", "smash", "backstab", "punch", "kick", "jumpkick", "hide", "sneak");
        Add(routes, RuntimeCommandRouteKind.PowerRuntime, "RuntimeLiveGameplayFacade.ExecutePolicyCommand", "RuntimeLiveGameplayFacade.ExecuteLivePowerCommand",
            "cast", "invoke", "prepare", "spells", "powers", "use", "dispel", "abilities", "skills");
        Add(routes, RuntimeCommandRouteKind.NarrativeRuntime, "RuntimeLiveGameplayFacade.ExecutePolicyCommand", "RuntimeLiveGameplayFacade.ExecuteWorldNarrativeCommand",
            "ask", "greet", "quest", "accept", "abandon", "complete", "journal");
        Add(routes, RuntimeCommandRouteKind.ServiceRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade.ExecuteLiveServicesBundleCommand",
            "buy", "sell", "repair", "train", "tat", "services", "list", "trade");
        Add(routes, RuntimeCommandRouteKind.BankRuntime, "RuntimeLiveGameplayFacade.ExecutePolicyCommand", "RuntimeLiveGameplayFacade bank runtime",
            "bank", "deposit", "withdraw");
        Add(routes, RuntimeCommandRouteKind.LivingWorldRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade.ExecuteLivingWorldCommand",
            "open", "close", "lock", "unlock", "pull", "push", "activate", "picklock", "disarm");
        Add(routes, RuntimeCommandRouteKind.CommunicationRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade communication/session runtime",
            "say", "talk", "gossip", "auction", "broadcast", "forget", "messages", "who", "create", "invite", "uninvite", "follow", "frontrank", "midrank", "backrank", "join", "leave", "share", "broadgang", "disband", "promote", "demote", "topten");
        Add(routes, RuntimeCommandRouteKind.MajorMudPlusRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade.ExecuteMajorMudPlusCommand",
            "mudplus", "description", "mudreg", "buylife", "plusadmin");
        Add(routes, RuntimeCommandRouteKind.SysopRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade.ExecuteWccSysopCommand",
            "sysop");
        Add(routes, RuntimeCommandRouteKind.RecoveryRuntime, "RuntimeLiveGameplayFacade.ExecutePolicyCommand", "RuntimeLiveGameplayFacade character recovery runtime",
            "respawn", "recover");
        Add(routes, RuntimeCommandRouteKind.SessionLifecycle, "ServerNetworkHost", "Interactive character replacement lifecycle",
            "reroll");
        Add(routes, RuntimeCommandRouteKind.NativeEvidenceGate, "RuntimeCommandDispatcher", "Evidence-gated native command identity; execute decoded contextual action when available",
            "drag", "go", "map", "track", "aid", "light", "stash", "mana", "read", "suicide",
            "appraise", "eat", "drink", "rob", "page", "quiet", "stock", "unstock", "markup");
        Add(routes, RuntimeCommandRouteKind.RuntimeDiagnostics, "RuntimeCommandDispatcher", "Runtime host/live-world diagnostic renderers",
            "worldstate", "audit", "where", "world", "worlds", "sessions", "state", "events");
        Add(routes, RuntimeCommandRouteKind.Help, "RuntimeCommandDispatcher", "Runtime command help catalog",
            "help", "commands");
        Add(routes, RuntimeCommandRouteKind.SessionExit, "ServerNetworkHost/ServerRuntimeHost", "RuntimeCommandDispatcher.RequestsSessionExit",
            "quit");

        return routes;
    }

    private static void Add(
        IDictionary<string, RuntimeCommandRouteDefinition> routes,
        RuntimeCommandRouteKind kind,
        string runtimeOwner,
        string liveConsumer,
        params string[] commands)
    {
        foreach (var command in commands)
        {
            if (!routes.TryAdd(command, new RuntimeCommandRouteDefinition(command, kind, runtimeOwner, liveConsumer)))
                throw new InvalidOperationException($"Duplicate runtime command route '{command}'.");
        }
    }
}
