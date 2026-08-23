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
    RuntimeDiagnostics,
    Help,
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
            "look", "brief", "verbose", "search", "examine");
        Add(routes, RuntimeCommandRouteKind.CharacterState, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade character/item-state renderers",
            "score", "character", "wealth", "experience", "inventory", "equipment", "party", "whohome", "librarium", "roomdata", "clientdata", "save", "rest", "meditate");
        Add(routes, RuntimeCommandRouteKind.ItemRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade.ExecuteItemCommand",
            "get", "drop", "put", "give", "equip", "wear", "wield", "remove");
        Add(routes, RuntimeCommandRouteKind.CombatRuntime, "RuntimeLiveGameplayFacade.ExecutePolicyCommand", "RuntimeLiveGameplayFacade combat/stealth runtime",
            "attack", "kill", "flee", "combat", "round", "assist", "bash", "smash", "backstab", "punch", "kick", "jumpkick", "hide", "sneak");
        Add(routes, RuntimeCommandRouteKind.PowerRuntime, "RuntimeLiveGameplayFacade.ExecutePolicyCommand", "RuntimeLiveGameplayFacade.ExecuteLivePowerCommand",
            "cast", "prepare", "spells", "use", "dispel", "abilities", "skills");
        Add(routes, RuntimeCommandRouteKind.NarrativeRuntime, "RuntimeLiveGameplayFacade.ExecutePolicyCommand", "RuntimeLiveGameplayFacade.ExecuteWorldNarrativeCommand",
            "ask", "talk", "quest", "accept", "abandon", "complete", "journal");
        Add(routes, RuntimeCommandRouteKind.ServiceRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade.ExecuteLiveServicesBundleCommand",
            "buy", "sell", "repair", "train", "tat", "services", "list", "trade");
        Add(routes, RuntimeCommandRouteKind.BankRuntime, "RuntimeLiveGameplayFacade.ExecutePolicyCommand", "RuntimeLiveGameplayFacade bank runtime",
            "bank", "deposit", "withdraw");
        Add(routes, RuntimeCommandRouteKind.LivingWorldRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade.ExecuteLivingWorldCommand",
            "open", "close", "lock", "unlock", "pull", "push", "activate");
        Add(routes, RuntimeCommandRouteKind.CommunicationRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade communication/session runtime",
            "say", "tell", "messages", "who", "create", "invite", "uninvite", "follow", "unfollow", "frontrank", "midrank", "backrank", "join", "leave", "broadgang", "disband", "promote", "demote", "topten");
        Add(routes, RuntimeCommandRouteKind.MajorMudPlusRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade.ExecuteMajorMudPlusCommand",
            "mudplus", "description", "mudreg", "buylife", "plusadmin");
        Add(routes, RuntimeCommandRouteKind.SysopRuntime, "RuntimeCommandDispatcher", "RuntimeLiveGameplayFacade.ExecuteWccSysopCommand",
            "sysop");
        Add(routes, RuntimeCommandRouteKind.RecoveryRuntime, "RuntimeLiveGameplayFacade.ExecutePolicyCommand", "RuntimeLiveGameplayFacade character recovery runtime",
            "respawn", "recover");
        Add(routes, RuntimeCommandRouteKind.RuntimeDiagnostics, "RuntimeCommandDispatcher", "Runtime host/live-world diagnostic renderers",
            "worldstate", "ready", "audit", "where", "world", "worlds", "sessions", "state", "events");
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
