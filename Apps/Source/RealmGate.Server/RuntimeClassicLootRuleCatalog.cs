using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicLootRuleDefinition(
    string Id,
    string Name,
    string Status,
    string Confidence,
    string RuntimeMappingStatus,
    bool RuntimeAuthoritative,
    string QuarantineReason);

public sealed record RuntimeClassicLootRuleCatalogDiagnostic(string Code, string Message, bool Fatal);

/// <summary>
/// World-scoped authority for generated Data/&lt;world&gt;/loot_rules.json.
/// Every decoded loot-rule record is runtime authority. Consumers resolve the fields present
/// in the record and apply data-defined defaults only for optional absent values.
/// </summary>
public sealed class RuntimeClassicLootRuleCatalog
{
    public const string ClassicDropRuleId = "loot_rule.classic.kill_drop_percent_roll_0136";
    private readonly IReadOnlyDictionary<string, RuntimeClassicLootRuleDefinition> _byId;
    private readonly string _dropRollRange;
    private readonly string _dropSuccessCondition;
    private RuntimeClassicLootRuleCatalog(int declared, IReadOnlyDictionary<string, RuntimeClassicLootRuleDefinition> byId, IReadOnlyList<RuntimeClassicLootRuleCatalogDiagnostic> diagnostics, string dropRollRange, string dropSuccessCondition)
    { DeclaredRecordCount=declared; _byId=byId; Diagnostics=diagnostics; _dropRollRange=dropRollRange; _dropSuccessCondition=dropSuccessCondition; }
    public int DeclaredRecordCount { get; }
    public int DefinitionCount => _byId.Count;
    public int RuntimeAuthoritativeCount => _byId.Values.Count(x=>x.RuntimeAuthoritative);
    public int QuarantinedCount => DefinitionCount-RuntimeAuthoritativeCount;
    public IReadOnlyList<RuntimeClassicLootRuleCatalogDiagnostic> Diagnostics { get; }
    public bool Passed => Diagnostics.All(x=>!x.Fatal);
    public RuntimeClassicLootRuleDefinition? Find(string id)=>_byId.TryGetValue(id,out var value)?value:null;
    public bool ShouldDrop(int dropPercent, int roll)
    {
        if (!_dropRollRange.Equals("1..100 inclusive", StringComparison.Ordinal) ||
            !_dropSuccessCondition.Equals("roll <= drop_percent", StringComparison.Ordinal))
            throw new InvalidOperationException("Generated Classic loot percentage rule is unavailable or unsupported.");
        if (dropPercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(dropPercent));
        if (roll is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return roll <= dropPercent;
    }

    public static RuntimeClassicLootRuleCatalog Load(string path)
    {
        if(!File.Exists(path)) throw new FileNotFoundException("Generated loot-rule catalog was not found.",path);
        using var document=JsonDocument.Parse(File.ReadAllText(path));
        var root=document.RootElement; var diagnostics=new List<RuntimeClassicLootRuleCatalogDiagnostic>();
        var declared=root.TryGetProperty("record_count",out var rc)&&rc.TryGetInt32(out var parsed)?parsed:0;
        var definitions=new Dictionary<string,RuntimeClassicLootRuleDefinition>(StringComparer.OrdinalIgnoreCase);
        if(!root.TryGetProperty("records",out var records)||records.ValueKind!=JsonValueKind.Array)
            diagnostics.Add(new("records-missing","loot_rules.json does not contain a records array.",true));
        var dropRollRange=string.Empty; var dropSuccessCondition=string.Empty;
        if(records.ValueKind==JsonValueKind.Array) foreach(var value in records.EnumerateArray())
        {
            var id=JsonValueReader.ReadString(value,"id",string.Empty).Trim();
            if(string.IsNullOrWhiteSpace(id)){diagnostics.Add(new("definition-missing-id","A loot-rule record has no id.",true));continue;}
            if(definitions.ContainsKey(id)){diagnostics.Add(new("definition-duplicate-id",$"Duplicate loot-rule id '{id}'.",true));continue;}
            var status=JsonValueReader.ReadString(value,"status",string.Empty);
            var confidence=JsonValueReader.ReadString(value,"confidence",string.Empty);
            var mapping=value.TryGetProperty("runtime_mapping",out var rm)&&rm.ValueKind==JsonValueKind.Object?JsonValueReader.ReadString(rm,"status",string.Empty):string.Empty;
            var authoritative=true;
            var reason=string.Empty;
            definitions[id]=new(id,JsonValueReader.ReadString(value,"name",id),status,confidence,mapping,authoritative,reason);
            if(id.Equals(ClassicDropRuleId,StringComparison.OrdinalIgnoreCase) && value.TryGetProperty("algorithm",out var algorithm) && algorithm.ValueKind==JsonValueKind.Object)
            {
                dropRollRange=JsonValueReader.ReadString(algorithm,"roll_range",string.Empty);
                dropSuccessCondition=JsonValueReader.ReadString(algorithm,"success_condition",string.Empty);
            }
        }
        if(declared!=definitions.Count) diagnostics.Add(new("declared-count-mismatch",$"Declared record_count={declared}, loaded definitions={definitions.Count}.",true));
        if(string.IsNullOrWhiteSpace(dropRollRange)||string.IsNullOrWhiteSpace(dropSuccessCondition)) diagnostics.Add(new("classic-drop-rule-missing","Verified Classic drop roll algorithm is missing.",true));
        return new(declared,definitions,diagnostics,dropRollRange,dropSuccessCondition);
    }
}
