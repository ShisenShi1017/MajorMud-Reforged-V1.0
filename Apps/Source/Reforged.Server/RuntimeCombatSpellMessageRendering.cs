namespace Reforged.Server;

public enum RuntimeCombatSpellMessageAudience { Attacker, Defender, Observer, Party, Room }
public enum RuntimeCombatSpellMessageEvent { AttackHit, AttackMiss, Dodge, Parry, Block, Critical, SpellStart, SpellComplete, Fizzle, Resistance, SavingThrow, StatusApplied, StatusRemoved, Death, Flee, Resurrection }
public enum RuntimeMessageVariantSelection { First, Seeded }

public sealed record RuntimeCombatSpellMessageTemplate(
    string TemplateId, RuntimeCombatSpellMessageEvent Event, RuntimeCombatSpellMessageAudience Audience,
    IReadOnlyList<string> Variants, RuntimeMessageVariantSelection VariantSelection,
    bool IncludeColorCodes, RuntimeResolutionConfidence Confidence,
    IReadOnlyList<string> EvidenceReferences, string SafeFallback);

public sealed record RuntimeCombatSpellMessageContext(
    string MessageEventId, RuntimeCombatSpellMessageEvent Event, long RandomSeed,
    string? AttackerId, string? DefenderId, string? ObserverId, string? SpellId,
    string? AbilityId, string? ItemId, string? TextblockId,
    string AttackerName, string DefenderName, string ObserverName,
    string AttackerSubjectPronoun, string DefenderObjectPronoun,
    int? Damage, int? Healing, string? StatusName, string? Reason);

public sealed record RuntimeRenderedCombatSpellMessage(
    string MessageEventId, string TemplateId, RuntimeCombatSpellMessageAudience Audience,
    int VariantIndex, string Text, string? TextblockId,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimeCombatSpellMessageRenderer
{
    private readonly IReadOnlyDictionary<(RuntimeCombatSpellMessageEvent, RuntimeCombatSpellMessageAudience), RuntimeCombatSpellMessageTemplate> _templates;
    private readonly HashSet<string> _renderedEvents = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatSpellMessageRenderer(IEnumerable<RuntimeCombatSpellMessageTemplate> templates)
    {
        var all=(templates??throw new ArgumentNullException(nameof(templates))).ToArray();
        foreach(var t in all) ValidateTemplate(t);
        _templates=all.ToDictionary(x=>(x.Event,x.Audience));
        if(_templates.Count!=all.Length) throw new InvalidOperationException("MESSAGE_TEMPLATE_DUPLICATE");
    }

    public IReadOnlyList<RuntimeRenderedCombatSpellMessage> Render(
        RuntimeCombatSpellMessageContext context, IReadOnlyList<RuntimeCombatSpellMessageAudience> audiences)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(audiences);
        RequireCanonical(context.MessageEventId,@"^messageevent\.\d+$","MESSAGE_EVENT_ID_INVALID");
        ValidateOptional(context.AttackerId,@"^(character|npc)\.\d+$","MESSAGE_ATTACKER_ID_INVALID");
        ValidateOptional(context.DefenderId,@"^(character|npc)\.\d+$","MESSAGE_DEFENDER_ID_INVALID");
        ValidateOptional(context.ObserverId,@"^(character|npc)\.\d+$","MESSAGE_OBSERVER_ID_INVALID");
        ValidateOptional(context.SpellId,@"^spell\.\d+$","MESSAGE_SPELL_ID_INVALID");
        ValidateOptional(context.AbilityId,@"^ability\.\d+$","MESSAGE_ABILITY_ID_INVALID");
        ValidateOptional(context.ItemId,@"^item\.\d+$","MESSAGE_ITEM_ID_INVALID");
        ValidateOptional(context.TextblockId,@"^textblock\.\d+$","MESSAGE_TEXTBLOCK_ID_INVALID");
        if(audiences.Count==0 || audiences.Distinct().Count()!=audiences.Count) throw new InvalidOperationException("MESSAGE_AUDIENCE_INVALID");
        if(!_renderedEvents.Add(context.MessageEventId)) throw new InvalidOperationException("MESSAGE_EVENT_DUPLICATE");
        try
        {
            var result=new List<RuntimeRenderedCombatSpellMessage>();
            foreach(var audience in audiences.OrderBy(x=>(int)x))
            {
                if(!_templates.TryGetValue((context.Event,audience),out var template)) throw new InvalidOperationException("MESSAGE_TEMPLATE_REQUIRED");
                if(template.Confidence==RuntimeResolutionConfidence.WeakHypothesis) throw new InvalidOperationException("MESSAGE_TEMPLATE_NOT_PROMOTED");
                var index=template.VariantSelection==RuntimeMessageVariantSelection.First?0:SelectVariant(context.RandomSeed,template.TemplateId,template.Variants.Count);
                var text=Expand(template.Variants[index],context);
                if(!template.IncludeColorCodes && text.Contains('|')) throw new InvalidOperationException("MESSAGE_COLOR_CODE_NOT_ALLOWED");
                result.Add(new RuntimeRenderedCombatSpellMessage(context.MessageEventId,template.TemplateId,audience,index,text,context.TextblockId,template.EvidenceReferences));
            }
            return result;
        }
        catch { _renderedEvents.Remove(context.MessageEventId); throw; }
    }

    private static int SelectVariant(long seed,string id,int count)
    {
        unchecked { ulong h=(ulong)seed; foreach(var c in id){h^=c;h*=1099511628211UL;} return (int)(h%(ulong)count); }
    }
    private static string Expand(string s,RuntimeCombatSpellMessageContext c)
    {
        var values=new Dictionary<string,string>(StringComparer.Ordinal){
            ["{attacker}"]=c.AttackerName,["{defender}"]=c.DefenderName,["{observer}"]=c.ObserverName,
            ["{attacker_subject}"]=c.AttackerSubjectPronoun,["{defender_object}"]=c.DefenderObjectPronoun,
            ["{damage}"]=c.Damage?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            ["{healing}"]=c.Healing?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            ["{status}"]=c.StatusName??string.Empty,["{reason}"]=c.Reason??string.Empty};
        foreach(var x in values) s=s.Replace(x.Key,x.Value,StringComparison.Ordinal);
        if(System.Text.RegularExpressions.Regex.IsMatch(s,@"\{[a-z_]+\}")) throw new InvalidOperationException("MESSAGE_TOKEN_UNRESOLVED");
        return s.Replace("\\n","\n",StringComparison.Ordinal);
    }
    private static void ValidateTemplate(RuntimeCombatSpellMessageTemplate t)
    {
        RequireCanonical(t.TemplateId,@"^messagetemplate\.\d+$","MESSAGE_TEMPLATE_ID_INVALID");
        if(t.Variants is null||t.Variants.Count==0||t.Variants.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("MESSAGE_VARIANTS_REQUIRED");
        if(t.EvidenceReferences.Count==0||!t.SafeFallback.Equals("reject",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("MESSAGE_TEMPLATE_INVALID");
    }
    private static void ValidateOptional(string? v,string p,string e){if(v is not null)RequireCanonical(v,p,e);}
    private static void RequireCanonical(string v,string p,string e){if(!System.Text.RegularExpressions.Regex.IsMatch(v??string.Empty,p))throw new InvalidOperationException(e);}
}
