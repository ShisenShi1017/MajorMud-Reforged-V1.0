using System.Text.Json;
namespace Reforged.Server;
public enum RuntimeRuleValueType { Boolean, Integer, Decimal, String, Duration }
public enum RuntimeRuleMutability { StartupOnly, RuntimeMutable, RuntimeMutableWithWorldReset }
public sealed record RuntimeWorldRuleDefinition(string Id,RuntimeRuleValueType ValueType,string DefaultValue,string? Minimum,string? Maximum,RuntimeRuleMutability Mutability,IReadOnlySet<RuntimeWorldMode> AllowedModes,bool Persist,bool RequiresAdministrator);
public sealed record RuntimeWorldRuleChange(string RuleId,string PreviousValue,string NewValue,string ActorId,DateTimeOffset ChangedUtc,long Revision,bool RequiresWorldReset);
public sealed record RuntimeWorldRuleResult(bool Accepted,string Code,string? Value,RuntimeWorldRuleChange? Change);
/// <summary>
/// Reforged-owned typed world-rule storage. The native MajorMUD evidence source is WCCMMUD.INI,
/// but Reforged intentionally does not create or parse that legacy file. Proven Classic defaults
/// are projected through RuntimeClassicConfigurationAuthority; unknown native ranges/mutability
/// remain evidence-gated instead of being inferred from this generic Reforged service.
/// </summary>
public sealed class RuntimeWorldRulesConfigurationService
{
 private const int SchemaVersion=1; private readonly RuntimeWorldMode _mode; private readonly Dictionary<string,RuntimeWorldRuleDefinition> _definitions; private readonly Dictionary<string,string> _values; private readonly string? _path; private long _revision;
 public RuntimeWorldRulesConfigurationService(RuntimeWorldMode mode,IEnumerable<RuntimeWorldRuleDefinition> definitions,string? persistencePath=null)
 { _mode=mode;_path=persistencePath;_definitions=new(StringComparer.OrdinalIgnoreCase);_values=new(StringComparer.OrdinalIgnoreCase);foreach(var d in definitions){ValidateDefinition(d);if(!_definitions.TryAdd(d.Id,d))throw new ArgumentException("duplicate-world-rule");_values[d.Id]=d.DefaultValue;}Load(); }
 public RuntimeWorldMode Mode=>_mode; public long Revision=>_revision;
 public RuntimeWorldRuleResult Get(string id)=>_values.TryGetValue(id,out var v)?new(true,"ok",v,null):new(false,"unknown-rule",null,null);
 public RuntimeWorldRuleResult Set(string id,string value,string actorId,bool actorIsAdministrator,bool worldRunning,DateTimeOffset now)
 { if(!_definitions.TryGetValue(id,out var d))return Reject("unknown-rule");if(d.RequiresAdministrator&&!actorIsAdministrator)return Reject("administrator-required");if(!d.AllowedModes.Contains(_mode))return Reject("rule-not-allowed-in-world-mode");if(worldRunning&&d.Mutability==RuntimeRuleMutability.StartupOnly)return Reject("restart-required");if(!TryNormalize(d,value,out var normalized))return Reject("invalid-rule-value");var previous=_values[id];if(previous==normalized)return new(true,"unchanged",normalized,null);_values[id]=normalized;_revision++;var c=new RuntimeWorldRuleChange(id,previous,normalized,actorId,now,_revision,d.Mutability==RuntimeRuleMutability.RuntimeMutableWithWorldReset);Save();return new(true,c.RequiresWorldReset?"changed-world-reset-required":"changed",normalized,c); }
 public IReadOnlyList<string> ValidateState(){var e=new List<string>();foreach(var d in _definitions.Values){if(!_values.TryGetValue(d.Id,out var v)||!TryNormalize(d,v,out _))e.Add("invalid-value:"+d.Id);if(!d.AllowedModes.Contains(_mode)&&_values[d.Id]!=d.DefaultValue)e.Add("mode-leak:"+d.Id);}return e;}
 public IReadOnlyDictionary<string,string> Snapshot()=>new Dictionary<string,string>(_values,StringComparer.OrdinalIgnoreCase);
 private static void ValidateDefinition(RuntimeWorldRuleDefinition d){if(string.IsNullOrWhiteSpace(d.Id)||d.AllowedModes.Count==0)throw new ArgumentException("invalid-world-rule-definition");if(!TryNormalize(d,d.DefaultValue,out _))throw new ArgumentException("invalid-world-rule-default");}
 private static bool TryNormalize(RuntimeWorldRuleDefinition d,string input,out string value){value=input.Trim();try{switch(d.ValueType){case RuntimeRuleValueType.Boolean:if(!bool.TryParse(value,out var b))return false;value=b?"true":"false";break;case RuntimeRuleValueType.Integer:if(!long.TryParse(value,out var i))return false;if(d.Minimum is not null&&i<long.Parse(d.Minimum))return false;if(d.Maximum is not null&&i>long.Parse(d.Maximum))return false;value=i.ToString();break;case RuntimeRuleValueType.Decimal:if(!decimal.TryParse(value,out var n))return false;if(d.Minimum is not null&&n<decimal.Parse(d.Minimum))return false;if(d.Maximum is not null&&n>decimal.Parse(d.Maximum))return false;value=n.ToString(System.Globalization.CultureInfo.InvariantCulture);break;case RuntimeRuleValueType.Duration:if(!TimeSpan.TryParse(value,out var t)||t<TimeSpan.Zero)return false;if(d.Minimum is not null&&t<TimeSpan.Parse(d.Minimum))return false;if(d.Maximum is not null&&t>TimeSpan.Parse(d.Maximum))return false;value=t.ToString("c");break;case RuntimeRuleValueType.String:if(d.Maximum is not null&&value.Length>int.Parse(d.Maximum))return false;break;}return true;}catch{return false;}}
 private void Save(){if(_path is null)return;var rows=_definitions.Values.Where(x=>x.Persist).ToDictionary(x=>x.Id,x=>_values[x.Id],StringComparer.OrdinalIgnoreCase);var dto=new SnapshotDto(SchemaVersion,_mode,_revision,rows);var tmp=_path+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(_path)??".");File.WriteAllText(tmp,JsonSerializer.Serialize(dto,new JsonSerializerOptions{WriteIndented=true}));File.Move(tmp,_path,true);}
 private void Load(){if(_path is null||!File.Exists(_path))return;try{var dto=JsonSerializer.Deserialize<SnapshotDto>(File.ReadAllText(_path));if(dto is null||dto.SchemaVersion!=SchemaVersion||dto.Mode!=_mode)return;foreach(var kv in dto.Values)if(_definitions.TryGetValue(kv.Key,out var d)&&d.Persist&&d.AllowedModes.Contains(_mode)&&TryNormalize(d,kv.Value,out var n))_values[kv.Key]=n;_revision=Math.Max(0,dto.Revision);}catch{}}
 private sealed record SnapshotDto(int SchemaVersion,RuntimeWorldMode Mode,long Revision,Dictionary<string,string> Values);
 private static RuntimeWorldRuleResult Reject(string code)=>new(false,code,null,null);
}
