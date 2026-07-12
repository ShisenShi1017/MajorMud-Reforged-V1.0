using System.Text.Json;

namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string ExecuteLiveCombatRound(RuntimeSession session)
    {
        if (!_liveCombats.TryGetValue(session.Id, out var encounter)) return "You are not in combat.";
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required for live combat.";

        var before = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found.");
        var player = new RuntimePlayerCombatProfileResolver(RuntimeItemCombatCatalog.Load(_characters.ProjectRoot)).Resolve(before);
        if (!player.Ready) return "Combat round blocked: " + string.Join(", ", player.Blockers);

        var npc = ResolveEncounterNpc(session, encounter);
        var npcDefense = RuntimeNpcDefensiveProfileResolver.Resolve(npc);
        var resolver = new ClassicCombatResolver();

        var playerStats = new ClassicCombatantStats(
            checked(player.AttackValue + player.WeaponAccuracyModifier),
            player.DefenseValue,
            player.ArmorClass,
            player.DamageResistance,
            player.DodgeBonus,
            0,
            player.CriticalChance);
        var npcStats = npcDefense.ToCombatantStats();
        var playerResult = resolver.Resolve(new ClassicAttackRequest(
            ClassicAttackType.NormalWeapon,
            playerStats,
            npcStats,
            player.MinimumDamage,
            player.MaximumDamage));

        var npcHp = Math.Max(0, encounter.NpcCurrentHitPoints - playerResult.FinalDamage);
        var messages = new List<string>
        {
            RenderAttack(actor.DisplayName, encounter.NpcName, playerResult)
        };

        var playerHp = before.HitPoints;
        if (npcHp > 0)
        {
            var attack = SelectNpcAttack(encounter.NpcAttacks);
            var attackType = ResolveAttackType(attack.Type);
            var npcAttacker = new ClassicCombatantStats(attack.Accuracy, 0, 0, 0, 0);
            var npcResult = resolver.Resolve(new ClassicAttackRequest(
                attackType,
                npcAttacker,
                playerStats,
                attack.MinimumDamage,
                attack.MaximumDamage));
            playerHp = Math.Max(0, playerHp - npcResult.FinalDamage);
            messages.Add(RenderAttack(encounter.NpcName, actor.DisplayName, npcResult));
        }

        try
        {
            if (npcHp == 0)
            {
                _liveCombats.TryRemove(session.Id, out _);
                messages.Add($"{encounter.NpcName} is defeated.");
                messages.Add(CompleteNpcDefeat(session, before, npc, playerHp));
            }
            else if (playerHp == 0)
            {
                _liveCombats.TryRemove(session.Id, out _);
                messages.Add($"{actor.DisplayName} is defeated.");
                messages.Add(CompletePlayerDefeat(session, before));
            }
            else
            {
                _characters.SaveCharacter(before with { HitPoints = playerHp, UpdatedAtUtc = DateTimeOffset.UtcNow });
                _liveCombats[session.Id] = encounter with
                {
                    NpcCurrentHitPoints = npcHp,
                    RoundNumber = checked(encounter.RoundNumber + 1)
                };
            }
        }
        catch
        {
            _characters.SaveCharacter(before);
            throw;
        }

        foreach (var message in messages) _sessions.BroadcastRoom(session, message, includeSource: false);
        messages.Add($"HP: {playerHp}/{before.MaximumHitPoints}; {encounter.NpcName}: {npcHp}/{encounter.NpcMaximumHitPoints}");
        return string.Join(Environment.NewLine, messages);
    }

    private static RuntimeNpcAttackDefinition SelectNpcAttack(IReadOnlyList<RuntimeNpcAttackDefinition> attacks)
    {
        var executable = attacks
            .Where(x => x.MinimumDamage >= 0 && x.MaximumDamage >= x.MinimumDamage && x.Accuracy > 0 && x.SelectionPercent > 0)
            .OrderBy(x => x.Slot)
            .ToArray();
        if (executable.Length == 0) throw new InvalidOperationException("NPC_COMBAT_NO_EXECUTABLE_ATTACK_ROWS");
        var total = executable.Sum(x => x.SelectionPercent);
        var roll = Random.Shared.Next(1, checked(total + 1));
        foreach (var attack in executable)
        {
            roll -= attack.SelectionPercent;
            if (roll <= 0) return attack;
        }
        return executable[^1];
    }

    private static ClassicAttackType ResolveAttackType(int generatedType)
    {
        if (!Enum.IsDefined(typeof(ClassicAttackType), generatedType))
            throw new InvalidOperationException($"NPC_COMBAT_ATTACK_TYPE_UNSUPPORTED:{generatedType}");
        return (ClassicAttackType)generatedType;
    }

    private static string RenderAttack(string attacker, string defender, ClassicAttackResult result) => result.Outcome switch
    {
        ClassicHitOutcome.Miss => $"{attacker} misses {defender}.",
        ClassicHitOutcome.Dodged => $"{defender} dodges {attacker}'s attack.",
        ClassicHitOutcome.Glance => $"{attacker}'s attack glances off {defender} for no damage.",
        ClassicHitOutcome.Critical => $"{attacker} critically hits {defender} for {result.FinalDamage} damage.",
        _ => $"{attacker} hits {defender} for {result.FinalDamage} damage."
    };
}

public sealed class RuntimeLiveCombatRoundSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeLiveCombatRoundSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeLiveCombatRoundSmokeReport();
        try
        {
            var itemCatalog = RuntimeItemCombatCatalog.Load(projectRoot);
            Require(itemCatalog.Profiles.Any(x => x.MaximumDamage > 0), "generated-player-weapon-profile"); report.Checks.Add("generated-player-weapon-profile");
            var root = ServerDataRootResolver.ResolveDefault(projectRoot).DataRoot;
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "npcs.json")));
            var npcs = document.RootElement.GetProperty("records").EnumerateArray().Select(RuntimeNpcDefinition.FromJson).ToArray();
            Require(npcs.Any(x => x.Attacks.Any(a => a.Accuracy > 0 && a.MaximumDamage >= a.MinimumDamage && a.SelectionPercent > 0)), "generated-npc-executable-attacks"); report.Checks.Add("generated-npc-executable-attacks");
            Require(RuntimeCommand.Parse("round").Name == "round", "round-command-route"); report.Checks.Add("round-command-route");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.ExecuteLiveCombatRound)) is not null, "live-round-execution"); report.Checks.Add("live-round-execution");
            var deterministic = new ClassicCombatResolver(new FixedClassicRandom());
            var result = deterministic.Resolve(new ClassicAttackRequest(ClassicAttackType.NormalWeapon, new(100,0,0,0,0), new(0,0,0,0,0), 3,3));
            Require(result.FinalDamage == 3, "classic-resolver-connected"); report.Checks.Add("classic-resolver-connected");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
    private sealed class FixedClassicRandom : IClassicRandom { public int NextInclusive(int minimum, int maximum) => minimum; }
}
