using System.Text.Json;

namespace RealmGate.Server;

public sealed partial class ServerRuntimeHost
{
    /// <summary>
    /// Runs a UI-free, sound-free MajorMUD session over newline-delimited JSON on stdin/stdout.
    /// Each input line is one JSON request and each output line is one complete state snapshot.
    /// This is intentionally owned by the same authoritative runtime used by local/network play.
    /// </summary>
    public async Task RunHeadlessJsonAsync(string playerName, CancellationToken cancellationToken)
    {
        if (_worlds.Count == 0)
            throw new InvalidOperationException("Headless runtime cannot start before Bootstrap().");

        var world = _worlds.TryGetValue(_options.DefaultWorldId, out var configured)
            ? configured
            : _worlds.Values.First();

        var trainingPlayerName = string.IsNullOrWhiteSpace(playerName) ? "Borin" : playerName.Trim();
        var trainingCharacterId = RuntimeIdentity.CharacterId(world.Definition.Id, trainingPlayerName);
        var sessionNumber = 1;

        RuntimeCharacterRecord ResolveTrainingCharacter()
        {
            var existing = _characters.LoadCharacter(trainingCharacterId);
            if (existing is not null)
            {
                if (!existing.WorldId.Equals(world.Definition.Id, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Headless training character '{existing.DisplayName}' belongs to world '{existing.WorldId}', not '{world.Definition.Id}'.");
                return existing;
            }

            // Older Borin saves can carry a character id that predates the current
            // RuntimeIdentity.CharacterId convention. Classic given names are unique,
            // so adopt the existing persistent character by given name before trying
            // to create another one. This also preserves all accumulated training state.
            RuntimeCharacterRecord? FindPersistentByGivenName(string givenName)
            {
                var matchingCharacters = _characters.ListAllCharacters()
                    .Where(character => character.WorldId.Equals(world.Definition.Id, StringComparison.OrdinalIgnoreCase))
                    .Where(character => RuntimeIdentity.ClassicGivenNameFromDisplayName(character.DisplayName)
                        .Equals(givenName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (matchingCharacters.Length == 1)
                    return matchingCharacters[0];
                if (matchingCharacters.Length > 1)
                    throw new InvalidOperationException(
                        $"Headless training found multiple persistent characters with Classic given name '{givenName}' in world '{world.Definition.Id}'.");
                return null;
            }

            var matchingCharacter = FindPersistentByGivenName(trainingPlayerName);
            if (matchingCharacter is not null)
                return matchingCharacter;

            var catalog = _characters.GetCatalog(world.Definition.Id);
            var race = catalog.Races
                .OrderBy(value => value.LegacyNumber <= 0 ? int.MaxValue : value.LegacyNumber)
                .ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Headless training cannot create a persistent character because the generated race catalog is empty.");
            var characterClass = catalog.Classes
                .OrderBy(value => value.LegacyNumber <= 0 ? int.MaxValue : value.LegacyNumber)
                .ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Headless training cannot create a persistent character because the generated class catalog is empty.");

            var account = _characters.GetOrCreateAccount($"headless-training-{trainingPlayerName}");
            var candidateNames = new[]
            {
                trainingPlayerName,
                "BorinTrain",
                "BorinAI",
                "BorinBot"
            }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase);

            InvalidOperationException? lastNameCollision = null;
            foreach (var candidateName in candidateNames)
            {
                try
                {
                    return _characters.CreateCharacter(
                        account,
                        world,
                        candidateName,
                        "Male",
                        race.Id,
                        characterClass.Id);
                }
                catch (InvalidOperationException exception)
                    when (exception.Message.Equals(RuntimeIdentity.ClassicNameAlreadyInUseMessage, StringComparison.Ordinal))
                {
                    // If another headless process created this same persistent player
                    // between lookup and creation, adopt it. NPC/command reservations still
                    // fall through to the next legal dedicated training name.
                    var racedCharacter = FindPersistentByGivenName(candidateName);
                    if (racedCharacter is not null)
                        return racedCharacter;
                    lastNameCollision = exception;
                }
            }

            throw new InvalidOperationException(
                $"Headless training could not resolve or create a persistent training character for '{trainingPlayerName}'.",
                lastNameCollision);
        }

        RuntimeSession CreateTrainingSession()
        {
            var id = $"borin-headless-{Environment.ProcessId}-{sessionNumber++}";
            var character = ResolveTrainingCharacter();
            var created = _sessions.CreateCharacterSession(id, world, character);
            if (created.Actor is not RuntimeCharacterActor || !created.Actor.IsPersistentCharacter)
                throw new InvalidOperationException(
                    $"Headless training refused to start because '{trainingPlayerName}' was not attached as a persistent character.");
            if (!_sessions.TryAdd(created, int.MaxValue, out var message))
                throw new InvalidOperationException("Unable to create headless session: " + message);
            return created;
        }

        var session = CreateTrainingSession();
        _events.Publish("runtime", "headless-training-started", world.Definition.Id, session.Id, session.Actor.Id, "transport=stdio-json");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var schedulerTask = _scheduler.RunAsync(linked.Token);
        var json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        void WriteState(string operation, string command, string response, string? error = null)
        {
            string SafeDispatch(string stateCommand)
            {
                try { return _commands.Dispatch(session, stateCommand); }
                catch (Exception ex) { return $"unavailable: {ex.Message}"; }
            }

            var visibleNpcs = session.World.FindSpawnsByRoom(session.CurrentRoom.Id)
                .Select(x => new { x.Id, x.DisplayName })
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var actor = session.Actor is RuntimeCharacterActor characterActor
                ? new
                {
                    id = session.Actor.Id,
                    displayName = session.Actor.DisplayName,
                    kind = session.Actor.Kind,
                    persistent = session.Actor.IsPersistentCharacter,
                    race = characterActor.Race,
                    characterClass = characterActor.CharacterClass,
                    level = characterActor.Level,
                    sex = characterActor.Sex
                }
                : new
                {
                    id = session.Actor.Id,
                    displayName = session.Actor.DisplayName,
                    kind = session.Actor.Kind,
                    persistent = session.Actor.IsPersistentCharacter,
                    race = string.Empty,
                    characterClass = string.Empty,
                    level = 0,
                    sex = string.Empty
                };

            var payload = new
            {
                protocol = "borin-headless-v1",
                utc = DateTimeOffset.UtcNow,
                operation,
                command,
                response,
                error,
                actor,
                world = new
                {
                    id = session.World.Definition.Id,
                    name = session.World.Definition.Name,
                    type = session.World.Definition.Type,
                    rules = session.World.Definition.RulesProfile
                },
                room = new
                {
                    id = session.CurrentRoom.Id,
                    number = session.CurrentRoom.RoomNumber,
                    name = session.CurrentRoom.Name,
                    description = session.CurrentRoom.Description,
                    exits = session.CurrentRoom.Exits
                        .Where(x => !string.IsNullOrWhiteSpace(x.Direction))
                        .Select(x => x.Direction)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    exitDetails = session.CurrentRoom.Exits
                        .Where(x => !string.IsNullOrWhiteSpace(x.Direction))
                        .OrderBy(x => x.Direction, StringComparer.OrdinalIgnoreCase)
                        .Select(x => new
                        {
                            x.Id,
                            x.Direction,
                            x.TargetMapId,
                            x.TargetRoomNumber,
                            x.TargetRoomId,
                            x.ExitType,
                            x.GateKind,
                            x.Qualifier,
                            x.RuntimeDisposition,
                            x.DestinationStatus,
                            x.ValidationStatus,
                            x.SourceVerified,
                            x.HasBlockedDestination
                        })
                        .ToArray(),
                    visibleNpcs
                },
                score = SafeDispatch("score"),
                health = SafeDispatch("health"),
                exits = SafeDispatch("exits"),
                profile = SafeDispatch("profile"),
                scan = SafeDispatch("scan"),
                character = SafeDispatch("character"),
                experience = SafeDispatch("experience"),
                wealth = SafeDispatch("wealth"),
                inventory = SafeDispatch("inventory"),
                equipment = SafeDispatch("equipment"),
                keys = SafeDispatch("keys"),
                combat = SafeDispatch("combat"),
                spells = SafeDispatch("spells"),
                powers = SafeDispatch("powers"),
                abilities = SafeDispatch("abilities"),
                skills = SafeDispatch("skills"),
                quest = SafeDispatch("quest"),
                journal = SafeDispatch("journal"),
                services = SafeDispatch("services"),
                bank = SafeDispatch("bank"),
                runtime = new
                {
                    processId = Environment.ProcessId,
                    sessionId = session.Id,
                    playerContract = new
                    {
                        version = "majorMud-player-2026-pass09",
                        terminalEncoding = "CP437",
                        privateClientDataOutOfBand = true,
                        megaMudDirectLoginSupported = true,
                        megaMudCharacterSelectorSupported = true,
                        megaMudUsesTelnetPortOnly = true,
                        defaultPromptAnsiFree = true,
                        healthCommandSupported = true,
                        exitsCommandSupported = true,
                        readyCommandSupported = true,
                        profileCommandSupported = true,
                        scanExactBehaviorRecovered = true,
                        scanDisplaysCurrentAdventurers = true,
                        warningBlocksProvenPositiveEvilPointActions = RuntimeClassicWarningAuthority.ExecutableEvilPointSafeguardConnected,
                        warningOffAllowsProvenEvilPointActions = true,
                        rerollCharacterReplacementConnected = RuntimeClassicDeathExperienceAuthority.FinalRerollConsumerConnected,
                        rerollDiscardsOldCharacterState = true,
                        keepControlsRerollExperienceRetention = true,
                        classicCombatRoundMilliseconds = RuntimeLiveGameplayFacade.ClassicCombatRoundMilliseconds,
                        castRejectsKaiClasses = true,
                        invokeRequiresKaiClass = true,
                        decodedRoomActionsUseNormalPlayerCommands = true
                    }
                },
                pendingMessages = session.DrainMessages(),
                prompt = _commands.RenderClassicPrompt(session)
            };
            Console.Out.WriteLine(JsonSerializer.Serialize(payload, json));
            Console.Out.Flush();
        }

        var gracefulSessionExit = false;
        try
        {
            WriteState("ready", string.Empty, "Headless MajorMUD runtime ready.");
            while (!linked.IsCancellationRequested)
            {
                var line = Console.ReadLine();
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    var operation = ReadText(root, "operation") ?? ReadText(root, "op") ?? "command";

                    if (operation.Equals("shutdown", StringComparison.OrdinalIgnoreCase) ||
                        operation.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteState("shutdown", "quit", "Headless runtime shutting down.");
                        gracefulSessionExit = true;
                        break;
                    }

                    if (operation.Equals("reset", StringComparison.OrdinalIgnoreCase))
                    {
                        _commands.CompleteGracefulSessionExit(session);
                        session = CreateTrainingSession();
                        gracefulSessionExit = false;
                        WriteState("reset", string.Empty, $"Training session reset. Persistent {session.Actor.DisplayName} character state was preserved.");
                        continue;
                    }

                    var command = ReadText(root, "command") ?? ReadText(root, "action") ?? string.Empty;
                    var direction = ReadText(root, "direction");
                    if (command.Equals("move", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(direction))
                        command = direction;
                    if (command.Length == 0)
                    {
                        WriteState("command", string.Empty, string.Empty, "A non-empty command/action is required.");
                        continue;
                    }

                    if (_commands.RequestsSessionExit(session, command))
                    {
                        WriteState("shutdown", command, "Headless runtime shutting down.");
                        gracefulSessionExit = true;
                        break;
                    }

                    var response = _commands.Dispatch(session, command);
                    WriteState("command", command, response);
                }
                catch (Exception ex)
                {
                    WriteState("error", string.Empty, string.Empty, ex.Message);
                }

                await Task.Yield();
            }
        }
        finally
        {
            if (_sessions.ActiveSessions.Any(active => active.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)))
            {
                if (gracefulSessionExit) _commands.CompleteGracefulSessionExit(session);
                else _commands.CompleteUnexpectedSessionExit(session);
            }
            linked.Cancel();
            try { await schedulerTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            _events.Publish("runtime", "headless-training-stopped", world.Definition.Id, null, null, "transport=stdio-json");
        }
    }

    private static string? ReadText(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : value.ToString().Trim();
    }
}
