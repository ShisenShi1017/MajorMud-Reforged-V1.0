using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeWccSysopCommand(
    string Id,
    string Command,
    bool GameopAllowed,
    string Owner,
    string Permission,
    IReadOnlyList<string> Forms,
    string RuntimeDisposition,
    IReadOnlyDictionary<string,string> Targets,
    bool AllowAllTargets,
    int DamageMinimumMaximumHitPointPercent,
    int DamageMaximumMaximumHitPointPercent,
    int NativeMessageId);

public sealed class RuntimeWccSysopCatalog
{
    private RuntimeWccSysopCatalog(
        string legacySetting,
        string configuredValue,
        string runtimeAuthority,
        bool plaintextKeyCommandAllowed,
        IReadOnlyDictionary<string, RuntimeWccSysopCommand> commands)
    {
        LegacySetting = legacySetting;
        ConfiguredValue = configuredValue;
        RuntimeAuthority = runtimeAuthority;
        PlaintextKeyCommandAllowed = plaintextKeyCommandAllowed;
        Commands = commands;
    }

    public string LegacySetting { get; }
    public string ConfiguredValue { get; }
    public string RuntimeAuthority { get; }
    public bool PlaintextKeyCommandAllowed { get; }
    public IReadOnlyDictionary<string, RuntimeWccSysopCommand> Commands { get; }

