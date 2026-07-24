using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicAppearanceInput(string Name, string Sex, string Race, int Strength, int Agility, int Intellect, int Willpower, int Health, int Charm, int HitPoints, int MaximumHitPoints, bool Hidden, bool Sneaking, bool Invisible, bool Disguised, bool Transformed);
public sealed record RuntimeClassicAppearanceViewer(bool SeeHidden, bool DetectInvisible, bool IsSelf);

public static class RuntimeClassicCharacterAppearance
{
    private static readonly int[] Thresholds = [30,40,50,60,70,80,90,100];
    private static readonly string[] Strength = ["puny","weak","slightly built","moderately built","well built","muscular","powerfully built","heroically proportioned","physically Godlike"];
    private static readonly string[] Agility = ["slowly","clumsily","slugishly","cautiously","gracefully","very swiftly","with uncanny speed","with catlike agility","blindingly fast"];
    private static readonly string[] Health = ["sickly","frail","thin","healthy","stout","solid","massive","gigantic","colossal"];
    private static readonly string[] Charm = ["openly hostile and quite revolting","hostile and rather unappealing","quite unfriendly and aloof","likable in an unassuming sort of way","quite attractive and pleasant to be around","charismatic and outgoing.  You can't help but like %s","extremely likeable, and fairly radiates charisma","incredibly charismatic.  You are almost overpowered by %s strong personality","overwhelmingly charismatic.  You almost drop to your knees in wonder at the sight of %s!"];
    private static readonly string[] Intellect = ["utterly moronic","quite stupid","slightly dull","intelligent","bright","extremely clever","brilliant","a genius","all-knowing"];
    private static readonly string[] Willpower = ["seems selfish and hot-tempered","seems sullen and impulsive","seems a little naive","looks fairly knowledgeable","looks quite experienced and wise","has a worldly air about %s","seems to possess a wisdom beyond %s years","seems to be in an enlightened state of mind","looks like %s is one with the Gods"];
    private static readonly string[] Wounds = ["mortally wounded","critically wounded","severely wounded","heavily wounded","moderately wounded","slightly wounded","unwounded"];

    public static bool CanView(RuntimeClassicAppearanceViewer viewer, RuntimeClassicAppearanceInput target)
    {
        if (viewer.IsSelf) return true;
        if (target.Invisible && !viewer.DetectInvisible) return false;
        return ClassicVisibilityPolicy.CanDetect(viewer.SeeHidden, target.Hidden, target.Sneaking);
    }

    public static string Compose(RuntimeClassicAppearanceViewer viewer, RuntimeClassicAppearanceInput target)
    {
        if (!CanView(viewer,target)) throw new InvalidOperationException("The target is not visible to this viewer.");
        var shownName = target.Disguised ? "A disguised figure" : target.Transformed ? "A transformed figure" : target.Name;
        var pronoun = target.Sex.Equals("Female",StringComparison.OrdinalIgnoreCase) ? "She" : "He";
        var possessive = target.Sex.Equals("Female",StringComparison.OrdinalIgnoreCase) ? "her" : "his";
        string F(string phrase) => phrase.Replace("%s", pronoun.ToLowerInvariant(), StringComparison.Ordinal);
        var wound = Wound(target.HitPoints,target.MaximumHitPoints);
        return string.Join(Environment.NewLine,
            $"{shownName} is a {target.Race}, {F(Phrase(Intellect,target.Intellect))}.",
            $"{pronoun} moves {Phrase(Agility,target.Agility)}, and is {Phrase(Health,target.Health)}.",
            $"{pronoun} appears to be {Phrase(Strength,target.Strength)} and {F(Phrase(Charm,target.Charm))}.",
            $"{pronoun} {F(Phrase(Willpower,target.Willpower))}.",
            $"{pronoun} is {wound}.");
    }

    public static string PhraseFor(string stat, int value) => stat.Trim().ToLowerInvariant() switch
    {
        "strength" or "str" => Phrase(Strength,value), "agility" or "agi" => Phrase(Agility,value),
        "intellect" or "int" => Phrase(Intellect,value), "willpower" or "wil" => Phrase(Willpower,value),
        "health" or "hea" => Phrase(Health,value), "charm" or "cha" => Phrase(Charm,value),
        _ => throw new KeyNotFoundException(stat)
    };
    private static string Phrase(string[] table,int value){var i=0;while(i<Thresholds.Length && value>=Thresholds[i])i++;return table[Math.Min(i,table.Length-1)];}
    private static string Wound(int hp,int max){if(max<=0||hp>=max)return Wounds[6];var p=hp*100/max;return p<10?Wounds[0]:p<20?Wounds[1]:p<35?Wounds[2]:p<50?Wounds[3]:p<75?Wounds[4]:Wounds[5];}
}

public sealed record RuntimeBundleFPhaseF2ADynamicAuditReport(bool Passed,int ThresholdCount,int PhraseCount,int DisconnectCount,IReadOnlyList<string> Checks,IReadOnlyList<string> Failures){public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});}
public static class RuntimeBundleFPhaseF2ADynamicAudit
{
 public static RuntimeBundleFPhaseF2ADynamicAuditReport Run(string projectRoot,string worldId){var c=new List<string>();var f=new List<string>();void C(bool x,string n){if(x)c.Add(n);else f.Add(n);}
 foreach(var stat in new[]{"strength","agility","intellect","willpower","health","charm"}){C(RuntimeClassicCharacterAppearance.PhraseFor(stat,29).Length>0,stat+"-low");C(RuntimeClassicCharacterAppearance.PhraseFor(stat,100).Length>0,stat+"-high");}
 var src=File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","Reforged.Server","RuntimeLiveWorld.cs"));C(src.Contains("RuntimeClassicCharacterAppearance.Compose",StringComparison.Ordinal),"look-examine-live-consumer");C(src.Contains("RuntimeClassicCharacterAppearance.CanView",StringComparison.Ordinal),"visibility-before-composition");C(src.Contains("RequireRaceDescription",StringComparison.Ordinal),"static-race-description-remains-separate");
 var hidden=new RuntimeClassicAppearanceInput("Target","Male","Human",50,50,50,50,50,50,100,100,true,false,false,false,false);C(!RuntimeClassicCharacterAppearance.CanView(new(false,false,false),hidden),"hidden-filter");C(RuntimeClassicCharacterAppearance.CanView(new(true,false,false),hidden),"see-hidden-filter");
 C(RuntimeClassicCharacterAppearance.Compose(new(false,false,true),hidden).Contains("moderately built",StringComparison.Ordinal),"decoded-threshold-composition");return new(f.Count==0,8,54,f.Count,c,f);}
}
