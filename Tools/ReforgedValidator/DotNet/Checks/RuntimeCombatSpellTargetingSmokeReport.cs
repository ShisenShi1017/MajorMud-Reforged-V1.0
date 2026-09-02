namespace Reforged.Server;

public static class RuntimeCombatSpellTargetingSmokeReport
{
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var policies = new[]
        {
            new RuntimeSpellTargetPolicy("self", "spell.9001", RuntimeSpellTargetMode.Self,
                new HashSet<RuntimeSpellTargetKind>{RuntimeSpellTargetKind.Character}, 1, true, true, true, true, true, false,
                new[]{"fixture:self"}, RuntimeResolutionConfidence.Proven, "reject"),
            new RuntimeSpellTargetPolicy("party", "spell.9002", RuntimeSpellTargetMode.Party,
                new HashSet<RuntimeSpellTargetKind>{RuntimeSpellTargetKind.Character}, 2, true, true, true, true, true, false,
                new[]{"fixture:party"}, RuntimeResolutionConfidence.Proven, "reject"),
            new RuntimeSpellTargetPolicy("unresolved", "spell.1304", null,
                new HashSet<RuntimeSpellTargetKind>(), null, false, true, true, true, false, false,
                new[]{"Librarium:spell.1304:Target"}, RuntimeResolutionConfidence.StronglyCorrelated, "fail_closed")
        };
        var runtime = new RuntimeSpellTargeting(policies);
        var candidates = new[]
        {
            new RuntimeSpellTargetCandidate("character.1", RuntimeSpellTargetKind.Character, "room.10", true, true, true, "party.7"),
            new RuntimeSpellTargetCandidate("character.2", RuntimeSpellTargetKind.Character, "room.10", true, true, true, "party.7"),
            new RuntimeSpellTargetCandidate("character.3", RuntimeSpellTargetKind.Character, "room.10", true, true, true, "party.7")
        };
        var self = runtime.Resolve(new RuntimeSpellTargetRequest("cast.1", "character.1", "spell.9001", "room.10", "party.7", null, candidates));
        var party = runtime.Resolve(new RuntimeSpellTargetRequest("cast.2", "character.1", "spell.9002", "room.10", "party.7", null, candidates));
        return new Dictionary<string, bool>
        {
            ["self_target"] = self.TargetIds.SequenceEqual(new[]{"character.1"}),
            ["deterministic_party_order_and_cap"] = party.TargetIds.SequenceEqual(new[]{"character.1", "character.2"}),
            ["duplicate_suppression"] = Throws("SPELL_TARGET_DUPLICATE_CANDIDATE", () => runtime.Resolve(new RuntimeSpellTargetRequest("cast.3", "character.1", "spell.9001", "room.10", "party.7", null, new[]{candidates[0], candidates[0]}))),
            ["canonical_room_validation"] = Throws("SPELL_TARGET_CANONICAL_ROOM_ID_REQUIRED", () => runtime.Resolve(new RuntimeSpellTargetRequest("cast.4", "character.1", "spell.9001", "10", "party.7", null, candidates))),
            ["unresolved_target_mode_fails_closed"] = Throws("SPELL_TARGET_MODE_UNRESOLVED", () => runtime.Resolve(new RuntimeSpellTargetRequest("cast.5", "character.1", "spell.1304", "room.10", "party.7", null, candidates)))
        };
    }

    private static bool Throws(string message, Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == message; }
    }
}
