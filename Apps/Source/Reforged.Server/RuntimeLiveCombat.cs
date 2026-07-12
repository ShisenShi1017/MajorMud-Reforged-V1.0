using System.Collections.Concurrent;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeLiveCombatEncounter(
    string SessionId,
    string WorldId,
    string RoomId,
    string SpawnId,
    string NpcId,
    string NpcName,
    int NpcLevel,
    int NpcMaximumHitPoints,
    IReadOnlyList<RuntimeNpcAttackDefinition> NpcAttacks,
    int NpcCurrentHitPoints,
    int RoundNumber,
    DateTimeOffset StartedAtUtc);

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly ConcurrentDictionary<string, RuntimeLiveCombatEncounter> _liveCombats = new(StringComparer.OrdinalIgnoreCase);

    public bool CanMove(RuntimeSession session, out string message)
    {
        if (_liveCombats.TryGetValue(session.Id, out var encounter))
        {
            message = $"You are fighting {encounter.NpcName}. Flee before moving.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    public string ExecuteLiveCombatCommand(RuntimeSession session, RuntimeCommand command)
    {
        return command.Name switch
        {
            "attack" or "kill" => BeginLiveCombat(session, command.ArgumentText),
            "flee" => FleeLiveCombat(session),
            "combat" => RenderLiveCombatStatus(session),
            "round" => ExecuteLiveCombatRound(session),
            "assist" => "Assist is not executable until generated party-combat policy is attached.",
            _ => _policyGateway.Execute(session, command)
        };
    }

    public string RenderLiveCombatStatus(RuntimeSession session)
    {
        if (!_liveCombats.TryGetValue(session.Id, out var encounter)) return "You are not in combat.";
        return string.Join(Environment.NewLine, new[]
        {
            $"Combat target: {encounter.NpcName}",
            $"NPC ID: {encounter.NpcId}",
            $"Spawn ID: {encounter.SpawnId}",
            $"Generated level: {encounter.NpcLevel}",
            $"NPC HP: {encounter.NpcCurrentHitPoints}/{encounter.NpcMaximumHitPoints}",
            $"Round: {encounter.RoundNumber}",
            $"Room: {encounter.RoomId}",
            $"Generated attack rows: {encounter.NpcAttacks.Count}",
            RenderPlayerCombatReadiness(session, encounter)
        });
    }

    private string RenderPlayerCombatReadiness(RuntimeSession session, RuntimeLiveCombatEncounter encounter)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "Player combat profile is unavailable for a non-character session.";
        var character = _characters.LoadCharacter(actor.Id);
        if (character is null) return "Player combat profile is unavailable because the character save could not be loaded.";
        var profile = new RuntimePlayerCombatProfileResolver(RuntimeItemCombatCatalog.Load(_characters.ProjectRoot)).Resolve(character);
        if (!profile.Ready) return "Player combat profile blocked: " + string.Join(", ", profile.Blockers);
        var npcDefense = RuntimeNpcDefensiveProfileResolver.Resolve(ResolveEncounterNpc(session, encounter));
        return $"Player combat profile ready: AV {profile.AttackValue}, DV {profile.DefenseValue}, AC {profile.ArmorClass}, DR {profile.DamageResistance}, Dodge {profile.DodgeBonus}, Damage {profile.MinimumDamage}-{profile.MaximumDamage}, Accuracy {profile.WeaponAccuracyModifier}. NPC defense ready: AC {npcDefense.ArmorClass}, DR {npcDefense.DamageResistance}; no separate NPC dodge input is defined by the authoritative schema.";
    }


    private static RuntimeNpcDefinition ResolveEncounterNpc(RuntimeSession session, RuntimeLiveCombatEncounter encounter)
    {
        foreach (var spawn in session.World.FindSpawnsByRoom(encounter.RoomId))
        {
            if (!string.Equals(spawn.Id, encounter.SpawnId, StringComparison.OrdinalIgnoreCase)) continue;
            if (spawn.BossDefinition is not null && string.Equals(spawn.BossDefinition.Id, encounter.NpcId, StringComparison.OrdinalIgnoreCase))
                return spawn.BossDefinition;
            var npc = spawn.GroupDefinitions.FirstOrDefault(x => string.Equals(x.Id, encounter.NpcId, StringComparison.OrdinalIgnoreCase));
            if (npc is not null) return npc;
        }
        throw new InvalidOperationException($"LIVE_COMBAT_NPC_PROFILE_NOT_FOUND:{encounter.NpcId}");
    }

    private string BeginLiveCombat(RuntimeSession session, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Attack what?";
        if (_liveCombats.TryGetValue(session.Id, out var current))
            return $"You are already fighting {current.NpcName}.";

        var target = ResolveCombatTarget(session, query.Trim());
        var encounter = new RuntimeLiveCombatEncounter(
            session.Id,
            session.World.Definition.Id,
            session.CurrentRoom.Id,
            target.Spawn.Id,
            target.Npc.Id,
            target.Npc.Name,
            target.Npc.Level,
            target.Npc.HitPoints,
            target.Npc.Attacks,
            target.Npc.HitPoints,
            0,
            DateTimeOffset.UtcNow);
        if (!_liveCombats.TryAdd(session.Id, encounter)) throw new InvalidOperationException("LIVE_COMBAT_DUPLICATE_SESSION");
        _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} attacks {target.Npc.Name}.");
        return $"You engage {target.Npc.Name}. Use 'combat' for status or 'flee' to disengage.";
    }

    private string FleeLiveCombat(RuntimeSession session)
    {
        if (!_liveCombats.TryRemove(session.Id, out var encounter)) return "You are not in combat.";
        _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} flees from {encounter.NpcName}.");
        return $"You flee from {encounter.NpcName}.";
    }

    private static CombatTarget ResolveCombatTarget(RuntimeSession session, string query)
    {
        var matches = new List<CombatTarget>();
        foreach (var spawn in session.World.FindSpawnsByRoom(session.CurrentRoom.Id))
        {
            if (spawn.BossDefinition is not null && Matches(query, spawn.Id, spawn.BossDefinition))
                matches.Add(new CombatTarget(spawn, spawn.BossDefinition));
            foreach (var npc in spawn.GroupDefinitions)
                if (Matches(query, spawn.Id, npc)) matches.Add(new CombatTarget(spawn, npc));
        }
        matches = matches.GroupBy(x => $"{x.Spawn.Id}|{x.Npc.Id}", StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        if (matches.Count == 0) throw new InvalidOperationException($"No generated NPC here uniquely matches '{query}'.");
        if (matches.Count > 1) throw new InvalidOperationException($"More than one generated NPC here matches '{query}'; use an NPC or spawn ID.");
        return matches[0];
    }

    private static bool Matches(string query, string spawnId, RuntimeNpcDefinition npc) =>
        spawnId.Equals(query, StringComparison.OrdinalIgnoreCase)
        || npc.Id.Equals(query, StringComparison.OrdinalIgnoreCase)
        || npc.Name.Equals(query, StringComparison.OrdinalIgnoreCase)
        || npc.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

    private sealed record CombatTarget(RuntimeNpcSpawn Spawn, RuntimeNpcDefinition Npc);
}