    public static RuntimeWccSysopCatalog Load(string projectRoot, string worldId)
    {
        var path = Path.Combine(projectRoot, "Data", worldId, "sysop_commands.json");
        if (!File.Exists(path)) throw new InvalidDataException($"Generated WCCMMUD SYSOP command data was not found: {path}");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Generated WCCMMUD SYSOP command records are missing.");

        var sourcePath = root.TryGetProperty("source_file", out var sourceFile)
            ? Path.GetFullPath(Path.Combine(projectRoot, sourceFile.GetString()!.Replace('/', Path.DirectorySeparatorChar)))
            : string.Empty;
        using var sourceDocument = File.Exists(sourcePath) ? JsonDocument.Parse(File.ReadAllText(sourcePath)) : null;
        var authorization = sourceDocument?.RootElement.GetProperty("authorization");
        var commands = records.EnumerateArray().Select(record =>
        {
            var forms = record.TryGetProperty("forms", out var formValues) && formValues.ValueKind == JsonValueKind.Array
                ? formValues.EnumerateArray().Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
                : [];
            var targets = record.TryGetProperty("targets", out var targetValues) && targetValues.ValueKind == JsonValueKind.Object
                ? targetValues.EnumerateObject().ToDictionary(
                    property => NormalizeDestination(property.Name),
                    property => property.Value.GetString() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            return new RuntimeWccSysopCommand(
                record.GetProperty("id").GetString() ?? string.Empty,
                record.GetProperty("command").GetString() ?? string.Empty,
                record.GetProperty("gameop_allowed").GetBoolean(),
                record.GetProperty("owner").GetString() ?? string.Empty,
                record.GetProperty("permission").GetString() ?? string.Empty,
                forms,
                record.GetProperty("runtime_disposition").GetString() ?? string.Empty,
                targets,
                record.TryGetProperty("allow_all_targets", out var allowAll) && allowAll.GetBoolean(),
                JsonValueReader.ReadInt(record, "damage_minimum_maximum_hp_percent", 0),
                JsonValueReader.ReadInt(record, "damage_maximum_maximum_hp_percent", 0),
                JsonValueReader.ReadInt(record, "native_message_id", 0));
        }).ToDictionary(record => record.Command, StringComparer.OrdinalIgnoreCase);

        return new RuntimeWccSysopCatalog(
            ReadAuthorization(authorization, "legacy_setting", "SYSOPKEY"),
            ReadAuthorization(authorization, "user_confirmed_configured_value", "configured-outside-runtime"),
            ReadAuthorization(authorization, "runtime_authority", "authenticated_account_role_and_explicit_capabilities"),
            authorization?.TryGetProperty("plaintext_key_command_allowed", out var plaintext) == true && plaintext.GetBoolean(),
            commands);
    }

    private static string ReadAuthorization(JsonElement? authorization, string name, string fallback) =>
        authorization is { } value && value.TryGetProperty(name, out var property)
            ? property.GetString() ?? fallback
            : fallback;

    public static string NormalizeDestination(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}

public sealed class RuntimeWccSysopService
{
    private readonly RuntimeCharacterService _characters;
    private readonly RuntimeSessionManager _sessions;
    private readonly RuntimeAccountSecurityService _security;
    private readonly RuntimeClassicGangStateService _gangs;

    public RuntimeWccSysopService(RuntimeCharacterService characters, RuntimeSessionManager sessions)
    {
        _characters = characters;
        _sessions = sessions;
        _security = new RuntimeAccountSecurityService(characters.ProjectRoot);
        _gangs = new RuntimeClassicGangStateService(characters.ProjectRoot);
    }

    public string Execute(RuntimeSession session, RuntimeCommand command, RuntimeLivingWorldService living)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            throw new InvalidOperationException("A persistent authenticated character is required.");
        var character = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found.");
        var credential = _security.TryLoad(character.AccountId);
        var policy = RuntimeClassicAdministrationPolicyStore.Load(_characters.ProjectRoot, character.WorldId);
        var accountRole = credential is { Enabled: true } ? credential.Role : RuntimeAccountRole.Player;
        var characterGrant = policy.HasCharacterSysopAccess(character.CharacterId);
        var hasAdministrativeAuthority = characterGrant || accountRole is RuntimeAccountRole.Moderator or RuntimeAccountRole.Sysop or RuntimeAccountRole.Administrator;

        var catalog = RuntimeWccSysopCatalog.Load(_characters.ProjectRoot, character.WorldId);
        if (catalog.PlaintextKeyCommandAllowed)
            throw new InvalidDataException("The generated SYSOP catalog incorrectly enables plaintext key commands.");
        if (command.Arguments.Count == 0)
        {
            if (hasAdministrativeAuthority)
                return RenderHelp(catalog, characterGrant ? RuntimeAccountRole.Sysop : accountRole, gotoOnly: false);
            if (policy.EveryoneCanUseSysGoto)
                return RenderHelp(catalog, RuntimeAccountRole.Player, gotoOnly: true);
            throw new InvalidOperationException("WCCMMUD GAMEOP or SYSOP authority is required.");
        }

        var verb = command.Arguments[0];
        var effectiveRole = characterGrant ? RuntimeAccountRole.Sysop : accountRole;
        if (verb.Equals("pvp", StringComparison.OrdinalIgnoreCase))
        {
            if (effectiveRole is not (RuntimeAccountRole.Sysop or RuntimeAccountRole.Administrator))
                throw new InvalidOperationException("SYSOP authority is required for PVP.");
            return ExecutePvp(character, command.Arguments.Skip(1).ToArray());
        }
        if (!catalog.Commands.TryGetValue(verb, out var definition))
            return $"Unknown WCCMMUD SYSOP command '{verb}'. Type SYSOP for the decoded command catalog.";

        var publicGoto = policy.EveryoneCanUseSysGoto && definition.Command.Equals("goto", StringComparison.OrdinalIgnoreCase);
        if (!hasAdministrativeAuthority && !publicGoto)
            throw new InvalidOperationException("WCCMMUD GAMEOP or SYSOP authority is required.");

        if (effectiveRole == RuntimeAccountRole.Moderator && !definition.GameopAllowed)
            throw new InvalidOperationException($"SYSOP authority is required for {definition.Command.ToUpperInvariant()}.");

        var arguments = command.Arguments.Skip(1).ToArray();
        return definition.Command.ToLowerInvariant() switch
        {
            "buffers" => ExecuteBuffers(arguments),
            "status" => ExecuteStatus(session, arguments),
            "map" => ExecuteMap(session),
            "god" => ExecuteGod(arguments),
            "list" => ExecuteList(arguments, living, character.WorldId),
            "report" => ExecuteReport(arguments),
            "lightning" => ExecuteLightning(session, definition, arguments),
            "disband" => ExecuteGangCommand("disband", arguments),
            "disable" => ExecuteGangCommand("disable", arguments),
            "enable" => ExecuteGangCommand("enable", arguments),
            "gangsize" => ExecuteGangCommand("gangsize", arguments),
            "unwrap" => ExecuteGangCommand("unwrap", arguments),
            "goto" => ExecuteGoto(session, definition, arguments),
            "initialize" => ExecuteInitialize(arguments, living),
            "configure" => ExecuteConfigure(arguments),
            "reload" => "SYSOP RELOAD has no literal file target in Reforged: native MajorMUD reloaded WCCMMUD.INI, while Reforged stores equivalent configuration in its own internal configuration system.",
            "reset" => ExecuteReset(session, arguments, living),
            "speak" => ExecuteSpeak(session, arguments),
            "arena" => ExecuteArena(character.WorldId, arguments),
            "invasion" => "Native invasions are room-scoped decoded triggers, not a SYSOP subcommand. Use SYSOP GOTO SUPPORT, then enter ORC INVASION, CULT INVASION, RED INVASION, GREEN INVASION, DUERGAR INVASION, or SARACEN INVASION.",
            _ => RenderUnavailable(definition)
        };
    }

    private string ExecutePvp(RuntimeCharacterRecord character, IReadOnlyList<string> arguments)
    {
        var store = new RuntimePvpWorldConfigurationStore(_characters.ProjectRoot, character.WorldId);
        if (arguments.Count == 0 || arguments[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            var state = store.Capture();
            var range = state.MaximumLevelDifference <= 0 ? "unrestricted" : state.MaximumLevelDifference.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return $"PvP is {(state.Enabled ? "ENABLED" : "DISABLED")} for {character.WorldId}; maximum level difference={range}. " +
                   "When enabled, PvP is permitted everywhere except rooms carrying proven safe-room evidence.";
        }
        var enabled = arguments.Count == 1 && (arguments[0].Equals("enable", StringComparison.OrdinalIgnoreCase) ||
                                               arguments[0].Equals("on", StringComparison.OrdinalIgnoreCase));
        var disabled = arguments.Count == 1 && (arguments[0].Equals("disable", StringComparison.OrdinalIgnoreCase) ||
                                                arguments[0].Equals("off", StringComparison.OrdinalIgnoreCase));
        if (arguments.Count == 2 && (arguments[0].Equals("level", StringComparison.OrdinalIgnoreCase) || arguments[0].Equals("range", StringComparison.OrdinalIgnoreCase)))
        {
            if (!int.TryParse(arguments[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var maximumDifference) || maximumDifference < -1)
                return "Usage: SYSOP PVP RANGE <positive levels|-1 for unrestricted>";
            var ranged = store.SetMaximumLevelDifference(maximumDifference, character.CharacterId);
            return ranged.MaximumLevelDifference <= 0
                ? $"PvP level range is now unrestricted for {character.WorldId}."
                : $"PvP is now restricted to a {ranged.MaximumLevelDifference}-level difference for {character.WorldId}.";
        }
        if (!enabled && !disabled) return "Usage: SYSOP PVP STATUS|ENABLE|DISABLE|RANGE <levels|-1>";
        var updated = store.SetEnabled(enabled, character.CharacterId);
        return $"PvP is now {(updated.Enabled ? "ENABLED" : "DISABLED")} for {character.WorldId}.";
    }

    private static string RenderHelp(RuntimeWccSysopCatalog catalog, RuntimeAccountRole role, bool gotoOnly)
    {
        var commands = catalog.Commands.Values
            .Where(command => !gotoOnly || command.Command.Equals("goto", StringComparison.OrdinalIgnoreCase))
            .Where(command => role != RuntimeAccountRole.Moderator || command.GameopAllowed)
            .OrderBy(command => command.Command, StringComparer.OrdinalIgnoreCase)
            .Select(command => $"{command.Command.ToUpperInvariant()} [{command.RuntimeDisposition}]");
        var authority = gotoOnly ? "world policy: SYSOP GOTO enabled for all players" : catalog.RuntimeAuthority;
        return $"WCCMMUD SYSOP control ({authority}; role={role}).{Environment.NewLine}" +
               string.Join(Environment.NewLine, commands) +
               (gotoOnly ? string.Empty : $"{Environment.NewLine}PVP [STATUS|ENABLE|DISABLE] [Reforged realm authority]") +
               $"{Environment.NewLine}The legacy {catalog.LegacySetting} value is never accepted as a player command.";
    }

    private string ExecuteBuffers(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0) return $"Runtime buffers: activeSessions={_sessions.ActiveSessionCount}.";
        if (arguments[0].Equals("save", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var active in _sessions.ActiveSessions.Where(active => active.Actor.IsPersistentCharacter))
                _characters.SaveSession(active);
            return $"Saved {_sessions.ActiveSessions.Count(active => active.Actor.IsPersistentCharacter)} active character session(s).";
        }
        if (arguments[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
            return "WCCMMUD BUFFERS CLEAR is connected but no safe equivalent host buffer mutation exists; no state changed.";
        return "Usage: SYSOP BUFFERS [CLEAR|SAVE]";
    }

    private string ExecuteStatus(RuntimeSession session, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0) return _sessions.RenderStatus();
        if (arguments[0].Equals("room", StringComparison.OrdinalIgnoreCase))
        {
            var room = ResolveRoom(session.World, arguments.Skip(1).ToArray());
            return room is null
                ? "The requested map-qualified room was not found."
                : $"Room={room.Id}; Name={room.Name}; Map={room.MapId}; Number={room.RoomNumber}; ActiveSessions={_sessions.InRoom(session.World.Definition.Id, room.Id).Count}";
        }
        var target = _sessions.FindByActor(string.Join(' ', arguments));
        return target is null
            ? "No active character uniquely matches that identifier."
            : $"{target.Actor.Id}: name={target.Actor.DisplayName}; world={target.World.Definition.Id}; room={target.CurrentRoom.Id}; connectedUtc={target.ConnectedAtUtc:O}";
    }

    private static string ExecuteMap(RuntimeSession session) =>
        $"Map={session.CurrentRoom.MapId}; Room={session.CurrentRoom.RoomNumber}; Id={session.CurrentRoom.Id}; Name={session.CurrentRoom.Name}";

    private string ExecuteList(IReadOnlyList<string> arguments, RuntimeLivingWorldService living, string worldId)
    {
        if (arguments.Count == 0) return "Usage: SYSOP LIST USERS|EVIL|GANG <gangname>|LIMITED";
        if (!arguments[0].Equals("users", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments[0].Equals("evil", StringComparison.OrdinalIgnoreCase))
            {
                var evilUsers = _sessions.ActiveSessions
                    .Where(session => session.Actor.IsPersistentCharacter)
                    .Select(session => (Session: session, Character: _characters.LoadCharacter(session.Actor.Id)))
                    .Where(entry => entry.Character?.SemanticState?.Alignment is not null)
                    .OrderByDescending(entry => entry.Character!.SemanticState!.Alignment!.EvilPoints)
                    .ThenBy(entry => entry.Character!.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var evilLines = evilUsers.Select(entry =>
                    $"{entry.Character!.DisplayName}; character={entry.Character.CharacterId}; evil={entry.Character.SemanticState!.Alignment!.EvilPoints}; reputation={entry.Character.SemanticState.Alignment.Reputation}; recentEvil={entry.Character.SemanticState.Alignment.PerformedEvilActionToday}; room={entry.Session.CurrentRoom.Id}").ToList();
                evilLines.Add($"Active characters with alignment state: {evilUsers.Length}.");
                return string.Join(Environment.NewLine, evilLines);
            }
            return arguments[0].ToLowerInvariant() switch
            {
                "gang" => RenderGangRoster(arguments.Skip(1).ToArray()),
                "limited" => RenderLimitedItems(living, worldId),
                _ => "Usage: SYSOP LIST USERS|EVIL|GANG <gangname>|LIMITED"
            };
        }

        var active = _sessions.ActiveSessions
            .Where(target => target.Actor.IsPersistentCharacter)
            .OrderBy(target => target.Actor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.Actor.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var lines = active.Select(target =>
        {
            var character = _characters.LoadCharacter(target.Actor.Id);
            var accountId = character?.AccountId ?? "unknown";
            return $"{target.Actor.DisplayName}; account={accountId}; character={target.Actor.Id}; map={target.CurrentRoom.MapId}; room={target.CurrentRoom.RoomNumber}; roomId={target.CurrentRoom.Id}";
        }).ToList();
        lines.Add($"Users in game: {active.Length}.");
        return string.Join(Environment.NewLine, lines);
    }

    private string RenderLimitedItems(RuntimeLivingWorldService living, string worldId)
    {
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, worldId);
        var limited = catalog.Items.Where(item => item.GameLimit > 0)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var characters = _characters.ListAllCharacters()
            .Where(character => character.WorldId.Equals(worldId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var characterNames = characters.ToDictionary(character => character.CharacterId, character => character.DisplayName, StringComparer.OrdinalIgnoreCase);
        var npcNames = living.Capture().Npcs.ToDictionary(npc => npc.InstanceId, npc => npc.Name, StringComparer.OrdinalIgnoreCase);
        var instances = characters.SelectMany(character => character.ItemState?.Items ?? [])
            .Concat(living.Capture().Items ?? [])
            .Where(item => limited.ContainsKey(item.CanonicalItemId))
            .GroupBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        string Location(RuntimeItemInstance item)
        {
            if (item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase))
                return $"Room {item.LocationId}";
            if (item.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) ||
                item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) && npcNames.ContainsKey(item.OwnerId ?? string.Empty))
                return $"Monster {npcNames.GetValueOrDefault(item.OwnerId ?? item.LocationId, item.OwnerId ?? item.LocationId)} ({item.LocationId})";
            if (item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase))
                return $"Corpse {item.LocationId}";
            if (characterNames.TryGetValue(item.OwnerId ?? item.LocationId, out var characterName))
                return $"User {characterName} ({item.LocationKind})";
            return $"{item.LocationKind} {item.LocationId}";
        }

        var lines = instances.GroupBy(item => item.CanonicalItemId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => limited[group.Key].Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var definition = limited[group.Key];
                var locations = group.GroupBy(Location, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(location => location.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(location => $"{location.Key} x{location.Sum(item => Math.Max(1, item.StackQuantity))}");
                return $"{definition.Name} ({definition.Id}; limit={definition.GameLimit}): {string.Join(", ", locations)}";
            }).ToList();
        if (lines.Count == 0) return "No limited items are currently in the world.";
        lines.Add($"Limited item definitions present in world: {lines.Count}.");
        return string.Join(Environment.NewLine, lines);
    }

    private string ExecuteReport(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0) return "Usage: SYSOP REPORT USERS [mask]|NOTOPTEN";
        if (arguments[0].Equals("notopten", StringComparison.OrdinalIgnoreCase))
        {
            var excluded = _characters.ListAllCharacters()
                .Where(character => RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.ToptenDisabled))
                .OrderBy(character => character.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(character => character.CharacterId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var excludedLines = excluded.Select(character =>
                $"{character.DisplayName}; account={character.AccountId}; character={character.CharacterId}; world={character.WorldId}").ToList();
            excludedLines.Add($"Characters excluded from TOPTEN: {excluded.Length}.");
            return string.Join(Environment.NewLine, excludedLines);
        }
        if (!arguments[0].Equals("users", StringComparison.OrdinalIgnoreCase))
            return "Usage: SYSOP REPORT USERS [mask]|NOTOPTEN";

        var mask = string.Join(' ', arguments.Skip(1)).Trim();
        if (mask.Length > 19) return "SYSOP REPORT USERS mask may not exceed 19 characters.";
        var active = _sessions.ActiveSessions
            .Where(target => target.Actor.IsPersistentCharacter)
            .Select(target => (Session: target, Character: _characters.LoadCharacter(target.Actor.Id)))
            .Where(entry => entry.Character is not null)
            .Where(entry => MatchesReportUserMask(entry.Character!.AccountId, entry.Character.DisplayName, mask))
            .OrderBy(entry => entry.Character!.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Character!.AccountId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var lines = active.Select(entry =>
            $"{entry.Character!.DisplayName}; account={entry.Character.AccountId}; character={entry.Character.CharacterId}; world={entry.Session.World.Definition.Id}").ToList();
        lines.Add($"Reported users: {active.Length}{(mask.Length == 0 ? string.Empty : $"; mask={mask}") }.");
        return string.Join(Environment.NewLine, lines);
    }

    internal static bool MatchesReportUserMask(string accountId, string displayName, string mask) =>
        string.IsNullOrEmpty(mask) ||
        accountId.Contains(mask, StringComparison.OrdinalIgnoreCase) ||
        displayName.Contains(mask, StringComparison.OrdinalIgnoreCase);

    private string RenderGangRoster(IReadOnlyList<string> arguments)
    {
        var name = string.Join(' ', arguments).Trim();
        if (name.Length == 0) return "Usage: SYSOP LIST GANG <full gang name>";
        var gang = _gangs.FindExact(name);
        if (gang is null) return $"No gang exactly matches '{name}'.";
        var members = gang.Members.OrderBy(member => member.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(member => $"{member.DisplayName}; character={member.CharacterId}; role={member.Role}; joinedUtc={member.JoinedAtUtc:O}").ToList();
        members.Add($"Gang={gang.Name}; id={gang.GangId}; members={gang.Members.Count}; theoreticalSize={gang.TheoreticalSize}; experience={gang.Experience}; topten={gang.ToptenEnabled}; disbanded={gang.Disbanded}; house={gang.GangHouseId ?? "none"}.");
        return string.Join(Environment.NewLine, members);
    }

    private string ExecuteGangCommand(string command, IReadOnlyList<string> arguments)
    {
        if (command.Equals("unwrap", StringComparison.OrdinalIgnoreCase))
            return "SYSOP UNWRAP is not required for the current standalone Reforged gang state; no state changed.";
        int? requestedSize = null;
        string name;
        if (command.Equals("gangsize", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count < 2 || !int.TryParse(arguments[0], out var size) || size < 0) return "Usage: SYSOP GANGSIZE <newsize> <full gang name>";
            requestedSize = size;
            name = string.Join(' ', arguments.Skip(1)).Trim();
        }
        else name = string.Join(' ', arguments).Trim();
        if (name.Length == 0) return $"Usage: SYSOP {command.ToUpperInvariant()} <full gang name>";
        var gang = _gangs.FindExact(name);
        if (gang is null) return $"No gang exactly matches '{name}'; no state changed.";
        var updated = command switch
        {
            "disband" => _gangs.Disband(gang),
            "disable" => _gangs.SetTopten(gang, false),
            "enable" => _gangs.SetTopten(gang, true),
            "gangsize" => _gangs.SetTheoreticalSize(gang, requestedSize!.Value),
            _ => throw new InvalidOperationException($"Unsupported gang administration command '{command}'.")
        };
        return command switch
        {
            "disband" => $"Gang '{updated.Name}' disbanded; {updated.PendingDisbandNotificationCharacterIds.Count} member notification(s) queued.",
            "disable" => $"Gang '{updated.Name}' is excluded from TOPTEN.",
            "enable" => $"Gang '{updated.Name}' is eligible for TOPTEN.",
            _ => $"Gang '{updated.Name}' theoretical size set to {updated.TheoreticalSize}."
        };
    }

    private string ExecuteLightning(RuntimeSession source, RuntimeWccSysopCommand definition, IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1) return "Usage: SYSOP LIGHTNING <character-id>";
        var targetSession = _sessions.ActiveSessions.SingleOrDefault(candidate =>
            candidate.Actor.IsPersistentCharacter && candidate.Actor.Id.Equals(arguments[0], StringComparison.OrdinalIgnoreCase));
        if (targetSession?.Actor is not RuntimeCharacterActor targetActor) return "You may not lightning that user.";
        var target = _characters.LoadCharacter(targetActor.Id);
        if (target is null || target.HitPoints <= 0 || !definition.AllowAllTargets) return "You may not lightning that user.";
        if (definition.DamageMinimumMaximumHitPointPercent != 25
            || definition.DamageMaximumMaximumHitPointPercent != 50
            || definition.NativeMessageId != 2842)
            throw new InvalidDataException("Generated SYSOP LIGHTNING does not match the recovered native Fiend-lightning contract.");

        var damage = RollNativeLightningDamage(target.MaximumHitPoints, Random.Shared);
        var now = DateTimeOffset.UtcNow;
        if (damage >= target.HitPoints)
        {
            var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, target.WorldId);
            var transition = RuntimeCharacterDeathResolver.Defeat(target, catalog, targetSession.CurrentRoom.Id, now);
            _characters.SaveCharacter(transition.Character, "sysop-lightning-native-fiend-contract");
        }
        else _characters.SaveCharacter(target with { HitPoints = target.HitPoints - damage, UpdatedAtUtc = now }, "sysop-lightning-native-fiend-contract");

        var native = RuntimeClassicNativeTextblockPayloadAuthority.Load(_characters.ProjectRoot);
        if (!native.TryResolveMessage(2842, out var administratorTemplate)
            || !native.TryResolveSecondaryMessage(2842, out var targetTemplate)
            || !native.TryResolveTertiaryMessage(2842, out var observerTemplate))
            throw new InvalidDataException("Native SYSOP LIGHTNING message 2842 is unavailable.");
        var targetName = target.DisplayName;
        targetSession.EnqueueMessage(FormatNativeLightning(targetTemplate, targetName, damage));
        foreach (var observer in _sessions.ActiveSessions.Where(candidate =>
                     candidate.Id != source.Id && candidate.Id != targetSession.Id
                     && candidate.World.Definition.Id.Equals(target.WorldId, StringComparison.OrdinalIgnoreCase)))
            observer.EnqueueMessage(FormatNativeLightning(observerTemplate, targetName, damage));
        return FormatNativeLightning(administratorTemplate, targetName, damage);
    }

    internal static int RollNativeLightningDamage(int maximumHitPoints, Random random)
    {
        if (maximumHitPoints < 1) throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));
        var minimum = checked(maximumHitPoints * 25 / 100);
        var maximum = checked(maximumHitPoints * 50 / 100);
        return random.Next(minimum, checked(maximum + 1));
    }

    internal static string FormatNativeLightning(string template, string targetName, int damage)
    {
        var rendered = template;
        rendered = ReplaceFirst(rendered, "%s", targetName);
        rendered = ReplaceFirst(rendered, "%d", damage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        rendered = ReplaceFirst(rendered, "%s", damage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return rendered.TrimEnd('\r', '\n');
    }

    private static string ReplaceFirst(string value, string token, string replacement)
    {
        var index = value.IndexOf(token, StringComparison.Ordinal);
        return index < 0 ? value : string.Concat(value.AsSpan(0, index), replacement, value.AsSpan(index + token.Length));
    }

    private string ExecuteGod(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2)
            return "Usage: SYSOP GOD <character-id> STATUS|SPEAK <message>|DISABLE TOPTEN|ENABLE TOPTEN";

        var targetSession = _sessions.FindByActor(arguments[0]);
        if (targetSession is null || !targetSession.Actor.IsPersistentCharacter)
            return "The target character must be uniquely identified and active in the game; no state changed.";
        var target = _characters.LoadCharacter(targetSession.Actor.Id);
        if (target is null) return "The active target character save was not found; no state changed.";

        if (arguments[1].Equals("status", StringComparison.OrdinalIgnoreCase) && arguments.Count == 2)
            return $"{targetSession.Actor.Id}: name={targetSession.Actor.DisplayName}; account={target.AccountId}; world={targetSession.World.Definition.Id}; room={targetSession.CurrentRoom.Id}; hp={target.HitPoints}/{target.MaximumHitPoints}; mana={target.Mana}/{target.MaximumMana}; level={target.Level}; experience={target.Experience}; connectedUtc={targetSession.ConnectedAtUtc:O}";

        if (arguments[1].Equals("inventory", StringComparison.OrdinalIgnoreCase) && arguments.Count == 2)
            return RenderGodInventory(target);

        if (arguments[1].Equals("spells", StringComparison.OrdinalIgnoreCase) && arguments.Count == 2)
            return RenderGodSpells(target);

        if (arguments[1].Equals("profile", StringComparison.OrdinalIgnoreCase) && arguments.Count == 2)
            return RenderGodProfile(target);

        if (arguments[1].Equals("powers", StringComparison.OrdinalIgnoreCase) && arguments.Count == 2)
            return RenderGodPowers(target);

        if (arguments[1].Equals("abilities", StringComparison.OrdinalIgnoreCase) && arguments.Count == 2)
            return RenderGodAbilities(target);

        if (arguments[1].Equals("change", StringComparison.OrdinalIgnoreCase) && arguments.Count == 3 && arguments[2].Equals("sex", StringComparison.OrdinalIgnoreCase))
        {
            var sex = target.Sex.Equals("Male", StringComparison.OrdinalIgnoreCase) ? "Female" : "Male";
            _characters.SaveCharacter(target with { Sex = sex, UpdatedAtUtc = DateTimeOffset.UtcNow }, "wccmmud-sysop-change-sex");
            targetSession.EnqueueMessage($"The gods have changed your sex to {sex}.");
            return $"{target.DisplayName}'s sex is now {sex}.";
        }

        if (arguments[1].Equals("add", StringComparison.OrdinalIgnoreCase) && arguments.Count == 4 && arguments[2].Equals("evil", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(arguments[3], out var amount)) return "Usage: SYSOP GOD <character-id> ADD EVIL <amount>";
            var alignment = target.SemanticState?.Alignment ?? RuntimeCharacterAlignmentResolver.CreateInitial();
            var updatedAlignment = RuntimeCharacterAlignmentResolver.ApplyEvilPointDelta(alignment, amount, markPositiveDeltaAsEvilAction: false) with { SourceRecordId = "wccmmud.sysop.god.add_evil" };
            SaveGodAlignment(target, targetSession, updatedAlignment, "add-evil");
            return $"{target.DisplayName} now has {updatedAlignment.EvilPoints} evil point(s).";
        }

        if (arguments.Count == 2 && arguments[1].ToLowerInvariant() is "good" or "saint" or "neutral")
        {
            var reputation = char.ToUpperInvariant(arguments[1][0]) + arguments[1][1..].ToLowerInvariant();
            var alignment = target.SemanticState?.Alignment ?? RuntimeCharacterAlignmentResolver.CreateInitial();
            var updatedAlignment = alignment with { Reputation = reputation, SourceRecordId = $"wccmmud.sysop.god.{arguments[1].ToLowerInvariant()}" };
            SaveGodAlignment(target, targetSession, updatedAlignment, $"set-{reputation.ToLowerInvariant()}");
            return $"{target.DisplayName}'s reputation is now {reputation}.";
        }

        if (arguments[1].Equals("speak", StringComparison.OrdinalIgnoreCase))
        {
            var message = string.Join(' ', arguments.Skip(2)).Trim();
            if (message.Length == 0) return "Usage: SYSOP GOD <character-id> SPEAK <message>";
            targetSession.EnqueueMessage($"The gods tell you: {message}");
            return $"The gods told {target.DisplayName}: {message}";
        }

        if (arguments.Count < 3)
            return "This decoded SYSOP GOD operation is not yet executable; no state changed.";
        var operation = arguments[^2];
        var subject = arguments[^1];
        if (!subject.Equals("topten", StringComparison.OrdinalIgnoreCase) ||
            !operation.Equals("disable", StringComparison.OrdinalIgnoreCase) &&
            !operation.Equals("enable", StringComparison.OrdinalIgnoreCase))
            return "This decoded SYSOP GOD operation is not yet executable; no state changed.";

        if (arguments.Count != 3)
            return "Use the exact active character ID with this SYSOP GOD operation; no state changed.";

        var disable = operation.Equals("disable", StringComparison.OrdinalIgnoreCase);
        var updated = disable
            ? RuntimeCharacterFlagResolver.Set(target, RuntimeCharacterFlagIds.ToptenDisabled, "wccmmud.sysop.god.disable_topten")
            : RuntimeCharacterFlagResolver.Clear(target, RuntimeCharacterFlagIds.ToptenDisabled, "wccmmud.sysop.god.enable_topten");
        _characters.SaveCharacter(updated, disable ? "wccmmud-sysop-disable-topten" : "wccmmud-sysop-enable-topten");
        targetSession.EnqueueMessage(disable
            ? "A SYSOP has excluded your character from TOPTEN listings."
            : "A SYSOP has restored your character to TOPTEN listings.");
        return $"{target.DisplayName} is now {(disable ? "excluded from" : "eligible for")} TOPTEN listings.";
    }

    private string RenderGodInventory(RuntimeCharacterRecord target)
    {
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, target.WorldId);
        var items = target.ItemState?.Items
            .Where(item => item.OwnerId?.Equals(target.CharacterId, StringComparison.OrdinalIgnoreCase) == true &&
                           (item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) ||
                            item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.EquippedSlot is null ? 1 : 0)
            .ThenBy(item => catalog.Require(item.CanonicalItemId).Name, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var definition = catalog.Require(item.CanonicalItemId);
                var quantity = item.StackQuantity > 1 ? $" x{item.StackQuantity}" : string.Empty;
                var slot = item.EquippedSlot is null ? string.Empty : $" [equipped:{item.EquippedSlot}]";
                return $"{definition.Name}{quantity}{slot}; id={item.CanonicalItemId}; instance={item.InstanceId}";
            }).ToArray() ?? [];
        var gold = target.ItemState?.Gold ?? 0;
        return items.Length == 0
            ? $"Inventory for {target.DisplayName}: empty; gold={gold}."
            : $"Inventory for {target.DisplayName}:{Environment.NewLine}{string.Join(Environment.NewLine, items)}{Environment.NewLine}Gold: {gold}";
    }

