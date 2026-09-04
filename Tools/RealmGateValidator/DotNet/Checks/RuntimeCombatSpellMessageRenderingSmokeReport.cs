namespace RealmGate.Server;
public static class RuntimeCombatSpellMessageRenderingSmokeReport
{
    public static IReadOnlyDictionary<string,bool> Run()
    {
        var evidence=new[]{"evidence.message.fixture"};
        var templates=new[]{
            new RuntimeCombatSpellMessageTemplate("messagetemplate.1",RuntimeCombatSpellMessageEvent.AttackHit,RuntimeCombatSpellMessageAudience.Attacker,new[]{"You hit {defender} for {damage}."},RuntimeMessageVariantSelection.First,false,RuntimeResolutionConfidence.Proven,evidence,"reject"),
            new RuntimeCombatSpellMessageTemplate("messagetemplate.2",RuntimeCombatSpellMessageEvent.AttackHit,RuntimeCombatSpellMessageAudience.Defender,new[]{"{attacker} hits you for {damage}."},RuntimeMessageVariantSelection.First,false,RuntimeResolutionConfidence.Proven,evidence,"reject")};
        var r=new RuntimeCombatSpellMessageRenderer(templates);
        var c=new RuntimeCombatSpellMessageContext("messageevent.1",RuntimeCombatSpellMessageEvent.AttackHit,7,"character.1","npc.2",null,null,null,null,"textblock.1","Ari","goblin","","they","it",12,null,null,null);
        var rendered=r.Render(c,new[]{RuntimeCombatSpellMessageAudience.Attacker,RuntimeCombatSpellMessageAudience.Defender});
        return new Dictionary<string,bool>{{"audience_views",rendered.Count==2},{"damage_token",rendered[0].Text.Contains("12")},{"textblock_link",rendered[0].TextblockId=="textblock.1"},{"evidence",rendered.All(x=>x.EvidenceReferences.Count>0)}};
    }
}