public sealed class RuntimeLiveCombatSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeLiveCombatSmokeReport Run(string projectRoot, string worldId)
    {
        var report = new RuntimeLiveCombatSmokeReport();
        try
        {
            var resolver = ServerDataRootResolver.ResolveDefault(projectRoot);
            var npcPath = Path.Combine(resolver.DataRoot, "npcs.json");
            using var document = JsonDocument.Parse(File.ReadAllText(npcPath));
            var records = document.RootElement.GetProperty("records");
            Require(records.GetArrayLength() > 0, "generated-npc-catalog"); report.Checks.Add("generated-npc-catalog");
            Require(records.EnumerateArray().Any(x => x.TryGetProperty("combat", out var c) && c.TryGetProperty("hp", out _)), "generated-combat-profile"); report.Checks.Add("generated-combat-profile");
            Require(RuntimeCommand.Parse("attack rat").Name == "attack", "attack-route"); report.Checks.Add("attack-route");
            Require(RuntimeCommand.Parse("combat").Name == "combat", "combat-status-route"); report.Checks.Add("combat-status-route");
            Require(RuntimeCommand.Parse("flee").Name == "flee", "flee-route"); report.Checks.Add("flee-route");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.ExecuteLiveCombatCommand)) is not null, "live-combat-admission"); report.Checks.Add("live-combat-admission");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.CanMove)) is not null, "combat-movement-guard"); report.Checks.Add("combat-movement-guard");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
