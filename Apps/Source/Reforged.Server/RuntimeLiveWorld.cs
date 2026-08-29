namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private const string ClassicStatlineModeKey = "ui.classic.statline.mode";
    private const string ClassicReceiveEnabledKey = "ui.classic.receive.enabled";
    private const string ClassicPartyFollowModeKey = "ui.classic.party.follow.mode";
    private const string ClassicMessagesLevelKey = "ui.classic.messages.level";
    private const string ClassicEntrancesEnabledKey = "ui.classic.entrances.enabled";
    private const string ClassicMaxPagesKey = "communication.maxpages";
    private const string ClassicMaxGossipKey = "communication.maxgossip";
    private const string ClassicMaxAuctionKey = "communication.maxauction";
    private const string ClassicStatlineTemplateKey = "ui.classic.statline.template";
    private const string ClassicInterruptEnabledKey = "ui.classic.interrupt.enabled";
    private const string ClassicPaletteKey = "ui.classic.palette";
    private readonly object _classicCommunicationCounterGate = new();
    private readonly Dictionary<string, (long Window, byte Count)> _classicCommunicationCounters = new(StringComparer.OrdinalIgnoreCase);

    public string RenderRoom(RuntimeSession session) => RenderRoomInternal(session, forceDescription: false, forceBrief: false);

    public string RenderRoomVerbose(RuntimeSession session) => RenderRoomInternal(session, forceDescription: true, forceBrief: false);

    public string RenderObviousExits(RuntimeSession session)
    {
        const string reset = "\u001b[0m";
        const string brightGreen = "\u001b[1;32m";
        if (session.Actor is RuntimeCharacterActor visionActor
            && _characters.LoadCharacter(visionActor.Id) is { } visionCharacter
            && RuntimeClassicStatusProjection.IsVisionBlocked(visionCharacter))
            return RuntimeClassicCombatAnsi.Warning("You are blind.");

        var visibleExits = session.CurrentRoom.Exits.Where(RuntimeMajorMudCompatibility.IsObviousExit).ToArray();
        var exits = visibleExits.Length == 0
            ? "NONE!!!"
            : string.Join(", ", visibleExits.Select(exit =>
                RuntimeMajorMudCompatibility.FormatObviousExit(exit, DescribeLivingExit(session, exit.Direction))));
        return $"{brightGreen}Obvious exits: {exits}{reset}";
    }

    public string RenderBlankCommand(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return string.Empty;
        var character = _characters.LoadCharacter(actor.Id);
        if (character is null) return string.Empty;
        var mode = GetClassicStatlineMode(character);
        return mode is "full" or "full_custom"
            ? RenderRoomInternal(session, forceDescription: false, forceBrief: true)
            : string.Empty;
    }

    public string SetRoomDescriptionMode(RuntimeSession session, bool brief)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            ["ui.room_description_mode"] = brief ? "brief" : "verbose"
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow });
        return brief
            ? "Brief mode enabled. Room descriptions will be omitted during normal movement; LOOK still shows the full description."
            : "Verbose mode enabled. Full room descriptions will be shown during normal movement.";
    }

    private bool UsesBriefRoomDescriptions(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return false;
        var character = _characters.LoadCharacter(actor.Id);
        return character?.Conditions.TryGetValue("ui.room_description_mode", out var mode) == true &&
               mode.Equals("brief", StringComparison.OrdinalIgnoreCase);
    }

    private string RenderRoomInternal(RuntimeSession session, bool forceDescription, bool forceBrief)
    {
        const string reset = "\u001b[0m";
        const string brightCyan = "\u001b[1;36m";
        const string brightGreen = "\u001b[1;32m";
        const string magenta = "\u001b[1;35m";
        const string white = "\u001b[0;37m";

        RuntimeBundleDSearchRuntime.EnsureLoaded(_characters.ProjectRoot, session.World.Definition.Id);
        EnsureClassicArenaPopulation(session);

        var living = LivingNpcsInRoom(session).ToArray();
        if (session.Actor is RuntimeCharacterActor visionActor
            && _characters.LoadCharacter(visionActor.Id) is { } visionCharacter
            && RuntimeClassicStatusProjection.IsVisionBlocked(visionCharacter))
        {
            if (!_liveCombats.ContainsKey(session.Id))
            {
                var aggressive = living.FirstOrDefault(npc => ResolveLiveNpcDisposition(npc).Equals("hostile", StringComparison.OrdinalIgnoreCase)
                    && !IsNpcAttackSuppressedByItem(npc, visionCharacter));
                if (aggressive is not null)
                {
                    try { _ = BeginLiveCombat(session, aggressive.InstanceId, ClassicAttackType.NormalWeapon); } catch { }
                }
            }
            return RuntimeClassicCombatAnsi.Warning("You are blind.");
        }

        var lines = new List<string>
        {
            $"{brightCyan}{session.CurrentRoom.Name}{reset}"
        };
        if (!forceBrief && (forceDescription || !UsesBriefRoomDescriptions(session)))
            lines.Add($"{white}{IndentClassicDescription(session.CurrentRoom.Description)}{reset}");

        var roomObserver = session.Actor is RuntimeCharacterActor roomObserverActor
            ? _characters.LoadCharacter(roomObserverActor.Id)
            : null;
        var roomSessions = _sessions.InRoom(session.World.Definition.Id, session.CurrentRoom.Id)
            .Where(other => !other.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase))
            .Where(other => other.Actor is not RuntimeCharacterActor targetActor || RuntimeClassicCharacterAppearance.CanView(
                new RuntimeClassicAppearanceViewer(ConditionIsTrue(roomObserver, "see_hidden"), ConditionIsTrue(roomObserver, "detect_invisible"), false),
                Appearance(_characters.LoadCharacter(targetActor.Id), targetActor)))
            .Where(other => !forceBrief || other.Actor is not RuntimeCharacterActor targetActor
                || !ConditionIsTrue(_characters.LoadCharacter(targetActor.Id), "sneaking"))
            .ToArray();

        // Every world inherits the native MajorMUD room stream. A trailing * marks
        // Fiend reputation only; duplicate NPC names remain distinct occupants.
        var nativePlayers = roomSessions.Select(other =>
        {
            var record = other.Actor is RuntimeCharacterActor characterActor
                ? _characters.LoadCharacter(characterActor.Id)
                : null;
            var isFiend = record?.SemanticState?.Alignment?.Reputation.Equals(
                "Fiend", StringComparison.OrdinalIgnoreCase) == true;
            return (other.Actor.DisplayName, isFiend);
        });
        var occupants = RuntimeMajorMudCompatibility.FormatRoomOccupants(
            nativePlayers, living.Select(npc => npc.Name));

        var roomCurrency = RuntimeClassicRoomCurrencyStore.Get(_characters.ProjectRoot, session.World.Definition.Id, session.CurrentRoom.Id);
        var ground = LoadVisibleGround(session).ToArray();
        var noticed = new List<string>();
        if (!roomCurrency.IsEmpty) noticed.Add(RuntimeClassicRoomCurrencyStore.Format(roomCurrency));
        noticed.AddRange(ground.Select(entry => entry.Name));
        if (noticed.Count > 0) lines.Add($"{brightGreen}You notice {string.Join(", ", noticed)} here.{reset}");
        if (!string.IsNullOrWhiteSpace(occupants)) lines.Add($"{magenta}{occupants}{reset}");

        var visibleExits = session.CurrentRoom.Exits.Where(RuntimeMajorMudCompatibility.IsObviousExit).ToArray();
        var exits = visibleExits.Length == 0
            ? "NONE!!!"
            : string.Join(", ", visibleExits.Select(exit =>
                RuntimeMajorMudCompatibility.FormatObviousExit(exit, DescribeLivingExit(session, exit.Direction))));
        lines.Add($"{brightGreen}Obvious exits: {exits}{reset}");
        if (!_liveCombats.ContainsKey(session.Id))
        {
            var targetCharacter = session.Actor is RuntimeCharacterActor targetActor ? _characters.LoadCharacter(targetActor.Id) : null;
            var aggressive = living.FirstOrDefault(npc => ResolveLiveNpcDisposition(npc).Equals("hostile", StringComparison.OrdinalIgnoreCase)
                && !IsNpcAttackSuppressedByItem(npc, targetCharacter));
            if (aggressive is not null)
            {
                try { lines.Add(BeginLiveCombat(session, aggressive.InstanceId, ClassicAttackType.NormalWeapon).Replace("You attack", aggressive.Name + " attacks")); } catch { }
            }
        }
        return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }


    public string LookDirection(RuntimeSession session, string query)
    {
        if (session.Actor is RuntimeCharacterActor visionActor
            && _characters.LoadCharacter(visionActor.Id) is { } visionCharacter
            && RuntimeClassicStatusProjection.IsVisionBlocked(visionCharacter))
            return RuntimeClassicCombatAnsi.Warning("You are blind.");
        var raw = query.Trim();
        var normalized = raw.ToUpperInvariant() switch
        {
            "NORTH" => "N", "SOUTH" => "S", "EAST" => "E", "WEST" => "W",
            "NORTHEAST" => "NE", "NORTHWEST" => "NW", "SOUTHEAST" => "SE", "SOUTHWEST" => "SW",
            "UP" => "U", "DOWN" => "D", "N" => "N", "S" => "S", "E" => "E", "W" => "W",
            "NE" => "NE", "NW" => "NW", "SE" => "SE", "SW" => "SW", "U" => "U", "D" => "D",
            _ => string.Empty
        };
        if (normalized.Length > 0 && VisiblePlayerMatches(session, raw, includeSelf: false).Length > 0) return Examine(session, raw);
        if (normalized.Length == 0) return Examine(session, raw);
        var exit = session.World.FindExit(session.CurrentRoom, normalized);
        if (exit is null || !CanCharacterSeeExit(session, exit)) return "You see nothing in that direction.";
        var target = session.World.ResolveExitDestination(exit);
        if (target is null) return "You cannot see where that exit leads.";
        const string reset = "\u001b[0m", brightCyan = "\u001b[1;36m", white = "\u001b[0;37m", brightGreen = "\u001b[1;32m";
        var visibleExits = target.Exits.Where(RuntimeMajorMudCompatibility.IsObviousExit).ToArray();
        var exits = visibleExits.Length == 0
            ? "NONE!!!"
            : string.Join(", ", visibleExits.Select(candidate =>
                RuntimeMajorMudCompatibility.FormatObviousExit(candidate, Living(session).DescribeExit(target, candidate.Direction))));
        return string.Join(Environment.NewLine, new[] { $"{brightCyan}{target.Name}{reset}", $"{white}{IndentClassicDescription(target.Description)}{reset}", $"{brightGreen}Obvious exits: {exits}{reset}" });
    }


    private static bool CanCharacterSeeExit(RuntimeSession session, RuntimeExit exit)
    {
        if (!exit.GateKind.Equals("hidden", StringComparison.OrdinalIgnoreCase)) return true;
        return RuntimeBundleDSearchRuntime.Registry.IsDiscovered(session.Actor.Id, exit.Id);
    }

    private void EnsureClassicArenaPopulation(RuntimeSession session)
    {
        if (!session.World.Definition.Id.Equals("classic", StringComparison.OrdinalIgnoreCase)) return;
        if (session.CurrentRoom.Id is not ("room.map1.2150" or "room.map1.2155")) return;
        if (LivingNpcsInRoom(session).Any()) return;

        // Original Classic observation: Newhaven Arena rotates low-level combatants
        // into the room, including giant rats and kobold thieves.
        var selector = Math.Abs(DateTimeOffset.UtcNow.Minute + DateTimeOffset.UtcNow.Second) % 2;
        var npcId = selector == 0 ? "1" : "7";
        Living(session).SummonNpc(npcId, session.CurrentRoom.Id, "classic-newhaven-arena-rotation");
    }

    private static string IndentClassicDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;
        // Imported Classic descriptions can contain fixed-width source line breaks and
        // indentation. Collapse them to a single paragraph and let the Telnet client
        // wrap at its negotiated display width, matching original MajorMUD output.
        var normalized = string.Join(" ", description
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return "    " + normalized;
    }

    public string RenderClassicProfile(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");

        string ExplicitBinary(string key) => character.Conditions.TryGetValue(key, out var raw)
            ? (raw.Equals("1", StringComparison.OrdinalIgnoreCase) ? "On" : "Off")
            : "Unresolved";
        string ExplicitValue(string key, string unresolved = "Unresolved") =>
            character.Conditions.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw.Trim() : unresolved;

        var statline = GetClassicStatlineMode(character).Replace('_', ' ');
        var roomMode = UsesBriefRoomDescriptions(session) ? "Brief" : "Verbose";
        var talk = character.Conditions.TryGetValue(MajorMudTalkFastCondition, out var talkRaw)
            ? (talkRaw.Equals("1", StringComparison.OrdinalIgnoreCase) ? "Fast" : "Slow")
            : "Unresolved";
        var follow = character.Conditions.TryGetValue(ClassicPartyFollowModeKey, out var followRaw)
            && followRaw.Equals("blind", StringComparison.OrdinalIgnoreCase) ? "Blind" : "Normal";
        var messages = ReadClassicMessagesLevel(character);
        if (messages.Length == 0) messages = "Unresolved";
        var entrances = ReadClassicEntrancesEnabled(character) ? "On" : "Off";

        return string.Join(Environment.NewLine, new[]
        {
            "Current profile settings:",
            $"Statline: {statline}",
            $"Room descriptions: {roomMode}",
            $"Talk: {talk}",
            $"Receive: {(IsClassicReceiveEnabled(character) ? "On" : "Off")}",
            $"Follow: {follow}",
            $"Messages: {messages}",
            $"Entrances: {entrances}",
            $"Telepath receipt: {(CommunicationFlagEnabled(character, ClassicTelepathReceiveEnabledKey) ? "On" : "Off")}",
            $"Gossip receipt: {(CommunicationFlagEnabled(character, ClassicGossipEnabledKey) ? "On" : "Off")}",
            $"Auction receipt: {(CommunicationFlagEnabled(character, ClassicAuctionEnabledKey) ? "On" : "Off")}",
            $"Warning: {ExplicitBinary(RuntimeClassicWarningAuthority.ConditionKey)}",
            $"Interrupt: {ExplicitBinary(ClassicInterruptEnabledKey)}",
            $"Keep: {(RuntimeClassicDeathExperienceAuthority.IsPlayerKeepExperienceEnabled(character) ? "On" : "Off")}",
            $"Palette: {ExplicitValue(ClassicPaletteKey)}"
        });
    }

    public string RenderClassicUserScan(RuntimeSession session)
    {
        var users = _sessions.ActiveSessions
            .Where(active => active.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase))
            .Where(active => active.Actor is RuntimeCharacterActor)
            .OrderBy(active => active.Actor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (users.Length == 0) return "There are no users in the game at the moment.";

        var gangs = new RuntimeClassicGangStateService(_characters.ProjectRoot);
        var lines = new List<string>
        {
            "Current Adventurers",
            "===================",
            string.Empty,
            "Name | Class | Level | Reputation | Gang/Guild | Location"
        };
        foreach (var active in users)
        {
            var actor = (RuntimeCharacterActor)active.Actor;
            var character = _characters.LoadCharacter(actor.Id);
            var reputation = character?.SemanticState?.Alignment?.Reputation ?? "Unresolved";
            var gang = gangs.FindByMember(actor.Id)?.Name ?? "-";
            lines.Add($"{actor.DisplayName} | {actor.CharacterClass} | {actor.Level} | {reputation} | {gang} | {active.CurrentRoom.Name}");
        }
        return string.Join(Environment.NewLine, lines);
    }


    public string ConfigureClassicSetting(RuntimeSession session, string arguments)
    {
        var raw = (arguments ?? string.Empty).Trim();
        if (raw.StartsWith("statline", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicStatline(session, raw, invokedThroughSet: true);
        if (raw.StartsWith("receive", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicReceive(session, raw["receive".Length..].Trim());
        if (raw.StartsWith("speech", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicSpeechSetting(session, raw["speech".Length..].Trim());
        if (raw.StartsWith("talk", StringComparison.OrdinalIgnoreCase))
            return ConfigureMajorMudTalkMode(session, raw["talk".Length..].Trim());
        if (raw.StartsWith("palette", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicPalette(session, raw["palette".Length..].Trim());
        if (raw.StartsWith("warning", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicBinaryProfileSetting(session, RuntimeClassicWarningAuthority.ConditionKey, raw["warning".Length..].Trim(), "Warning", "majormud-set-warning",
                "You will now be warned and stopped from doing most evil actions.",
                "You will no longer be stopped from performing evil actions.",
                "Valid warning options: ON, OFF");
        if (raw.StartsWith("interrupt", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicBinaryProfileSetting(session, ClassicInterruptEnabledKey, raw["interrupt".Length..].Trim(), "Interrupt", "majormud-set-interrupt",
                "PVP Combat will now interrupt your typing and output.",
                "PVP Combat will be displayed as normal messages.",
                "Valid interrupt options: ON, OFF");
        if (raw.StartsWith("follow", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicPartyFollowMode(session, raw["follow".Length..].Trim());
        if (raw.StartsWith("messages", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicMessagesLevel(session, raw["messages".Length..].Trim());
        if (raw.StartsWith("maxpages", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicReceiveLimit(session, ClassicMaxPagesKey, raw["maxpages".Length..].Trim(), "MAXPAGES");
        if (raw.StartsWith("maxgossip", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicReceiveLimit(session, ClassicMaxGossipKey, raw["maxgossip".Length..].Trim(), "MAXGOSSIP");
        if (raw.StartsWith("maxauction", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicReceiveLimit(session, ClassicMaxAuctionKey, raw["maxauction".Length..].Trim(), "MAXAUCTION");
        if (raw.StartsWith("keep", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicKeepExperience(session, raw["keep".Length..].Trim());
        if (raw.StartsWith("entrances", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicEntrances(session, raw["entrances".Length..].Trim());
        if (raw.StartsWith("telepath", StringComparison.OrdinalIgnoreCase))
            return ConfigureClassicTelepathReceive(session, raw["telepath".Length..].Trim());
        if (raw.StartsWith("gossip", StringComparison.OrdinalIgnoreCase))
            return ConfigureCommunicationFlag(session, ClassicGossipEnabledKey, raw["gossip".Length..].Trim(), "Gossip");
        if (raw.StartsWith("auction", StringComparison.OrdinalIgnoreCase))
            return ConfigureCommunicationFlag(session, ClassicAuctionEnabledKey, raw["auction".Length..].Trim(), "Auction");
        return "Unknown setting.";
    }

    public string ConfigureClassicKeepExperience(RuntimeSession session, string arguments)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var raw = (arguments ?? string.Empty).Trim();
        var current = RuntimeClassicDeathExperienceAuthority.IsPlayerKeepExperienceEnabled(character);
        bool enabled;
        if (raw.Length == 0) enabled = !current;
        else if (raw.Equals("on", StringComparison.OrdinalIgnoreCase)) enabled = true;
        else if (raw.Equals("off", StringComparison.OrdinalIgnoreCase)) enabled = false;
        else return "Usage: SET KEEP ON|OFF.";

        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeClassicDeathExperienceAuthority.KeepExperienceConditionKey] = enabled ? "1" : "0"
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, "classic.set-keep");
        return enabled ? "Keep Experience On." : "Keep Experience Off.";
    }

    private string ConfigureClassicPalette(RuntimeSession session, string arguments)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var raw = (arguments ?? string.Empty).Trim();
        if (!raw.Equals("0", StringComparison.OrdinalIgnoreCase)) return "Usage: SET PALETTE 0.";

        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [ClassicPaletteKey] = "0"
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, "majormud-set-palette");
        return "Palette 0.";
    }

    private string ConfigureClassicBinaryProfileSetting(
        RuntimeSession session,
        string conditionKey,
        string arguments,
        string label,
        string saveReason,
        string? enabledMessage = null,
        string? disabledMessage = null,
        string? invalidMessage = null)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var raw = (arguments ?? string.Empty).Trim();
        var current = character.Conditions.TryGetValue(conditionKey, out var stored)
            && stored.Equals("1", StringComparison.OrdinalIgnoreCase);

        bool enabled;
        if (raw.Length == 0) enabled = !current;
        else if (raw.Equals("on", StringComparison.OrdinalIgnoreCase)) enabled = true;
        else if (raw.Equals("off", StringComparison.OrdinalIgnoreCase)) enabled = false;
        else return invalidMessage ?? $"Usage: SET {label.ToUpperInvariant()} ON|OFF.";

        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [conditionKey] = enabled ? "1" : "0"
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, saveReason);
        return enabled ? (enabledMessage ?? $"{label} On.") : (disabledMessage ?? $"{label} Off.");
    }

    public string ConfigureClassicPartyFollowMode(RuntimeSession session, string arguments)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var raw = (arguments ?? string.Empty).Trim();
        var current = character.Conditions.TryGetValue(ClassicPartyFollowModeKey, out var stored)
            && stored.Equals("blind", StringComparison.OrdinalIgnoreCase) ? "Blind" : "Normal";

        // Classic SET options are toggles when invoked bare unless retained evidence
        // proves a non-binary/value form. SET FOLLOW also has explicit BLIND/NORMAL.
        string mode;
        if (raw.Length == 0) mode = current == "Blind" ? "normal" : "blind";
        else if (raw.Equals("blind", StringComparison.OrdinalIgnoreCase)) mode = "blind";
        else if (raw.Equals("normal", StringComparison.OrdinalIgnoreCase)) mode = "normal";
        else return "Usage: SET FOLLOW BLIND|NORMAL.";

        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [ClassicPartyFollowModeKey] = mode
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow });
        return mode == "blind" ? "Follow mode is Blind." : "Follow mode is Normal.";
    }

    public string ConfigureClassicMessagesLevel(RuntimeSession session, string arguments)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var raw = (arguments ?? string.Empty).Trim().ToLowerInvariant();
        if (raw.Length == 0) raw = RuntimeClassicCommunicationPersistenceLimitsAuthority.CycleMessagesLevel(ReadClassicMessagesLevel(character));
        if (raw is not ("low" or "medium" or "high"))
            return "Usage: SET MESSAGES LOW|MEDIUM|HIGH.";

        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [ClassicMessagesLevelKey] = raw
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, "majormud-set-messages");
        return $"Messages {char.ToUpperInvariant(raw[0])}{raw[1..]}.";
    }

    public string ConfigureClassicEntrances(RuntimeSession session, string arguments)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var raw = (arguments ?? string.Empty).Trim();
        var current = ReadClassicEntrancesEnabled(character);

        bool entrancesEnabled;
        if (raw.Length == 0) entrancesEnabled = !current;
        else if (raw.Equals("on", StringComparison.OrdinalIgnoreCase)) entrancesEnabled = true;
        else if (raw.Equals("off", StringComparison.OrdinalIgnoreCase)) entrancesEnabled = false;
        else return "Usage: SET ENTRANCES ON|OFF.";

        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [ClassicEntrancesEnabledKey] = entrancesEnabled ? "1" : "0"
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, "majormud-set-entrances");
        return entrancesEnabled ? "Entrances On." : "Entrances Off.";
    }

    internal static string ReadClassicMessagesLevel(RuntimeCharacterRecord character) =>
        character.Conditions.TryGetValue(ClassicMessagesLevelKey, out var raw)
        && raw is not null
        && (raw.Equals("low", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("medium", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("high", StringComparison.OrdinalIgnoreCase))
            ? raw.ToLowerInvariant()
            : RuntimeClassicCommunicationPersistenceLimitsAuthority.DefaultMessagesLevel;

    internal static bool ReadClassicEntrancesEnabled(RuntimeCharacterRecord character)
    {
        if (!character.Conditions.TryGetValue(ClassicEntrancesEnabledKey, out var raw))
            return RuntimeClassicCommunicationPersistenceLimitsAuthority.DefaultEntrancesEnabled;
        return !raw.Equals("0", StringComparison.OrdinalIgnoreCase);
    }

    public string ConfigureClassicPage(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor) return "A persistent character is required.";
        return session.ToggleClassicPageEnabled() ? "Paging is now set to Ok." : "Paging is now set to Off.";
    }

    private string ConfigureClassicReceiveLimit(RuntimeSession session, string key, string arguments, string label)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        if (!TryParseNativeAtol(arguments, out var parsed)) return $"Usage: SET {label} <number>.";
        var value = RuntimeClassicCommunicationPersistenceLimitsAuthority.NormalizeNativeLimit(parsed);
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase) { [key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, $"majormud-set-{label.ToLowerInvariant()}");
        return $"{label} set to {value}.";
    }

    private static bool TryParseNativeAtol(string arguments, out long value)
    {
        var raw = (arguments ?? string.Empty).TrimStart();
        if (raw.Length == 0) { value = 0; return false; }
        var sign = 1L; var i = 0;
        if (raw[0] is '+' or '-') { if (raw[0] == '-') sign = -1; i++; }
        var start = i; long acc = 0;
        while (i < raw.Length && char.IsDigit(raw[i])) { acc = Math.Min(long.MaxValue / 10, acc) * 10 + (raw[i] - '0'); i++; }
        if (i == start) { value = 0; return true; }
        value = sign < 0 ? -acc : acc; return true;
    }

    public bool ShouldRenderClassicPartyFollowRoom(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return true;
        var character = _characters.LoadCharacter(actor.Id);
        return character is null
            || !character.Conditions.TryGetValue(ClassicPartyFollowModeKey, out var mode)
            || !mode.Equals("blind", StringComparison.OrdinalIgnoreCase);
    }

    public string ConfigureClassicReceive(RuntimeSession session, string arguments)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var raw = (arguments ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            var toggled = !IsClassicReceiveEnabled(character);
            var toggledConditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
            {
                [ClassicReceiveEnabledKey] = toggled ? "1" : "0"
            };
            _characters.SaveCharacter(character with { Conditions = toggledConditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, "majormud-receive-toggle");
            return toggled ? "Receive On." : "Receive Off.";
        }

        bool enabled;
        if (raw.Equals("on", StringComparison.OrdinalIgnoreCase)) enabled = true;
        else if (raw.Equals("off", StringComparison.OrdinalIgnoreCase)) enabled = false;
        else return "Usage: SET RECEIVE ON|OFF.";

        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [ClassicReceiveEnabledKey] = enabled ? "1" : "0"
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow });
        return enabled ? "Receive On." : "Receive Off.";
    }

    private static bool IsClassicReceiveEnabled(RuntimeCharacterRecord character) =>
        !character.Conditions.TryGetValue(ClassicReceiveEnabledKey, out var raw)
        || !raw.Equals("0", StringComparison.OrdinalIgnoreCase);

    public string ConfigureClassicStatline(RuntimeSession session, string arguments, bool invokedThroughSet)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var raw = (arguments ?? string.Empty).Trim();
        if (invokedThroughSet)
        {
            if (!raw.StartsWith("statline", StringComparison.OrdinalIgnoreCase)) return "Unknown setting.";
            raw = raw["statline".Length..].Trim();
        }

        if (raw.Length == 0) return $"Statline is {GetClassicStatlineMode(character).Replace('_', ' ')}.";

        string mode;
        string template = string.Empty;
        if (raw.Equals("on", StringComparison.OrdinalIgnoreCase)) mode = "on";
        else if (raw.Equals("off", StringComparison.OrdinalIgnoreCase)) mode = "off";
        else if (raw.Equals("full", StringComparison.OrdinalIgnoreCase)) mode = "full";
        else if (raw.StartsWith("full custom", StringComparison.OrdinalIgnoreCase))
        {
            mode = "full_custom";
            template = raw["full custom".Length..].Trim();
        }
        else if (raw.StartsWith("custom", StringComparison.OrdinalIgnoreCase))
        {
            mode = "custom";
            template = raw["custom".Length..].Trim();
        }
        else return "Usage: STATLINE ON|OFF|FULL or SET STATLINE [FULL] CUSTOM <format>.";

        if (mode is "custom" or "full_custom")
        {
            if (template.Length == 0) return "A custom statline format is required.";
            if (template.Length > 60) return "The custom statline may not exceed 60 characters.";
        }

        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [ClassicStatlineModeKey] = mode
        };
        if (mode is "custom" or "full_custom") conditions[ClassicStatlineTemplateKey] = template;
        else conditions.Remove(ClassicStatlineTemplateKey);
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow });
        return mode switch
        {
            "off" => "Statline Off.",
            "on" => "Statline On.",
            "full" => "Statline Full.",
            "custom" => "Custom statline set.",
            _ => "Full custom statline set."
        };
    }

    public string RenderClassicPrompt(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return ":";
        var character = _characters.LoadCharacter(actor.Id);
        if (character is null) return ":";
        var mode = GetClassicStatlineMode(character);
        if (mode.Equals("off", StringComparison.OrdinalIgnoreCase)) return ":";
        if (mode is "custom" or "full_custom"
            && character.Conditions.TryGetValue(ClassicStatlineTemplateKey, out var template)
            && !string.IsNullOrWhiteSpace(template))
            return RenderClassicCustomStatline(character, template);

        var position = ResolveClassicPromptPosition(character);
        var usesKai = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId)
            .RequireClass(character.ClassId).Magic?.Type == 5;
        return RuntimeClassicCombatAnsi.Prompt(
            character.HitPoints, character.MaximumHitPoints,
            character.Mana, character.MaximumMana,
            usesKai, position);
    }


    private string ResolveClassicPromptPosition(RuntimeCharacterRecord character)
    {
        if (_focusedRegeneration.TryGetValue(character.CharacterId, out var focused))
            return focused.Mode.Equals("meditate", StringComparison.OrdinalIgnoreCase) ? "Meditating" : "Resting";
        return RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Resting) ? "Resting" : string.Empty;
    }

    private static string GetClassicStatlineMode(RuntimeCharacterRecord character) =>
        character.Conditions.TryGetValue(ClassicStatlineModeKey, out var mode) && !string.IsNullOrWhiteSpace(mode)
            ? mode.Trim().ToLowerInvariant()
            : "full";

    private string RenderClassicCustomStatline(RuntimeCharacterRecord character, string template)
    {
        var position = ResolveClassicPromptPosition(character);
        var resting = string.IsNullOrWhiteSpace(position) ? string.Empty : $" ({position}) ";
        var wealth = character.ItemState is null ? 0L : RuntimeBundleCInventoryAuthority.ReadCurrency(character.ItemState).CopperValue;
        var experienceToNext = 0L;
        try
        {
            var schedule = RuntimeCharacterProgressionSchedule.Load(Path.Combine(_characters.ProjectRoot, "Data", character.WorldId));
            var foundation = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId);
            var row = schedule.Require(character, checked(character.Level + 1), foundation.RequireRace(character.RaceId), foundation.RequireClass(character.ClassId));
            experienceToNext = Math.Max(0L, row.RequiredExperience - character.Experience);
        }
        catch { }

        return RuntimeMajorMudCompatibility.RenderStatlineTemplate(
            template, character.HitPoints, character.MaximumHitPoints, character.Mana, character.MaximumMana,
            wealth, character.Experience, experienceToNext, resting);
    }

    private static string ResolveLiveNpcDisposition(RuntimeLivingNpcState npc)
    {
        if (npc.Metadata.TryGetValue("disposition", out var disposition) && !string.IsNullOrWhiteSpace(disposition))
        {
            return disposition;
        }

        if (npc.Metadata.TryGetValue("alignment", out var alignment) && alignment.Contains("evil", StringComparison.OrdinalIgnoreCase))
        {
            return "hostile";
        }

        if (npc.InteractionModel.Contains("service", StringComparison.OrdinalIgnoreCase) ||
            npc.InteractionModel.Contains("shop", StringComparison.OrdinalIgnoreCase) ||
            npc.InteractionModel.Contains("trainer", StringComparison.OrdinalIgnoreCase))
        {
            return "friendly";
        }

        return "neutral";
    }

    internal static bool IsNpcAttackSuppressedByItem(RuntimeLivingNpcState npc, RuntimeCharacterRecord? character)
    {
        if (character?.ItemState?.Items is null
            || !npc.Metadata.TryGetValue("non_attack_item_legacy_id", out var raw)
            || !int.TryParse(raw, out var legacyItemId) || legacyItemId <= 0) return false;
        var canonicalItemId = $"item.{legacyItemId}";
        return character.ItemState.Items.Any(item => item.OwnerId is not null
            && item.OwnerId.Equals(character.CharacterId, StringComparison.OrdinalIgnoreCase)
            && item.CanonicalItemId.Equals(canonicalItemId, StringComparison.OrdinalIgnoreCase));
    }

    public string RenderStatLine(RuntimeSession session)
    {
        const string status = "\u001b[1;37;44m";
        const string reset = "\u001b[0m";
        if (session.Actor is RuntimeCharacterActor actor)
        {
            var character = _characters.LoadCharacter(actor.Id);
            if (character is not null)
            {
                var items = LoadItemState(session);
                var gold = items?.Gold ?? 0;
                var state = RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Resting) ? "Resting" : "Ready";
                return $"{status} HP:{character.HitPoints}/{character.MaximumHitPoints}  MA:{character.Mana}/{character.MaximumMana}  EXP:{character.Experience}  GP:{gold}  {state} {reset}";
            }
        }
        return $"{status} Ready {reset}";
    }

    public string Examine(RuntimeSession session, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Examine what?";
        if (session.Actor is RuntimeCharacterActor visionActor
            && _characters.LoadCharacter(visionActor.Id) is { } visionCharacter
            && RuntimeClassicStatusProjection.IsVisionBlocked(visionCharacter))
            return RuntimeClassicCombatAnsi.Warning("You are blind.");
        var value = query.Trim();
        var observerRecord = session.Actor is RuntimeCharacterActor observerActor ? _characters.LoadCharacter(observerActor.Id) : null;
        var playerMatches = VisiblePlayerMatches(session, value);
        var npcMatches = LivingNpcsInRoom(session)
            .Where(npc => npc.InstanceId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || npc.NpcId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || npc.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToArray();
        var itemMatches = LoadVisibleGround(session)
            .Where(item => item.InstanceId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || item.CanonicalId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || item.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (playerMatches.Length > 1) return $"More than one visible character matches '{value}'; use a longer name abbreviation.";
        if (playerMatches.Length == 1)
        {
            var actor = playerMatches[0].Actor;
            if (actor is RuntimeCharacterActor character)
            {
                var record = _characters.LoadCharacter(character.Id);
                if (record is null) return $"{actor.DisplayName} [{actor.Kind}] id={actor.Id}";
                var pronoun = string.Equals(character.Sex, "Female", StringComparison.OrdinalIgnoreCase) ? "She" : "He";
                var isSelf = character.Id.Equals(session.Actor.Id,StringComparison.OrdinalIgnoreCase);
                var appearance = RuntimeClassicCharacterAppearance.Compose(
                    new RuntimeClassicAppearanceViewer(ConditionIsTrue(observerRecord,"see_hidden"), ConditionIsTrue(observerRecord,"detect_invisible"), isSelf),
                    Appearance(record,character));
                var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
                var shadowLook = RuntimeClassicCharacterAppearance.TryResolveEquippedShadowLookOverride(record, catalog, isSelf, out var shadowPayload);
                var visibleIdentity = shadowLook ? "A shadowy figure" : character.DisplayName;
                var lines = new List<string> { $"[ {visibleIdentity} ]", string.Empty };
                var native = RuntimeClassicNativeTextblockPayloadAuthority.Load(_characters.ProjectRoot);
                if (shadowLook)
                    lines.Add(shadowPayload);
                else if (RuntimeClassicCharacterAppearance.TryResolveShadowformOverride(record, native, out var shadowform))
                    lines.Add(shadowform);
                else
                    lines.Add(appearance);
                lines.Add(string.Empty);
                lines.Add($"{pronoun} is equipped with:");
                lines.Add(string.Empty);
                if (_majorMudPlus.TryGetApprovedDescription(session.World.Definition.Id, character.Id, out var customDescription))
                {
                    var equipmentHeadingIndex = lines.FindIndex(line => line.Equals($"{pronoun} is equipped with:", StringComparison.Ordinal));
                    lines.InsertRange(equipmentHeadingIndex < 0 ? lines.Count : equipmentHeadingIndex, customDescription.Concat(new[] { string.Empty }));
                }
                var activeDescriptions = ActiveSpellLookMessages(session, character.Id, shadowLook ? "a shadowy figure" : character.DisplayName);
                if (activeDescriptions.Count > 0)
                {
                    lines.AddRange(activeDescriptions);
                    lines.Add(string.Empty);
                }
                if (record?.ItemState is not null)
                {
                    var equipped = record.ItemState.Items.Where(i => i.OwnerId != null && i.OwnerId.Equals(character.Id, StringComparison.OrdinalIgnoreCase) && i.EquippedSlot is not null).OrderBy(i => catalog.Require(i.CanonicalItemId).Name).ToArray();
                    if (equipped.Length == 0) lines.Add("Nothing.");
                    else foreach (var equippedItem in equipped) lines.Add($"{catalog.Require(equippedItem.CanonicalItemId).Name,-30}({ClassicDisplaySlot(catalog, equippedItem)})");
                }
                else lines.Add("Nothing.");
                return string.Join(Environment.NewLine, lines);
            }
            return $"{actor.DisplayName} [{actor.Kind}] id={actor.Id}";
        }
        var total = npcMatches.Length + itemMatches.Length;
        if (total == 0) return $"You do not see '{value}' here.";
        if (total > 1) return $"More than one visible target matches '{value}'; use a stable ID.";
        if (npcMatches.Length == 1)
        {
            var spawn = npcMatches[0];
            var descriptions = RuntimeClassicDescriptionAuthority.Load(_characters.ProjectRoot, session.World.Definition.Id);
            var baseDescription = descriptions.TryNpcDescription(spawn.NpcId, out var npcDescription)
                ? npcDescription
                : $"{spawn.Name} [instance={spawn.InstanceId}; npc={spawn.NpcId}; spawn={spawn.SpawnId}; hp={spawn.CurrentHitPoints}/{spawn.MaximumHitPoints}]";
            var activeDescriptions = ActiveSpellLookMessages(session, spawn.InstanceId, spawn.Name);
            return activeDescriptions.Count == 0
                ? baseDescription
                : baseDescription + Environment.NewLine + string.Join(Environment.NewLine, activeDescriptions);
        }
        var item = itemMatches[0];
        var itemDescriptions = RuntimeClassicDescriptionAuthority.Load(_characters.ProjectRoot, session.World.Definition.Id);
        return itemDescriptions.TryItemDescription(item.CanonicalId, out var itemDescription) ? itemDescription : $"{item.Name} [item={item.CanonicalId}; instance={item.InstanceId}]";
    }

    private RuntimeSession[] VisiblePlayerMatches(RuntimeSession session, string value, bool includeSelf = true)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var observerRecord = session.Actor is RuntimeCharacterActor observerActor ? _characters.LoadCharacter(observerActor.Id) : null;
        return _sessions.InRoom(session.World.Definition.Id, session.CurrentRoom.Id)
            .Append(session)
            .DistinctBy(other => other.Id, StringComparer.OrdinalIgnoreCase)
            .Where(other => includeSelf || !other.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase))
            .Where(other => RuntimeClassicCharacterLookTargeting.Matches(other.Actor.Id, other.Actor.DisplayName, value))
            .Where(other => other.Actor is not RuntimeCharacterActor targetActor || RuntimeClassicCharacterAppearance.CanView(
                new RuntimeClassicAppearanceViewer(ConditionIsTrue(observerRecord,"see_hidden"), ConditionIsTrue(observerRecord,"detect_invisible"), other.Id.Equals(session.Id,StringComparison.OrdinalIgnoreCase)),
                Appearance(_characters.LoadCharacter(targetActor.Id), targetActor)))
            .ToArray();
    }

    private static bool ConditionIsTrue(RuntimeCharacterRecord? record, string key) =>
        record is not null && record.Conditions.TryGetValue(key, out var value)
        && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private static RuntimeClassicAppearanceInput Appearance(RuntimeCharacterRecord? record, RuntimeCharacterActor actor)
    {
        int Stat(int fallback, string? statusModifier, params string[] names)
        {
            if (record is null) return fallback;
            var value = fallback;
            foreach (var name in names)
            {
                if (!record.BaseStats.TryGetValue(name, out value)) continue;
                break;
            }
            if (!string.IsNullOrWhiteSpace(statusModifier))
                value += RuntimeClassicStatusProjection.Numeric(record, statusModifier);
            return value;
        }
        string Condition(string name) => record is not null && record.Conditions.TryGetValue(name, out var value) ? value : string.Empty;
        bool Flag(string name) => ConditionIsTrue(record, name);
        var narrativeName = Condition("given_name").Trim();
        if (narrativeName.Length == 0)
            narrativeName = actor.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? actor.DisplayName;
        return new(narrativeName, actor.Sex, actor.Race, actor.CharacterClass,
            Condition("hair_length"), Condition("hair_color"), Condition("eye_color"),
            Stat(50, "stat_strength", "strength", "str"),
            Stat(50, "stat_agility", "agility", "agi", "agl"),
            Stat(50, "stat_intellect", "intellect", "intelligence", "int"),
            Stat(50, "stat_wisdom", "willpower", "wil"),
            Stat(50, null, "health", "hea"),
            Stat(50, "stat_charm", "charm", "cha"),
            record?.HitPoints ?? 1, record?.MaximumHitPoints ?? 1,
            Flag("hidden"), Flag("sneaking"), Flag("invisible"), Flag("disguised"), Flag("transformed") || Flag("polymorphed"));
    }

    private const string MajorMudTalkFastCondition = "ui.majormud.talk_fast";
    private const string ClassicTelepathIgnoreKey = "communication.telepath.ignore";
    private const string ClassicTelepathReceiveEnabledKey = "communication.telepath.receive.enabled";
    private const string ClassicGossipEnabledKey = "communication.gossip.enabled";
    private const string ClassicAuctionEnabledKey = "communication.auction.enabled";
    private const string ClassicBroadcastGroupKey = "communication.broadcast.group";

    public string Say(RuntimeSession session, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Say what?";
        if (IsSpeechBlocked(session)) return "You are unable to speak.";
        var message = text.Trim();
        _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} says \"{message}\"");
        return $"You say \"{message}\"";
    }

    public string Yell(RuntimeSession session, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Yell what?";
        if (IsSpeechBlocked(session)) return "You are unable to speak.";
        var message = text.Trim();
        var roomIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { session.CurrentRoom.Id };
        foreach (var exit in session.CurrentRoom.Exits)
        {
            var target = session.World.ResolveExitDestination(exit);
            if (target is not null) roomIds.Add(target.Id);
        }
        foreach (var target in _sessions.ActiveSessions.Where(target =>
            target.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)
            && roomIds.Contains(target.CurrentRoom.Id)
            && !target.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)))
        {
            target.EnqueueMessage($"{session.Actor.DisplayName} yells \"{message}\"");
        }
        return $"You yell \"{message}\"";
    }

    private string ConfigureClassicSpeechSetting(RuntimeSession session, string arguments)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var mode = (arguments ?? string.Empty).Trim();
        if (mode.Length == 0)
        {
            // WCCMMHLP message 119 explicitly says SET SPEECH toggles Fast/Slow.
            var currentlyFast = character.Conditions.TryGetValue(MajorMudTalkFastCondition, out var stored)
                && stored.Equals("1", StringComparison.OrdinalIgnoreCase);
            mode = currentlyFast ? "slow" : "fast";
        }
        return ConfigureMajorMudTalkMode(session, mode);
    }

    public string ConfigureMajorMudTalkMode(RuntimeSession session, string arguments)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var mode = (arguments ?? string.Empty).Trim();
        if (!mode.Equals("fast", StringComparison.OrdinalIgnoreCase) && !mode.Equals("slow", StringComparison.OrdinalIgnoreCase))
            return "Use: TALK FAST or TALK SLOW.";
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [MajorMudTalkFastCondition] = mode.Equals("fast", StringComparison.OrdinalIgnoreCase) ? "1" : "0"
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, "majormud-talk-mode");
        return mode.Equals("fast", StringComparison.OrdinalIgnoreCase) ? "Talk mode is FAST." : "Talk mode is SLOW.";
    }

    public bool TryExecuteMajorMudSpeechShortcut(RuntimeSession session, RuntimeCommand command, out string result)
    {
        result = string.Empty;
        var raw = command.RawInput.TrimStart();
        if (raw.StartsWith(".", StringComparison.Ordinal))
        {
            result = Say(session, raw[1..]);
            return true;
        }
        if (raw.StartsWith("\"", StringComparison.Ordinal))
        {
            result = Yell(session, raw[1..]);
            return true;
        }
        if (raw.StartsWith("/", StringComparison.Ordinal))
        {
            var payload = raw[1..].TrimStart();
            var separator = payload.IndexOf(' ');
            if (separator <= 0 || separator >= payload.Length - 1)
            {
                result = "Telepath to whom, and what?";
                return true;
            }
            result = Telepath(session, payload[..separator], payload[(separator + 1)..]);
            return true;
        }
        return false;
    }

    public string Tell(RuntimeSession session, IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2) return "Use: tell <character> <message>.";
        if (IsSpeechBlocked(session)) return "You are unable to speak.";
        var target = arguments[0];
        var message = string.Join(" ", arguments.Skip(1));
        _sessions.SendTell(session, target, message, out var response);
        return response;
    }

    public string Gossip(RuntimeSession session, string text) => ExecuteGlobalCommunicationChannel(
        session, text, ClassicGossipEnabledKey, "Gossip");

    public string Auction(RuntimeSession session, string text) => ExecuteGlobalCommunicationChannel(
        session, text, ClassicAuctionEnabledKey, "Auction");


    public string Broadcast(RuntimeSession session, string text)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var group = ReadClassicBroadcastGroup(character);
        var raw = (text ?? string.Empty).Trim();
        if (group == RuntimeClassicBroadcastGroupAuthority.DefaultGroup)
            return "Native BROADCAST behavior while in default group 0 is not separately recovered.";

        // WCCMMHLP 123/157 prove the channel-selection/list/broadcast shape.
        // They conflict only on whether group 127 is valid, so Pass 30 activates the
        // shared 0..126 contract and keeps 127 evidence-gated rather than guessing.
        var currentMembers = _sessions.ActiveSessions
            .Where(target => target.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase))
            .Where(target => target.Actor is RuntimeCharacterActor targetActor
                && _characters.LoadCharacter(targetActor.Id) is { } targetRecord
                && ReadClassicBroadcastGroup(targetRecord) == group)
            .OrderBy(target => target.Actor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (raw.Length == 0)
        {
            return currentMembers.Length == 0
                ? $"No connected characters are on broadcast group {group}."
                : $"Broadcast group {group}: {string.Join(", ", currentMembers.Select(target => target.Actor.DisplayName))}";
        }

        if (IsSpeechBlocked(session)) return "You are unable to speak.";
        foreach (var target in currentMembers.Where(target => !target.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)))
            target.EnqueueMessage($"[Broadcast {group}] {session.Actor.DisplayName}: {raw}");
        return $"[Broadcast {group}] You: {raw}";
    }

    internal string SetClassicBroadcastGroup(RuntimeSession session, int group)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var selection = RuntimeClassicBroadcastGroupAuthority.ClassifySelection(group);
        if (selection.State == RuntimeClassicBroadcastGroupSelectionState.EvidenceConflict)
            return "Native HELP conflicts on whether broadcast group 127 is valid; group 127 remains evidence-gated.";
        if (!selection.IsSelectable)
            return "Native broadcast groups are proven only for group 0 and groups 1 through 126; group 127 remains evidence-gated.";

        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [ClassicBroadcastGroupKey] = group.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, "majormud-broadcast-group");
        return group == 0 ? "Broadcast group reset to 0." : $"Broadcast group set to {group}.";
    }

    internal int GetClassicBroadcastGroup(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return 0;
        return _characters.LoadCharacter(actor.Id) is { } character ? ReadClassicBroadcastGroup(character) : 0;
    }

    private static int ReadClassicBroadcastGroup(RuntimeCharacterRecord character)
    {
        if (!character.Conditions.TryGetValue(ClassicBroadcastGroupKey, out var raw)) return 0;
        return int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var group)
            ? RuntimeClassicBroadcastGroupAuthority.NormalizePersistedGroup(group)
            : RuntimeClassicBroadcastGroupAuthority.DefaultGroup;
    }

    public string ForgetTelepath(RuntimeSession session, string query)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var ignored = ReadIgnoredTelepaths(character);
        var raw = (query ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            if (ignored.Count == 0) return "You are not ignoring telepaths from anyone.";
            var names = _characters.ListAllCharacters()
                .Where(candidate => ignored.Contains(candidate.CharacterId))
                .Select(candidate => candidate.DisplayName)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return names.Length == 0 ? string.Join(Environment.NewLine, ignored.Order(StringComparer.OrdinalIgnoreCase)) : string.Join(Environment.NewLine, names);
        }

        var matches = _characters.ListAllCharacters()
            .Where(candidate => candidate.CharacterId.Equals(raw, StringComparison.OrdinalIgnoreCase)
                || candidate.DisplayName.Equals(raw, StringComparison.OrdinalIgnoreCase)
                || candidate.DisplayName.Contains(raw, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (matches.Length != 1) return $"No character uniquely matches '{raw}'.";
        var target = matches[0];
        var nowIgnored = ignored.Add(target.CharacterId) ? true : !ignored.Remove(target.CharacterId);
        SaveIgnoredTelepaths(character, ignored);
        return nowIgnored
            ? $"Telepaths from {target.DisplayName} will now be ignored."
            : $"Telepaths from {target.DisplayName} will now be received.";
    }

    private string Telepath(RuntimeSession session, string targetQuery, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Telepath what?";
        if (IsSpeechBlocked(session)) return "You are unable to speak.";
        var target = _sessions.FindByActor(targetQuery);
        if (target is null) return $"No connected character uniquely matches '{targetQuery}'.";
        if (target.Actor is RuntimeCharacterActor targetActor && _characters.LoadCharacter(targetActor.Id) is { } targetRecord)
        {
            if (!CommunicationFlagEnabled(targetRecord, ClassicTelepathReceiveEnabledKey)
                || ReadIgnoredTelepaths(targetRecord).Contains(session.Actor.Id))
                return $"{target.Actor.DisplayName} is not receiving your telepaths.";
            if (!TryConsumeClassicReceiveBudget(targetRecord, "pages", ClassicMaxPagesKey, RuntimeClassicCommunicationPersistenceLimitsAuthority.DefaultMaxPages))
                return $"{target.Actor.DisplayName} is receiving too many pages or telepaths right now.";
        }
        var message = text.Trim();
        target.EnqueueMessage($"{session.Actor.DisplayName} telepaths: {message}");
        return $"You telepath to {target.Actor.DisplayName}: {message}";
    }

    private string ExecuteGlobalCommunicationChannel(RuntimeSession session, string text, string conditionKey, string channelName)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            var enabled = !CommunicationFlagEnabled(character, conditionKey);
            SaveCommunicationFlag(character, conditionKey, enabled, $"majormud-{channelName.ToLowerInvariant()}-toggle");
            return $"{channelName} {(enabled ? "On" : "Off")}.";
        }
        if (IsSpeechBlocked(session)) return "You are unable to speak.";
        foreach (var target in _sessions.ActiveSessions.Where(target =>
            target.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)
            && !target.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (target.Actor is RuntimeCharacterActor targetActor
                && _characters.LoadCharacter(targetActor.Id) is { } targetRecord)
            {
                if (!CommunicationFlagEnabled(targetRecord, conditionKey)) continue;
                var limitKey = channelName.Equals("Gossip", StringComparison.OrdinalIgnoreCase) ? ClassicMaxGossipKey : ClassicMaxAuctionKey;
                var defaultLimit = channelName.Equals("Gossip", StringComparison.OrdinalIgnoreCase)
                    ? RuntimeClassicCommunicationPersistenceLimitsAuthority.DefaultMaxGossip
                    : RuntimeClassicCommunicationPersistenceLimitsAuthority.DefaultMaxAuction;
                if (!TryConsumeClassicReceiveBudget(targetRecord, channelName, limitKey, defaultLimit)) continue;
            }
            target.EnqueueMessage($"[{channelName}] {session.Actor.DisplayName}: {raw}");
        }
        return $"[{channelName}] You: {raw}";
    }


    private byte ReadClassicReceiveLimit(RuntimeCharacterRecord character, string key, byte nativeDefault)
    {
        if (!character.Conditions.TryGetValue(key, out var raw) || !byte.TryParse(raw, out var parsed)) return nativeDefault;
        return parsed;
    }

    private bool TryConsumeClassicReceiveBudget(RuntimeCharacterRecord target, string counterKind, string limitKey, byte nativeDefault)
    {
        var maximum = ReadClassicReceiveLimit(target, limitKey, nativeDefault);
        // Native fast-update clears the byte counters every three seconds. Reforged projects that
        // global cadence onto deterministic three-second UTC buckets; the compare/increment rule
        // itself is exact: reject only when current > maximum, otherwise increment an 8-bit byte.
        var window = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / RuntimeClassicCommunicationPersistenceLimitsAuthority.MaxPagesWindowSeconds;
        var key = target.CharacterId + "|" + counterKind;
        lock (_classicCommunicationCounterGate)
        {
            var current = _classicCommunicationCounters.TryGetValue(key, out var state) && state.Window == window ? state.Count : (byte)0;
            if (!RuntimeClassicCommunicationPersistenceLimitsAuthority.NativePreIncrementReceiveAllowed(current, maximum)) return false;
            _classicCommunicationCounters[key] = (window, unchecked((byte)(current + 1)));
            return true;
        }
    }

    private string ConfigureClassicTelepathReceive(RuntimeSession session, string arguments)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var raw = (arguments ?? string.Empty).Trim();

        bool enabled;
        if (raw.Length == 0) enabled = !CommunicationFlagEnabled(character, ClassicTelepathReceiveEnabledKey);
        else if (raw.Equals("on", StringComparison.OrdinalIgnoreCase)) enabled = true;
        else if (raw.Equals("off", StringComparison.OrdinalIgnoreCase)) enabled = false;
        else return "Usage: SET TELEPATH ON|OFF.";
        SaveCommunicationFlag(character, ClassicTelepathReceiveEnabledKey, enabled, "majormud-telepath-toggle");
        return enabled ? "Telepath On." : "Telepath Off.";
    }

    private string ConfigureCommunicationFlag(RuntimeSession session, string conditionKey, string arguments, string label)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var raw = (arguments ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            var toggledEnabled = !CommunicationFlagEnabled(character, conditionKey);
            SaveCommunicationFlag(character, conditionKey, toggledEnabled, $"majormud-{label.ToLowerInvariant()}-toggle");
            return $"{label} {(toggledEnabled ? "On" : "Off")}.";
        }
        if (!raw.Equals("on", StringComparison.OrdinalIgnoreCase) && !raw.Equals("off", StringComparison.OrdinalIgnoreCase))
            return $"Usage: SET {label.ToUpperInvariant()} ON|OFF.";
        var enabled = raw.Equals("on", StringComparison.OrdinalIgnoreCase);
        SaveCommunicationFlag(character, conditionKey, enabled, $"majormud-{label.ToLowerInvariant()}-setting");
        return $"{label} {(enabled ? "On" : "Off")}.";
    }

    private void SaveCommunicationFlag(RuntimeCharacterRecord character, string conditionKey, bool enabled, string reason)
    {
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [conditionKey] = enabled ? "1" : "0"
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, reason);
    }

    private static bool CommunicationFlagEnabled(RuntimeCharacterRecord character, string conditionKey) =>
        !character.Conditions.TryGetValue(conditionKey, out var raw) || !raw.Equals("0", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> ReadIgnoredTelepaths(RuntimeCharacterRecord character) =>
        character.Conditions.TryGetValue(ClassicTelepathIgnoreKey, out var raw) && !string.IsNullOrWhiteSpace(raw)
            ? raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void SaveIgnoredTelepaths(RuntimeCharacterRecord character, IReadOnlySet<string> ignored)
    {
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        if (ignored.Count == 0) conditions.Remove(ClassicTelepathIgnoreKey);
        else conditions[ClassicTelepathIgnoreKey] = string.Join('|', ignored.Order(StringComparer.OrdinalIgnoreCase));
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow }, "majormud-telepath-forget");
    }

    private bool IsSpeechBlocked(RuntimeSession session) =>
        session.Actor is RuntimeCharacterActor actor
        && _characters.LoadCharacter(actor.Id) is { } character
        && RuntimeClassicStatusProjection.IsSpeechBlocked(character);

    public string DrainMessages(RuntimeSession session)
    {
        var messages = session.DrainMessages();
        return messages.Count == 0 ? string.Empty : string.Join(Environment.NewLine, messages);
    }

    public void BroadcastDeparture(RuntimeSession session, string roomId, string direction)
    {
        var expanded = RuntimeMajorMudCompatibility.ExpandDirection(direction);
        foreach (var target in _sessions.ActiveSessions.Where(target =>
            target.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)
            && target.CurrentRoom.Id.Equals(roomId, StringComparison.OrdinalIgnoreCase)
            && !target.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)))
        {
            target.EnqueueMessage($"{session.Actor.DisplayName} just left to the {expanded}.");
        }
    }

    public void BroadcastArrival(RuntimeSession session, string movementDirection)
    {
        var message = $"{session.Actor.DisplayName} walks into the room from the {RuntimeMajorMudCompatibility.OppositeDirection(movementDirection)}.";
        _sessions.BroadcastRoom(session, message);
    }

    private IEnumerable<string> LoadVisibleCorpses(RuntimeSession session)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var active in _sessions.ActiveSessions.Where(active => active.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (active.Actor is not RuntimeCharacterActor actor) continue;
            var state = _characters.LoadCharacter(actor.Id)?.ItemState;
            if (state?.Corpses is null) continue;
            foreach (var corpse in state.Corpses.Where(corpse => !corpse.CleanedUp
                && corpse.RoomId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase)
                && corpse.CharacterId.StartsWith("npc:", StringComparison.OrdinalIgnoreCase)
                && seen.Add(corpse.CorpseId)))
            {
                var npcId = corpse.CharacterId[4..];
                yield return ResolveNpcName(session, npcId);
            }
        }
    }

    private string ResolveNpcName(RuntimeSession session, string npcId)
    {
        var match = Living(session).Capture().Npcs.FirstOrDefault(npc => npc.NpcId.Equals(npcId, StringComparison.OrdinalIgnoreCase));
        return match?.Name ?? npcId;
    }

    private IEnumerable<VisibleGroundItem> LoadVisibleGround(RuntimeSession session)
    {
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in Living(session).GroundItems(session.CurrentRoom.Id).Where(item=>
            RuntimeBundleDSearchRuntime.CanCharacterSeeGroundItem(session.Actor.Id,item)
            &&seen.Add(item.InstanceId)))
        {
            string name;
            try{name=catalog.Require(item.CanonicalItemId).Name;}
            catch{continue;}
            yield return new VisibleGroundItem(item.InstanceId,item.CanonicalItemId,name);
        }
        foreach (var active in _sessions.ActiveSessions.Where(active => active.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (active.Actor is not RuntimeCharacterActor actor) continue;
            var state = _characters.LoadCharacter(actor.Id)?.ItemState;
            if (state is null) continue;
            foreach (var item in state.Items.Where(item =>
                !RuntimeClassicRoomCurrencyStore.IsLegacyCurrency(item)
                && item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)
                && item.LocationId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase)
                && RuntimeBundleDSearchRuntime.CanCharacterSeeGroundItem(session.Actor.Id, item)
                && seen.Add(item.InstanceId)))
            {
                string name;
                try { name = catalog.Require(item.CanonicalItemId).Name; }
                catch { continue; }
                yield return new VisibleGroundItem(item.InstanceId, item.CanonicalItemId, name);
            }
        }
    }

    private sealed record VisibleGroundItem(string InstanceId, string CanonicalId, string Name);
}

public sealed class RuntimeLiveGameplayPart3SmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();
    public static RuntimeLiveGameplayPart3SmokeReport Run()
    {
        var report = new RuntimeLiveGameplayPart3SmokeReport();
        try
        {
            Require(RuntimeCommand.Parse("say hello world").ArgumentText == "hello world", "say-route"); report.Checks.Add("say-route");
            Require(RuntimeCommand.Parse("tell Alice hello").Arguments.Count == 2, "tell-route"); report.Checks.Add("tell-route");
            Require(RuntimeCommand.SupportedCommands.Contains("examine"), "examine-route"); report.Checks.Add("examine-route");
            Require(RuntimeCommand.SupportedCommands.Contains("say") && RuntimeCommand.SupportedCommands.Contains("tell"), "communication-catalog"); report.Checks.Add("communication-catalog");
            Require(typeof(RuntimeSession).GetMethod(nameof(RuntimeSession.DrainMessages)) is not null, "message-mailbox"); report.Checks.Add("message-mailbox");
            Require(typeof(RuntimeSessionManager).GetMethod(nameof(RuntimeSessionManager.InRoom)) is not null, "room-presence"); report.Checks.Add("room-presence");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
