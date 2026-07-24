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
        var path = Path.Combine(projectRoot, "Data", "Classic", "exit_traps.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Generated Classic exit trap catalog was not found.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Classic exit trap catalog must be a JSON array.");
        var definitions = new Dictionary<string, RuntimeClassicExitTrapDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in document.RootElement.EnumerateArray())
        {
            var definition = new RuntimeClassicExitTrapDefinition(
                JsonValueReader.ReadString(value, "trap_id", string.Empty),
                JsonValueReader.ReadString(value, "exit_id", string.Empty),
                JsonValueReader.ReadString(value, "from_room_id", string.Empty),
                JsonValueReader.ReadString(value, "to_room_id", string.Empty),
                JsonValueReader.ReadString(value, "direction", string.Empty),
                JsonValueReader.ReadString(value, "trap_kind", string.Empty),
                JsonValueReader.ReadInt(value, "damage", 0),
                JsonValueReader.ReadInt(value, "spell_id", 0),
                JsonValueReader.ReadString(value, "qualifier", string.Empty),
                JsonValueReader.ReadString(value, "raw_value", string.Empty),
                JsonValueReader.ReadString(value, "runtime_disposition", string.Empty),
                JsonValueReader.ReadString(value, "semantic_status", string.Empty),
                JsonValueReader.ReadString(value, "source_exit_record", string.Empty));
            Validate(definition);
            if (!definitions.TryAdd(definition.ExitId, definition)) throw new InvalidOperationException($"Duplicate Classic exit trap owner: {definition.ExitId}");
        }
        return new RuntimeClassicExitTrapCatalog(definitions);
    }

    private static void Validate(RuntimeClassicExitTrapDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.TrapId) || string.IsNullOrWhiteSpace(definition.ExitId)) throw new InvalidOperationException("Classic exit trap identity is required.");
        if (string.IsNullOrWhiteSpace(definition.FromRoomId) || string.IsNullOrWhiteSpace(definition.ToRoomId)) throw new InvalidOperationException($"Classic exit trap endpoints are required: {definition.ExitId}");
        if (string.IsNullOrWhiteSpace(definition.Direction)) throw new InvalidOperationException($"Classic exit trap direction is required: {definition.ExitId}");
        if (definition.TrapKind.Equals("damage_trap", StringComparison.OrdinalIgnoreCase) && definition.Damage <= 0) throw new InvalidOperationException($"Decoded damage trap has no positive damage: {definition.ExitId}");
        if (definition.TrapKind.Equals("spell_trap", StringComparison.OrdinalIgnoreCase) && definition.SpellId <= 0) throw new InvalidOperationException($"Decoded spell trap has no spell ID: {definition.ExitId}");
        if (!definition.TrapKind.Equals("damage_trap", StringComparison.OrdinalIgnoreCase) && !definition.TrapKind.Equals("spell_trap", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Unsupported generated trap kind: {definition.TrapKind}");
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
        if (session.Actor is not RuntimeCharacterActor actor) return string.Empty;
        return trap.TrapKind.Equals("damage_trap", StringComparison.OrdinalIgnoreCase)
            ? ApplyDecodedDamageExitTrap(session, actor, trap)
            : ApplyDecodedSpellExitTrap(session, actor, trap);
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
