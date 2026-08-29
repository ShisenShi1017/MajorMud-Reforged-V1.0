using System.Text.Json;

namespace Reforged.Server;

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

    public static RuntimeClassicExitTrapCatalog Load(string projectRoot)
    {
        // Exit traps are generated as authoritative effects on the chunked exits catalog.
        // Do not require a second, independently generated exit_traps.json file.
        var exitsPath = Path.Combine(projectRoot, "Data", "Classic", "exits.json");
        if (!File.Exists(exitsPath))
            throw new FileNotFoundException("Generated Classic exits catalog was not found.", exitsPath);

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
    private RuntimeClassicExitTrapCatalog? _classicExitTraps;
    private readonly object _classicExitTrapGate = new();

    private RuntimeClassicExitTrapCatalog ClassicExitTraps()
    {
        lock (_classicExitTrapGate)
            return _classicExitTraps ??= RuntimeClassicExitTrapCatalog.Load(_characters.ProjectRoot);
    }

    public string ApplyExitTrap(RuntimeSession session, RuntimeRoom sourceRoom, string direction)
    {
        var exit = session.World.FindExit(sourceRoom, direction);
        if (exit is null || !ClassicExitTraps().TryGetByExitId(exit.Id, out var trap)) return string.Empty;
        if (!trap.FromRoomId.Equals(sourceRoom.Id, StringComparison.OrdinalIgnoreCase) ||
            !trap.ToRoomId.Equals(exit.TargetRoomId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Decoded exit trap endpoint mismatch: {trap.ExitId}");
        if (session.Actor is not RuntimeCharacterActor) return string.Empty;

        // Strict Classic fail-closed rule: CodeUpdate 0333 now projects every stock WCCMP002
        // RoomRec Para2 trap state exactly. Missing state is therefore a data-integrity failure, not
        // permission to guess. Exact per-state trigger/transition/reset behavior is still not proven.
        if (!trap.NativeActivationStateProjected) return string.Empty;

        // The five native state values and all 295 stock per-exit values are recovered, but exact
        // traversal/transition behavior is not yet executable-proven. Projected traps remain fail-closed.
        return string.Empty;

        /* Activation-state closure will re-enable one of these exact effect consumers:
        return trap.TrapKind.Equals("damage_trap", StringComparison.OrdinalIgnoreCase)
            ? ApplyDecodedDamageExitTrap(session, actor, trap)
            : ApplyDecodedSpellExitTrap(session, actor, trap); */
    }

    private string ApplyDecodedDamageExitTrap(RuntimeSession session, RuntimeCharacterActor actor, RuntimeClassicExitTrapDefinition trap)
    {
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found for decoded exit trap.");
        var nextHitPoints = Math.Max(0, character.HitPoints - trap.Damage);
        if (nextHitPoints == 0)
            return $"{trap.Qualifier} You take {trap.Damage} damage. {CompletePlayerDefeat(session, character)}";
        _characters.SaveCharacter(character with { HitPoints = nextHitPoints, UpdatedAtUtc = DateTimeOffset.UtcNow }, $"decoded-exit-trap:{trap.ExitId}");
        return $"{trap.Qualifier} You take {trap.Damage} damage. HP: {nextHitPoints}/{character.MaximumHitPoints}.";
    }

    private string ApplyDecodedSpellExitTrap(RuntimeSession session, RuntimeCharacterActor actor, RuntimeClassicExitTrapDefinition trap)
    {
        EnsureLivePowers(session);
        var spell = ResolveSpell(trap.SpellId.ToString());
        var casterId = trap.TrapId;
        if (!_liveSpellEngine!.Knows(casterId, spell.Id)) _liveSpellEngine.Learn(casterId, spell.Id);
        _liveSpellEngine.SetResources(casterId, new RuntimeSpellResources(int.MaxValue / 4, int.MaxValue / 4));
        var execution = new RuntimeSpellExecutionEngine(_liveSpellCatalog!, _liveSpellEngine,
            new GeneratedLiveSpellOperationPolicy(this, session), new LiveSpellEventSink(this, session));
        var now = DateTimeOffset.UtcNow;
        var result = execution.Cast(new RuntimeSpellCastRequest(casterId, spell.Id, new RuntimeSpellTarget("character", actor.Id), now));
        if (!result.Succeeded) throw new InvalidOperationException($"Decoded spell trap {trap.ExitId} failed to execute spell {trap.SpellId}: {result.FailureCode} {result.FailureMessage}");
        NormalizeDecodedPlayerStatusCast(session, result.Cast.Effect, now);
        SynchronizeCharacterSpellStatuses(new RuntimeSpellTarget("character", actor.Id), now, $"decoded-exit-spell-trap:{trap.ExitId}");
        PersistLivePowers(session.World.Definition.Id);
        return $"{trap.Qualifier} {spell.Name} strikes you. " + string.Join(" ", result.Events.Select(RenderSpellEvent));
    }
}
