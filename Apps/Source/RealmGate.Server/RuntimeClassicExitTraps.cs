using System.Collections.Concurrent;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicExitTrapDefinition(
    string TrapId,
    string ExitId,
    string FromRoomId,
    string ToRoomId,
    string Direction,
    string TrapKind,
    int Damage,
    int SpellId,
    bool NativeActivationStateProjected,
    int? NativeActivationState,
    string Qualifier,
    string RawValue,
    string RuntimeDisposition,
    string SemanticStatus,
    string SourceExitRecord);

public sealed class RuntimeClassicExitTrapCatalog
{
    private readonly IReadOnlyDictionary<string, RuntimeClassicExitTrapDefinition> _byExitId;

    private RuntimeClassicExitTrapCatalog(IReadOnlyDictionary<string, RuntimeClassicExitTrapDefinition> byExitId) => _byExitId = byExitId;

    public int Count => _byExitId.Count;
    public IReadOnlyCollection<RuntimeClassicExitTrapDefinition> Definitions => _byExitId.Values.ToArray();
    public bool TryGetByExitId(string exitId, out RuntimeClassicExitTrapDefinition definition) => _byExitId.TryGetValue(exitId, out definition!);

    public static RuntimeClassicExitTrapCatalog Load(string projectRoot) => Load(projectRoot, "Classic");

    public static RuntimeClassicExitTrapCatalog Load(string projectRoot, string worldId)
    {
        // Exit traps are generated as authoritative effects on the chunked exits catalog.
        // Do not require a second, independently generated exit_traps.json file.
        var resolved = ServerDataRootResolver.Resolve(projectRoot, worldId);
        var exitsPath = Path.Combine(resolved.DataRoot, "exits.json");
        if (!File.Exists(exitsPath))
            throw new FileNotFoundException($"Generated world '{resolved.WorldId}' exits catalog was not found.", exitsPath);

        var definitions = new Dictionary<string, RuntimeClassicExitTrapDefinition>(StringComparer.OrdinalIgnoreCase);
        ForEachGeneratedRecord(exitsPath, value =>
        {
            if (!value.TryGetProperty("effects", out var effects) || effects.ValueKind != JsonValueKind.Object)
                return;

            var damage = JsonValueReader.ReadInt(effects, "damage", 0);
            var spellId = JsonValueReader.ReadInt(effects, "spell_id", 0);
            if (damage <= 0 && spellId <= 0)
                return; // pre/post transition casts are not exit traps.

            var exitId = JsonValueReader.ReadString(value, "id", string.Empty);
            var fromMap = JsonValueReader.ReadInt(value, "from_map_number", 0);
            var fromRoom = JsonValueReader.ReadInt(value, "from_room_number", 0);
            var toMap = JsonValueReader.ReadInt(value, "to_map_number", 0);
            var toRoom = JsonValueReader.ReadInt(value, "to_room_number", 0);
            var sourceRecord = ReadSourceExitRecord(value);

            // The stock RoomRec layout proves Para2 is the signed 16-bit trap-state field
            // (0 Active, 1 Inactive, 2 Hidden, 3 Active-move, 4 Inactive-move). MMUD Explorer's
            // rendered qualifier drops that field. CodeUpdate 0333 directly recovers WCCMP002 and
            // projects the exact state; never infer it from the display qualifier.
            var nativeActivationState = 0;
            var nativeActivationStateProjected = effects.TryGetProperty("native_trap_state", out var nativeTrapStateElement)
                && nativeTrapStateElement.ValueKind == JsonValueKind.Number
                && nativeTrapStateElement.TryGetInt32(out nativeActivationState);

            var definition = new RuntimeClassicExitTrapDefinition(
                $"exit-trap.{exitId}",
                exitId,
                $"room.map{fromMap}.{fromRoom}",
                $"room.map{toMap}.{toRoom}",
                JsonValueReader.ReadString(value, "direction", string.Empty).ToUpperInvariant(),
                damage > 0 ? "damage_trap" : "spell_trap",
                damage,
                spellId,
                nativeActivationStateProjected,
                nativeActivationStateProjected ? nativeActivationState : null,
                JsonValueReader.ReadString(value, "qualifier", string.Empty),
                JsonValueReader.ReadString(value, "raw_value", string.Empty),
                JsonValueReader.ReadString(value, "runtime_disposition", string.Empty),
                JsonValueReader.ReadString(value, "semantic_status", string.Empty),
                sourceRecord);

            Validate(definition);
            if (!definitions.TryAdd(definition.ExitId, definition))
                throw new InvalidOperationException($"Duplicate Classic exit trap owner: {definition.ExitId}");
        });

        if (definitions.Count == 0)
            throw new InvalidOperationException("Generated Classic exits catalog did not contain any live exit-trap effects.");

        return new RuntimeClassicExitTrapCatalog(definitions);
    }