    private string RenderGodSpells(RuntimeCharacterRecord target)
    {
        var catalog = RuntimeSpellCatalog.Load(_characters.ProjectRoot, target.WorldId);
        var definitions = target.Spells.Select(id =>
                catalog.Spells.SingleOrDefault(spell => spell.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                                                        spell.LegacyId.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .Where(spell => spell is not null)
            .OrderBy(spell => spell!.Name, StringComparer.OrdinalIgnoreCase)
            .Select(spell => $"{spell!.Id}: {spell.Name}; level={spell.SpellLevel}; mana={spell.ManaCost}; energy={spell.EnergyCost}")
            .ToArray();
        return definitions.Length == 0
            ? $"{target.DisplayName} knows no generated spells."
            : $"Spells for {target.DisplayName}:{Environment.NewLine}{string.Join(Environment.NewLine, definitions)}";
    }

    private static string RenderGodProfile(RuntimeCharacterRecord target)
    {
        var stats = string.Join(", ", target.BaseStats.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}={pair.Value}"));
        var equipment = string.Join(", ", target.Equipment.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}={pair.Value ?? "empty"}"));
        return $"Profile for {target.DisplayName}: character={target.CharacterId}; account={target.AccountId}; race={target.RaceName}; class={target.ClassName}; sex={target.Sex}; level={target.Level}; experience={target.Experience}; hp={target.HitPoints}/{target.MaximumHitPoints}; mana={target.Mana}/{target.MaximumMana}; room={target.RoomId}; stats=[{stats}]; equipment=[{equipment}]";
    }

    private static string RenderGodPowers(RuntimeCharacterRecord target)
    {
        var statuses = target.SemanticState?.StatusEffects.OrderBy(effect => effect.EffectId, StringComparer.OrdinalIgnoreCase)
            .Select(effect => $"{effect.EffectId}; magnitude={effect.Magnitude}; remainingTicks={effect.RemainingTicks}; source={effect.SourceRecordId ?? "unknown"}").ToArray() ?? [];
        return statuses.Length == 0 ? $"{target.DisplayName} has no active persisted powers/status effects." : $"Powers for {target.DisplayName}:{Environment.NewLine}{string.Join(Environment.NewLine, statuses)}";
    }

    private static string RenderGodAbilities(RuntimeCharacterRecord target)
    {
        var abilities = target.Skills.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}={pair.Value}").ToArray();
        var permanent = target.SemanticState?.Progression.PermanentAbilityAdjustments.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}={pair.Value}").ToArray() ?? [];
        return $"Abilities for {target.DisplayName}:{Environment.NewLine}skills=[{string.Join(", ", abilities)}]{Environment.NewLine}permanentAdjustments=[{string.Join(", ", permanent)}]";
    }

