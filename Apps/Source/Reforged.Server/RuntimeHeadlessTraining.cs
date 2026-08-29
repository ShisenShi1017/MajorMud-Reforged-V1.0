using System.Text.Json;

namespace Reforged.Server;

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

        var sessionNumber = 1;
        RuntimeSession CreateTrainingSession()
        {
            var id = $"borin-headless-{Environment.ProcessId}-{sessionNumber++}";
            var created = _sessions.CreateSession(id, world, string.IsNullOrWhiteSpace(playerName) ? "Borin" : playerName.Trim());
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
                        WriteState("reset", string.Empty, "Training session reset. Persistent Borin character state was preserved.");
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
