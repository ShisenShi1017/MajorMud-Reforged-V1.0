using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeClassicNarrativeOperationOwner
{
    BundleACharacter,
    BundleBCombat,
    BundleCItemsEconomy,
    BundleDRoomWorld,
    BundleENpcSpawn,
    BundleFCondition,
    BundleFPresentation,
    BundleFControl
}

public sealed record RuntimeClassicNarrativeOpcodeContract(
    string Opcode,
    RuntimeClassicNarrativeOperationOwner Owner,
    bool MutatesState,
    bool RequiresAtomicBoundary,
    string ConsumerContract,
    string EvidenceClassification);

/// <summary>
/// Canonical ownership and dispatch contract for decoded Classic textblock operations.
/// This registry does not duplicate Bundle A-E mutations; it routes each opcode to the
/// existing authoritative subsystem contract and rejects every unclassified opcode.
/// </summary>
public sealed class RuntimeClassicNarrativeOperationDispatcher
{
    private readonly IReadOnlyDictionary<string, RuntimeClassicNarrativeOpcodeContract> _contracts;

    public RuntimeClassicNarrativeOperationDispatcher()
    {
        var rows = Contracts().ToArray();
        if (rows.Select(x => x.Opcode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Length)
            throw new InvalidOperationException("Duplicate Classic narrative opcode contract.");
        _contracts = rows.ToDictionary(x => x.Opcode, StringComparer.OrdinalIgnoreCase);
    }

    public int Count => _contracts.Count;
    public IReadOnlyCollection<RuntimeClassicNarrativeOpcodeContract> All => _contracts.Values.ToArray();

    public RuntimeClassicNarrativeOpcodeContract RequireContract(string opcode)
    {
        var normalized = (opcode ?? string.Empty).Trim();
        return _contracts.TryGetValue(normalized, out var contract)
            ? contract
            : throw new InvalidOperationException($"Unclassified decoded Classic narrative opcode '{normalized}'.");
    }

    public RuntimeClassicNarrativeOpcodeContract Classify(RuntimeClassicTextblockOperation operation)
        => RequireContract(operation.Opcode);

    private static IEnumerable<RuntimeClassicNarrativeOpcodeContract> Contracts()
    {
        const string verified = "Verified decoded operation vocabulary and established Bundle A-E ownership";
        RuntimeClassicNarrativeOpcodeContract C(string op, RuntimeClassicNarrativeOperationOwner owner, bool mutates, bool atomic, string consumer)
            => new(op, owner, mutates, atomic, consumer, verified);

        // Bundle F presentation/control: rendering and graph flow only.
        yield return C("message", RuntimeClassicNarrativeOperationOwner.BundleFPresentation, false, false, "decoded-message-renderer");
        yield return C("text", RuntimeClassicNarrativeOperationOwner.BundleFPresentation, false, false, "decoded-textblock-renderer");
        yield return C("route", RuntimeClassicNarrativeOperationOwner.BundleFControl, false, false, "decoded-textblock-route");
        yield return C("random", RuntimeClassicNarrativeOperationOwner.BundleFControl, false, false, "decoded-random-branch-selector");
        yield return C("break", RuntimeClassicNarrativeOperationOwner.BundleFControl, false, false, "decoded-operation-stop");
        yield return C("delay", RuntimeClassicNarrativeOperationOwner.BundleFControl, false, false, "bundle-g-delay-request-contract");
        yield return C("adddelay", RuntimeClassicNarrativeOperationOwner.BundleFControl, true, true, "bundle-g-delay-mutation-request");

        // Bundle F conditions evaluate authoritative state without owning it.
        foreach (var op in new[] { "class", "race", "minlevel", "maxlevel", "levelcheck", "goodaligned", "evilaligned", "check", "checkability", "failability", "testability", "testskill", "checkspell", "checkitem", "failitem", "roomitem", "failroomitem", "nomonsters", "monsters", "needmonster", "price" })
            yield return C(op, RuntimeClassicNarrativeOperationOwner.BundleFCondition, false, false, "authoritative-state-condition-evaluator");

        // Bundle A owns permanent character progression and character values.
        foreach (var op in new[] { "giveability", "addability", "removeability", "addexp", "addevil", "goodability" })
            yield return C(op, RuntimeClassicNarrativeOperationOwner.BundleACharacter, true, true, "bundle-a-character-mutation");

        // Bundle B owns spell/status/combat application.
        yield return C("cast", RuntimeClassicNarrativeOperationOwner.BundleBCombat, true, true, "bundle-b-spell-status-execution");

        // Bundle C owns inventory and currency transactions.
        foreach (var op in new[] { "takeitem", "giveitem", "clearitem", "givecoins" })
            yield return C(op, RuntimeClassicNarrativeOperationOwner.BundleCItemsEconomy, true, true, "bundle-c-atomic-item-currency-transaction");

        // Bundle D owns world position and room-ground state.
        yield return C("teleport", RuntimeClassicNarrativeOperationOwner.BundleDRoomWorld, true, true, "bundle-d-teleport-request");

        // Bundle E owns NPC/spawn/action production.
        foreach (var op in new[] { "summon", "remoteaction" })
            yield return C(op, RuntimeClassicNarrativeOperationOwner.BundleENpcSpawn, true, true, "bundle-e-npc-spawn-action-request");
    }
}

public sealed record RuntimeBundleFPhaseF3OpcodeDispatchAuditReport(
    bool Passed,
    int DecodedOperationCount,
    int DecodedOpcodeCount,
    int ContractCount,
    int UnclassifiedOpcodeCount,
    int MutationOpcodeCount,
    int AtomicOpcodeCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF3OpcodeDispatchAudit
{
    public static RuntimeBundleFPhaseF3OpcodeDispatchAuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        var dispatcher = new RuntimeClassicNarrativeOperationDispatcher();
        var operations = new List<RuntimeClassicTextblockOperation>();
        foreach (var id in Enumerable.Range(1, 20000))
            if (graph.TryGet(id, out var node)) operations.AddRange(node.Operations);
        var opcodes = operations.Select(x => x.Opcode).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var unclassified = new List<string>();
        foreach (var opcode in opcodes)
        {
            try { _ = dispatcher.RequireContract(opcode); checks.Add("opcode-owned:" + opcode); }
            catch (InvalidOperationException) { unclassified.Add(opcode); failures.Add("opcode-unclassified:" + opcode); }
        }
        Check(dispatcher.Count == opcodes.Length, "contract-count-matches-decoded-vocabulary");
        Check(dispatcher.All.Where(x => x.MutatesState).All(x => x.RequiresAtomicBoundary), "all-mutations-require-transaction-boundary");
        Check(dispatcher.RequireContract("takeitem").Owner == RuntimeClassicNarrativeOperationOwner.BundleCItemsEconomy, "item-turnin-owned-by-bundle-c");
        Check(dispatcher.RequireContract("giveability").Owner == RuntimeClassicNarrativeOperationOwner.BundleACharacter, "ability-reward-owned-by-bundle-a");
        Check(dispatcher.RequireContract("cast").Owner == RuntimeClassicNarrativeOperationOwner.BundleBCombat, "spell-status-owned-by-bundle-b");
        Check(dispatcher.RequireContract("teleport").Owner == RuntimeClassicNarrativeOperationOwner.BundleDRoomWorld, "teleport-owned-by-bundle-d");
        Check(dispatcher.RequireContract("summon").Owner == RuntimeClassicNarrativeOperationOwner.BundleENpcSpawn, "summon-owned-by-bundle-e");
        Check(dispatcher.RequireContract("message").Owner == RuntimeClassicNarrativeOperationOwner.BundleFPresentation, "message-owned-by-bundle-f-presentation");
        return new(failures.Count == 0, operations.Count, opcodes.Length, dispatcher.Count, unclassified.Count,
            dispatcher.All.Count(x => x.MutatesState), dispatcher.All.Count(x => x.RequiresAtomicBoundary), checks, failures);
    }
}