    private void SaveGodAlignment(RuntimeCharacterRecord target, RuntimeSession targetSession, RuntimeCharacterAlignmentState alignment, string reason)
    {
        var semantic = target.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        _characters.SaveCharacter(target with { SemanticState = semantic with { Alignment = alignment }, UpdatedAtUtc = DateTimeOffset.UtcNow }, $"wccmmud-sysop-{reason}");
        targetSession.EnqueueMessage($"The gods have adjusted your alignment: reputation={alignment.Reputation}; evil={alignment.EvilPoints}.");
    }

    private string ExecuteGoto(RuntimeSession session, RuntimeWccSysopCommand definition, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0) return $"Usage: {definition.Forms.FirstOrDefault() ?? "SYSOP GOTO <destination>"}";
        var destination = RuntimeWccSysopCatalog.NormalizeDestination(string.Join(' ', arguments));
        if (!definition.Targets.TryGetValue(destination, out var roomId) || string.IsNullOrWhiteSpace(roomId))
            return $"Unknown WCCMMUD GOTO destination '{string.Join(' ', arguments)}'; no state changed.";
        var room = session.World.FindRoomById(roomId);
        if (room is null) return $"The evidence-locked GOTO destination {roomId} is absent from generated runtime data; no state changed.";
        session.MoveToWorld(session.World, room, "wccmmud-sysop-goto");
        _characters.SaveSession(session);
        return $"Teleported to {room.Name} ({room.Id}).";
    }

    private static string ExecuteReset(
        RuntimeSession session,
        IReadOnlyList<string> arguments,
        RuntimeLivingWorldService living)
    {
        RuntimeRoom? room;
        if (arguments.Count == 0 || arguments[0].Equals("room", StringComparison.OrdinalIgnoreCase) && arguments.Count == 1)
            room = session.CurrentRoom;
        else if (arguments[0].Equals("area", StringComparison.OrdinalIgnoreCase))
            return "WCCMMUD RESET AREA is cataloged but area boundaries are not proven in generated runtime data; no state changed.";
        else
            room = ResolveRoom(session.World,
                arguments[0].Equals("room", StringComparison.OrdinalIgnoreCase) ? arguments.Skip(1).ToArray() : arguments);
        if (room is null) return "The requested map-qualified room was not found.";

        var slots = living.CaptureNpcSpawnSlots().Where(slot => slot.HomeRoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var slot in slots)
            living.ResetNpcSpawnSlot(slot.SpawnId, DateTimeOffset.UtcNow, "wccmmud-sysop-reset-room");
        return $"Reset {slots.Length} generated NPC spawn slot(s) for {room.Id}.";
    }

    private static string ExecuteInitialize(IReadOnlyList<string> arguments, RuntimeLivingWorldService living)
    {
        var slots = living.CaptureNpcSpawnSlots();
        if (arguments.Count == 1 && arguments[0].Equals("-1", StringComparison.OrdinalIgnoreCase))
            return $"Initialization state: Finished; generated spawn slots={slots.Count}; alive={slots.Sum(slot => slot.AliveCount)}; waiting={slots.Sum(slot => slot.WaitingToRespawnCount)}; capacity={slots.Sum(slot => slot.MaterializedCapacity)}.";
        if (arguments.Count > 2 || arguments.Count > 0 && !int.TryParse(arguments[0], out _))
            return "Usage: SYSOP INITIALIZE|-1|<rate> [RECOVER]";
        if (arguments.Count == 2 && !arguments[1].Equals("recover", StringComparison.OrdinalIgnoreCase))
            return "Usage: SYSOP INITIALIZE <rate> RECOVER";
        var rate = arguments.Count == 0 ? "host-default" : arguments[0];
        var mode = arguments.Count == 2 ? "recovery" : "initialization";
        var now = DateTimeOffset.UtcNow;
        foreach (var slot in slots) living.ResetNpcSpawnSlot(slot.SpawnId, now, $"wccmmud-sysop-{mode}");
        return $"Completed standalone {mode} across {slots.Count} generated NPC spawn slot(s); requestedRate={rate}.";
    }

    private string ExecuteConfigure(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 1 && arguments[0].Equals("show", StringComparison.OrdinalIgnoreCase))
        {
            var validation = new Reforged.Core.Validation.ValidationResult();
            var host = new ServerHostConfigurationAudit().Run(_characters.ProjectRoot, validation);
            return $"Host={host.HostName}; bind={host.BindAddress}; port={host.Port}; maxUsers={host.MaxSessions}; worldTickMs={host.WorldTickMilliseconds}; combatTickMs={host.CombatTickMilliseconds}; gameplay={host.GameplayEnabled}; networking={host.NetworkingEnabled}; worldSimulation={host.WorldSimulationEnabled}; activeUsers={_sessions.ActiveSessionCount}.";
        }
        if (arguments.Count > 0 && arguments[0].Equals("maxusers", StringComparison.OrdinalIgnoreCase))
            return "SYSOP CONFIGURE MAXUSERS is not applicable: realm capacity belongs to the standalone Reforged application configuration; no state changed.";
        return "Usage: SYSOP CONFIGURE SHOW|MAXUSERS <n>";
    }

    private string ExecuteSpeak(RuntimeSession session, IReadOnlyList<string> arguments)
    {
        var message = string.Join(' ', arguments).Trim();
        if (message.Length == 0) return "Usage: SYSOP SPEAK <message>";
        foreach (var target in _sessions.ActiveSessions.Where(target =>
                     target.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)))
            target.EnqueueMessage($"[SYSOP] {session.Actor.DisplayName}: {message}");
        return $"Broadcast to {_sessions.ActiveSessions.Count(target => target.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase))} active realm session(s).";
    }

    private string ExecuteArena(string worldId, IReadOnlyList<string> arguments)
    {
        var path = Path.Combine(RuntimeSavePaths.RealmRoot(_characters.ProjectRoot, worldId), "sysop", $"{RuntimeIdentity.Normalize(worldId, "world")}.arena");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var current = File.Exists(path) ? File.ReadAllText(path).Trim() : "normal";
        if (arguments.Count == 0 || arguments[0].Equals("status", StringComparison.OrdinalIgnoreCase))
            return $"Arena mode: {current}.";
        var requested = arguments[0].ToLowerInvariant();
        if (requested is not ("normal" or "combat")) return "Usage: SYSOP ARENA STATUS|NORMAL|COMBAT";
        File.WriteAllText(path, requested);
        return $"Arena mode set to {requested}. Board cleanup must restore NORMAL.";
    }

    private static RuntimeRoom? ResolveRoom(RuntimeWorld world, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0) return null;
        if (!int.TryParse(arguments[0], out var roomNumber)) return world.FindRoomById(arguments[0]);
        if (arguments.Count > 1 && int.TryParse(arguments[1], out var mapNumber))
            return world.FindRoomById($"room.map{mapNumber}.{roomNumber}");
        return world.FindRoomByNumber(roomNumber);
    }

    private static string RenderUnavailable(RuntimeWccSysopCommand definition) =>
        $"{definition.Command.ToUpperInvariant()} is decoded and authority-gated, but is not executable: " +
        $"{definition.RuntimeDisposition}; owner={definition.Owner}; permission={definition.Permission}. No state changed.";
}