    private static void ForEachGeneratedRecord(string path, Action<JsonElement> visitor)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in root.EnumerateArray()) visitor(value);
            return;
        }

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Generated catalog must contain an array, records array, or record_parts manifest: {path}");

        if (root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in records.EnumerateArray()) visitor(value);
            return;
        }

        if (root.TryGetProperty("record_parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
        {
            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            foreach (var part in parts.EnumerateArray())
            {
                var relative = part.ValueKind == JsonValueKind.String
                    ? part.GetString()
                    : part.TryGetProperty("path", out var pathValue) ? pathValue.GetString() : null;
                if (string.IsNullOrWhiteSpace(relative))
                    throw new InvalidOperationException($"Generated catalog contains an invalid record part: {path}");
                var partPath = Path.GetFullPath(Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(partPath))
                    throw new FileNotFoundException("Generated Classic exit chunk was not found.", partPath);
                ForEachGeneratedRecord(partPath, visitor);
            }
            return;
        }

        throw new InvalidOperationException($"Generated catalog must contain an array, records array, or record_parts manifest: {path}");
    }

    private static string ReadSourceExitRecord(JsonElement value)
    {
        // The canonical generated exit is the trap owner. Evidence paths remain
        // attached to that exit record and are not a second runtime identity.
        return JsonValueReader.ReadString(value, "id", string.Empty);
    }

    private static void Validate(RuntimeClassicExitTrapDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.TrapId) || string.IsNullOrWhiteSpace(definition.ExitId)) throw new InvalidOperationException("Classic exit trap identity is required.");
        if (string.IsNullOrWhiteSpace(definition.FromRoomId) || string.IsNullOrWhiteSpace(definition.ToRoomId)) throw new InvalidOperationException($"Classic exit trap endpoints are required: {definition.ExitId}");
        if (string.IsNullOrWhiteSpace(definition.Direction)) throw new InvalidOperationException($"Classic exit trap direction is required: {definition.ExitId}");
        if (definition.TrapKind.Equals("damage_trap", StringComparison.OrdinalIgnoreCase) && definition.Damage <= 0) throw new InvalidOperationException($"Decoded damage trap has no positive damage: {definition.ExitId}");
        if (definition.TrapKind.Equals("spell_trap", StringComparison.OrdinalIgnoreCase) && definition.SpellId <= 0) throw new InvalidOperationException($"Decoded spell trap has no spell ID: {definition.ExitId}");
        if (!definition.TrapKind.Equals("damage_trap", StringComparison.OrdinalIgnoreCase) && !definition.TrapKind.Equals("spell_trap", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Unsupported generated trap kind: {definition.TrapKind}");
        if (definition.NativeActivationStateProjected && (definition.NativeActivationState is null || !RuntimeClassicRoomExitNativeFieldContract.IsKnownTrapState(definition.NativeActivationState.Value)))
            throw new InvalidOperationException($"Decoded trap has an unsupported native Para2 activation state: {definition.ExitId}");
    }
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly ConcurrentDictionary<string, RuntimeClassicExitTrapCatalog> _classicExitTraps = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _classicTrapMovementPresentationSuppressions = new(StringComparer.OrdinalIgnoreCase);

    private RuntimeClassicExitTrapCatalog ClassicExitTraps(RuntimeSession session) =>
        _classicExitTraps.GetOrAdd(session.World.Definition.Id,
            worldId => RuntimeClassicExitTrapCatalog.Load(_characters.ProjectRoot, worldId));

    public bool TryGetClassicExitTrap(RuntimeSession session, string direction, out RuntimeClassicExitTrapDefinition trap, out int state)
    {
        trap = null!;
        state = -1;
        var exit = session.World.FindExit(session.CurrentRoom, direction);
        if (exit is null || !ClassicExitTraps(session).TryGetByExitId(exit.Id, out trap)) return false;
        if (!trap.NativeActivationStateProjected || trap.NativeActivationState is null)
            throw new InvalidOperationException($"Decoded exit trap has no native activation-state projection: {trap.ExitId}");
        state = Living(session).ReadClassicTrapState(trap.ExitId, trap.NativeActivationState.Value);
        return true;
    }

    public bool CanTraverseClassicExitTrap(RuntimeSession session, string direction, out string failure)
    {
        failure = string.Empty;
        if (!TryGetClassicExitTrap(session, direction, out _, out var state)) return true;
        if (state != 2) return true;
        // Native _move_user treats hidden trap-state 2 as no visible exit during ordinary movement.
        failure = "There is no exit in that direction!";
        return false;
    }

    public bool RevealClassicExitTrap(RuntimeSession session, string exitId)
    {
        var exit = session.CurrentRoom.Exits.FirstOrDefault(candidate => candidate.Id.Equals(exitId, StringComparison.OrdinalIgnoreCase));
        if (exit is null || !ClassicExitTraps(session).TryGetByExitId(exit.Id, out var trap) || trap.NativeActivationState is null) return false;
        var state = Living(session).ReadClassicTrapState(trap.ExitId, trap.NativeActivationState.Value);
        if (state != 2) return false;
        // Native directional SEARCH reports type-9 traps from the shared RoomRec but does not mutate
        // Para2 and does not create a separate per-user discovery node. Hidden state 2 therefore
        // remains movement-blocking after the SEARCH presentation, exactly as the executable does.
        return false;
    }

    public string ExecuteClassicTrapDisarm(RuntimeSession session, string direction)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "Only characters can disarm traps.";
        var exit = session.World.FindExit(session.CurrentRoom, direction);
        if (exit is null || !ClassicExitTraps(session).TryGetByExitId(exit.Id, out var trap))
            return $"You failed to disarm any trap to the {ClassicDirectionWord(direction)}.";
        if (trap.NativeActivationState is null)
            throw new InvalidOperationException($"Decoded exit trap has no native activation-state projection: {trap.ExitId}");

        var state = Living(session).ReadClassicTrapState(trap.ExitId, trap.NativeActivationState.Value);
        if (state is not (0 or 3))
            return $"You failed to disarm any trap to the {ClassicDirectionWord(direction)}.";

        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found for trap disarm.");
        var skill = ResolveClassicDisarmTrapsSkill(character);
        var roll = Random.Shared.Next(0, 100);
        var native = RuntimeClassicRoomsWorldNativeAuthority.Load(_characters.ProjectRoot, session.World.Definition.Id).Require(trap.ExitId);
        if (roll < skill)
        {
            Living(session).WriteClassicTrapStateWithNativeReset(trap.ExitId, state == 0 ? 1 : 4, DateTimeOffset.UtcNow);
            var success = $"You successfully disarmed the trap to the {ClassicDirectionWord(direction)}.";
            // Native type 24 additionally renders Para3 after a successful DISARM.  This is an
            // executable quirk of the stock data even when the authored message wording sounds like
            // a trigger; the success branch does NOT cast the spell.
            if (native.RoomType != 24 || native.Para3 <= 0) return success;
            var successPresentation = EmitClassicTrapMessage(session, trap, native.Para3, suppressOrdinaryMovement: false);
            return JoinClassicTrapOutput(success, successPresentation);
        }

        var failure = $"You failed to disarm any trap to the {ClassicDirectionWord(direction)}.";
        // Native type 9 has a ten-point non-triggering failure band. Type 24 enters its trigger
        // branch on every failed roll. No separate ability-presence gate exists in _cmd_disarm;
        // a zero Traps value simply fails this comparison naturally.
        var triggers = native.RoomType == 24 || roll - 10 >= skill;
        if (!triggers) return failure;

        var presentation = native.Para4 > 0
            ? EmitClassicTrapMessage(session, trap, native.Para4, suppressOrdinaryMovement: false)
            : string.Empty;

        if (state == 3)
        {
            // Native failed DISARM on movement-state 3 calls _move_user(..., mode 7), which bypasses
            // a second trap effect and completes the exit traversal after the Para4 presentation.
            var moved = ForceClassicTrapState3Movement(session, direction);
            return JoinClassicTrapOutput(failure, presentation, moved);
        }

        var effect = ApplyClassicExitTrapEffect(session, actor, trap, native);
        return JoinClassicTrapOutput(failure, presentation, effect);
    }

    public string ApplyExitTrap(RuntimeSession session, RuntimeRoom sourceRoom, string direction)
    {
        var exit = session.World.FindExit(sourceRoom, direction);
        if (exit is null || !ClassicExitTraps(session).TryGetByExitId(exit.Id, out var trap)) return string.Empty;
        if (!trap.FromRoomId.Equals(sourceRoom.Id, StringComparison.OrdinalIgnoreCase) ||
            !trap.ToRoomId.Equals(exit.TargetRoomId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Decoded exit trap endpoint mismatch: {trap.ExitId}");
        if (session.Actor is not RuntimeCharacterActor actor) return string.Empty;
        if (!trap.NativeActivationStateProjected || trap.NativeActivationState is null)
            throw new InvalidOperationException($"Decoded exit trap has no native activation-state projection: {trap.ExitId}");

        var state = Living(session).ReadClassicTrapState(trap.ExitId, trap.NativeActivationState.Value);
        // Native _move_user jump tables: states 0 and 3 enter the trap effect,
        // states 1 and 4 pass normally, and state 2 is rejected by admission above.
        if (state is 1 or 4) return string.Empty;
        if (state == 2) return "There is no exit in that direction!";
        if (state is not (0 or 3))
            throw new InvalidOperationException($"Unsupported native trap state {state} for {trap.ExitId}.");

        var native = RuntimeClassicRoomsWorldNativeAuthority.Load(_characters.ProjectRoot, session.World.Definition.Id).Require(trap.ExitId);
        var messageId = native.RoomType == 9 ? native.Para3 : native.RoomType == 24 ? native.Para4 : 0;
        var presentation = messageId > 0
            ? EmitClassicTrapMessage(session, trap, messageId, suppressOrdinaryMovement: true)
            : string.Empty;
        var effect = ApplyClassicExitTrapEffect(session, actor, trap, native);
        return JoinClassicTrapOutput(presentation, effect);
    }

    public bool ClassicTrapStoppedMovement(RuntimeSession session, RuntimeRoom sourceRoom)
    {
        var stopped = !session.CurrentRoom.Id.Equals(sourceRoom.Id, StringComparison.OrdinalIgnoreCase);
        if (!stopped && session.Actor is RuntimeCharacterActor actor)
        {
            var character = _characters.LoadCharacter(actor.Id);
            stopped = character is null || character.HitPoints <= 0 || RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Dead);
        }
        if (stopped) CancelClassicTrapMovementPresentation(session);
        return stopped;
    }

    public bool ShouldSuppressClassicTrapDeparture(RuntimeSession session) =>
        _classicTrapMovementPresentationSuppressions.ContainsKey(session.Id);

    public bool ConsumeClassicTrapArrivalSuppression(RuntimeSession session) =>
        _classicTrapMovementPresentationSuppressions.TryRemove(session.Id, out _);

    public void CancelClassicTrapMovementPresentation(RuntimeSession session) =>
        _classicTrapMovementPresentationSuppressions.TryRemove(session.Id, out _);

    private string ApplyClassicExitTrapEffect(
        RuntimeSession session,
        RuntimeCharacterActor actor,
        RuntimeClassicExitTrapDefinition trap,
        RuntimeClassicNativeExitRecord native) =>
        native.RoomType == 9
            ? ApplyDecodedDamageExitTrap(session, actor, trap, native)
            : native.RoomType == 24
                ? ApplyDecodedSpellExitTrap(session, actor, trap, native)
                : throw new InvalidOperationException($"Unsupported native trap room type {native.RoomType}: {trap.ExitId}");

    private static int ResolveClassicDisarmTrapsSkill(RuntimeCharacterRecord character) =>
        RuntimeClassicCharacterProgressionFormula.DisarmTrapsValue(character);

    private static string ClassicDirectionWord(string direction) =>
        (direction ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "n" => "north", "s" => "south", "e" => "east", "w" => "west",
            "ne" => "northeast", "nw" => "northwest", "se" => "southeast", "sw" => "southwest",
            "u" => "up", "d" => "down", var value => value
        };

    private string EmitClassicTrapMessage(
        RuntimeSession session,
        RuntimeClassicExitTrapDefinition trap,
        int messageId,
        bool suppressOrdinaryMovement)
    {
        var payloads = RuntimeClassicNativeTextblockPayloadAuthority.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var player = payloads.RequireMessage(messageId);
        var actorName = session.Actor.DisplayName;
        if (payloads.TryResolveSecondaryMessage(messageId, out var sourceTemplate) && !string.IsNullOrWhiteSpace(sourceTemplate))
        {
            var sourceMessage = FormatClassicTrapObserverMessage(sourceTemplate, actorName);
            foreach (var target in _sessions.ActiveSessions.Where(target =>
                target.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)
                && target.CurrentRoom.Id.Equals(trap.FromRoomId, StringComparison.OrdinalIgnoreCase)
                && !target.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)))
                target.EnqueueMessage(sourceMessage);
        }
        if (payloads.TryResolveTertiaryMessage(messageId, out var destinationTemplate) && !string.IsNullOrWhiteSpace(destinationTemplate))
        {
            var destinationMessage = FormatClassicTrapObserverMessage(destinationTemplate, actorName);
            foreach (var target in _sessions.ActiveSessions.Where(target =>
                target.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)
                && target.CurrentRoom.Id.Equals(trap.ToRoomId, StringComparison.OrdinalIgnoreCase)
                && !target.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)))
                target.EnqueueMessage(destinationMessage);
        }
        if (suppressOrdinaryMovement) _classicTrapMovementPresentationSuppressions[session.Id] = 0;
        return player;
    }

    private static string FormatClassicTrapObserverMessage(string template, string actorName) =>
        template.Replace("%s", actorName, StringComparison.Ordinal);

    private string ApplyDecodedDamageExitTrap(
        RuntimeSession session,
        RuntimeCharacterActor actor,
        RuntimeClassicExitTrapDefinition trap,
        RuntimeClassicNativeExitRecord native)
    {
        if (native.Para1 <= 0) throw new InvalidOperationException($"Native damage trap has non-positive Para1: {trap.ExitId}");
        // WCCMMUD.DLL type-9 movement and failed-DISARM owners call genrdn(Para1>>1, Para1+1).
        var damage = Random.Shared.Next(native.Para1 >> 1, checked(native.Para1 + 1));
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found for decoded exit trap.");
        var nextHitPoints = Math.Max(0, character.HitPoints - damage);
        if (nextHitPoints == 0) return CompletePlayerDefeat(session, character);
        _characters.SaveCharacter(character with { HitPoints = nextHitPoints, UpdatedAtUtc = DateTimeOffset.UtcNow }, $"decoded-exit-trap:{trap.ExitId}");
        return string.Empty;
    }

    private string ApplyDecodedSpellExitTrap(
        RuntimeSession session,
        RuntimeCharacterActor actor,
        RuntimeClassicExitTrapDefinition trap,
        RuntimeClassicNativeExitRecord native)
    {
        if (native.Para1 <= 0) throw new InvalidOperationException($"Native spell trap has non-positive Para1 spell ID: {trap.ExitId}");
        EnsureLivePowers(session);
        var spell = ResolveSpell(native.Para1.ToString());
        var casterId = trap.TrapId;
        if (!_liveSpellEngine!.Knows(casterId, spell.Id)) _liveSpellEngine.Learn(casterId, spell.Id);
        _liveSpellEngine.SetResources(casterId, new RuntimeSpellResources(int.MaxValue / 4, int.MaxValue / 4));
        var execution = new RuntimeSpellExecutionEngine(_liveSpellCatalog!, _liveSpellEngine,
            new GeneratedLiveSpellOperationPolicy(this, session), new LiveSpellEventSink(this, session));
        var now = DateTimeOffset.UtcNow;
        var result = execution.Cast(new RuntimeSpellCastRequest(casterId, spell.Id, new RuntimeSpellTarget("character", actor.Id), now));
        if (!result.Succeeded) throw new InvalidOperationException($"Decoded spell trap {trap.ExitId} failed to execute spell {native.Para1}: {result.FailureCode} {result.FailureMessage}");
        NormalizeDecodedPlayerStatusCast(session, result.Cast.Effect, now);
        SynchronizeCharacterSpellStatuses(new RuntimeSpellTarget("character", actor.Id), now, $"decoded-exit-spell-trap:{trap.ExitId}");
        PersistLivePowers(session.World.Definition.Id);
        var after = _characters.LoadCharacter(actor.Id);
        if (after is not null && after.HitPoints <= 0 && !RuntimeCharacterFlagResolver.IsSet(after, RuntimeCharacterFlagIds.Dead))
            return CompletePlayerDefeat(session, after);
        // Player/source/destination wording belongs to the WCCMSG Para4 payload emitted by the
        // movement/failed-DISARM caller, not to a synthetic spell-name wrapper.
        return string.Empty;
    }

    private string ForceClassicTrapState3Movement(RuntimeSession session, string direction)
    {
        if (!session.TryMove(direction, out _)) return string.Empty;
        if (session.Actor is RuntimeCharacterActor actor && _characters.LoadCharacter(actor.Id) is { } character)
            _characters.SaveCharacter(character with
            {
                WorldId = session.World.Definition.Id,
                RoomId = session.CurrentRoom.Id,
                RoomNumber = session.CurrentRoom.RoomNumber,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }, "classic-trap-state3-forced-move");
        return RenderRoom(session);
    }

    private static string JoinClassicTrapOutput(params string[] values) =>
        string.Join(Environment.NewLine, values.Where(value => !string.IsNullOrWhiteSpace(value)));
}
