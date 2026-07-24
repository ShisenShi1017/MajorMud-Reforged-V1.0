using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeCombatDecodeBundleAuditResult(
    string Status,
    int FormulaRecords,
    int ExecutableCombatFormulaRecords,
    IReadOnlyList<string> VerifiedRuntimeBindings,
    IReadOnlyList<string> RemainingDecodedButNotMapped,
    IReadOnlyList<string> Conflicts);

public static class RuntimeCombatDecodeBundleAudit
{
    public static RuntimeCombatDecodeBundleAuditResult Run(string repositoryRoot)
    {
        var authority = RuntimeClassicFormulaAuthority.Load(repositoryRoot);
        var executable = authority.Records
            .Where(x => x.Subsystem.StartsWith("combat", StringComparison.OrdinalIgnoreCase) && x.Executable)
            .ToArray();
        foreach (var id in new[] { ClassicCombatResolver.HitFormulaId, ClassicCombatResolver.ClassCombatAccuracyFormulaId, ClassicCombatResolver.PhysicalDamageFormulaId, ClassicCombatResolver.StrengthDamageFormulaId, ClassicCombatResolver.PlayerAttackEnergyFormulaId,
            RuntimeClassicPhysicalFormulaCalculator.PlayerAttackSchedulingFormulaId, RuntimeClassicPhysicalFormulaCalculator.SpecialAttackTimingFormulaId, RuntimeClassicPhysicalFormulaCalculator.NaturalCriticalChanceFormulaId, RuntimeClassicPhysicalFormulaCalculator.FinalCriticalChanceFormulaId, RuntimeClassicPhysicalFormulaCalculator.CriticalResolutionOrderFormulaId, ClassicCombatResolver.QuickAndDeadlyFormulaId, ClassicCombatResolver.QuestCombatBonusFormulaId, ClassicCombatResolver.ItemCriticalBonusFormulaId, ClassicCombatResolver.CriticalFormulaId, ClassicCombatResolver.DodgeFormulaId, ClassicCombatResolver.DodgeBaseFormulaId, ClassicCombatResolver.DodgeAbilityFormulaId, ClassicCombatResolver.DodgeFinalFormulaId, ClassicCombatResolver.DodgeRollBoundaryFormulaId, ClassicCombatResolver.NpcAttackCountFormulaId, ClassicCombatResolver.NpcAttackEnergyFormulaId, ClassicCombatResolver.NpcAttackSelectionWeightFormulaId, ClassicCombatResolver.NpcResistedSpellEnergyRefundFormulaId, ClassicCombatResolver.NpcEncounterEnergyResetFormulaId, ClassicCombatResolver.NpcDecodedCurrencyAmountsFormulaId, ClassicCombatResolver.NpcHitSpellRelationshipFormulaId, ClassicCombatResolver.NpcHitSpellMetadataFormulaId, ClassicCombatResolver.NpcHitSpellClassificationFormulaId, ClassicCombatResolver.NpcHitSpellAttackElementFormulaId, ClassicCombatResolver.NpcHitSpellEffectRangeFormulaId, ClassicCombatResolver.NpcHitSpellTargetScopeFormulaId, ClassicCombatResolver.NpcHitSpellResistanceScopeFormulaId, ClassicCombatResolver.NpcHitSpellResistanceRollFormulaId, ClassicCombatResolver.NpcHitSpellTypeSemanticsFormulaId, ClassicCombatResolver.NpcHitSpellImmediateDamageFormulaId, ClassicCombatResolver.NpcHitSpellElementalResistanceFormulaId, ClassicCombatResolver.NpcHitSpellPoisonImmunityFormulaId, ClassicCombatResolver.NpcHitSpellStatusDeliveryFormulaId, ClassicCombatResolver.NpcHitSpellExpirationFormulaId, ClassicCombatResolver.NpcHitSpellCurePoisonFormulaId, ClassicCombatResolver.StatusLifecycleFormulaId, ClassicCombatResolver.StatusPeriodicFormulaId, ClassicCombatResolver.StatusModifierFormulaId, ClassicCombatResolver.StatusCureDispelFormulaId, ClassicCombatResolver.StatusDeathCleanupFormulaId, ClassicCombatResolver.BackstabDamageFormulaId, ClassicCombatResolver.BackstabPreparationFormulaId, ClassicCombatResolver.PhysicalAttackConcealmentBreakFormulaId, ClassicCombatResolver.IncapacitatedDodgeSuppressionFormulaId, ClassicCombatResolver.IncapacitatedAttackSuppressionFormulaId, ClassicCombatResolver.PhysicalSpecialCommandRoutingFormulaId, ClassicCombatResolver.PerfectStealthCommandFormulaId, ClassicCombatResolver.StealthVsPerceptionFormulaId, RuntimeClassicStealthResolver.ModifiedStealthFormulaId, RuntimeClassicStealthResolver.SeeHiddenFormulaId, RuntimeClassicStealthResolver.PercentileRollFormulaId, RuntimeClassicStealthResolver.CommandAdmissionFormulaId, RuntimeLiveGameplayFacade.NpcTargetThreatFormulaId, RuntimeLiveGameplayFacade.PlayerAssistFormulaId, RuntimeLiveGameplayFacade.CompanionAssistFormulaId, RuntimeLiveGameplayFacade.GuardAssistFormulaId, RuntimeLiveGameplayFacade.KillRewardAttributionFormulaId })
            authority.RequireExecutable(id);

        var ownershipSmoke = RuntimeLiveCombatOwnershipSmokeReport.Run();
        if (!ownershipSmoke.Passed)
            throw new InvalidOperationException("Live combat ownership integration failed: " + string.Join(", ", ownershipSmoke.Failures));

        var closureSmoke = RuntimeCombatBundleClosureSmokeReport.Run(repositoryRoot);
        if (!closureSmoke.Passed)
            throw new InvalidOperationException("Bundle B closure audit failed: " + string.Join(", ", closureSmoke.Failures));


        if (!RuntimeCommandDispatcher.RoutesToPolicy("smash") || !RuntimeCommandDispatcher.RoutesToPolicy("backstab"))
            throw new InvalidOperationException("Smash and Backstab must route through the main policy dispatcher.");
        if (RuntimeCommand.Parse("smash target").Name != "smash" || RuntimeCommand.Parse("backstab target").Name != "backstab" || RuntimeCommand.Parse("bs target").Name != "backstab")
            throw new InvalidOperationException("Smash, Backstab, or the bs alias failed command resolution.");
        if (!RuntimeCommandDispatcher.RoutesToPolicy("hide") || !RuntimeCommandDispatcher.RoutesToPolicy("sneak"))
            throw new InvalidOperationException("Hide and Sneak must route through the main policy dispatcher.");
        if (RuntimeCommand.Parse("hide").Name != "hide" || RuntimeCommand.Parse("hid").Name != "hide" || RuntimeCommand.Parse("sneak").Name != "sneak" || RuntimeCommand.Parse("sn").Name != "sneak")
            throw new InvalidOperationException("Hide, Sneak, or their aliases failed command resolution.");
        var stealthCharacter = new RuntimeCharacterRecord(
            "character.29", "account.29", "Perfect Stealth Audit", "Classic", "race.1", "Race", "class.1", "Class", 1, 0,
            10, 10, 0, 0, "room.1", 1, new Dictionary<string, int>(), new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(),
            new Dictionary<string, string?>(), new Dictionary<string, string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var hidden = RuntimeLiveGameplayFacade.ApplyPerfectStealth(stealthCharacter, "hide");
        if (!hidden.Conditions.TryGetValue("combat.hidden", out var hiddenValue) || hiddenValue != "1" || hidden.Conditions.GetValueOrDefault("stealth.source") != "ability.186")
            throw new InvalidOperationException("Perfect Stealth Hide failed to persist authoritative concealment.");
        var sneaking = RuntimeLiveGameplayFacade.ApplyPerfectStealth(stealthCharacter, "sneak");
        if (!sneaking.Conditions.TryGetValue("combat.sneaking", out var sneakValue) || sneakValue != "1" || sneaking.Conditions.GetValueOrDefault("stealth.source") != "ability.186")
            throw new InvalidOperationException("Perfect Stealth Sneak failed to persist authoritative movement stealth.");
        var weightedRows = new[]
        {
            new RuntimeNpcAttackDefinition(1, 1, 1, 2, 25, 50, 0, 0, 0, 0, 0),
            new RuntimeNpcAttackDefinition(2, 1, 1, 2, 75, 50, 0, 0, 0, 0, 0),
            new RuntimeNpcAttackDefinition(3, 1, 1, 2, 100, 50, 0, 50, 0, 0, 0)
        };
        var firstWeight = RuntimeNpcAttackSelector.Select(weightedRows, 0, 25);
        if (firstWeight.Attack?.Slot != 1 || firstWeight.TotalWeight != 100)
            throw new InvalidOperationException("NPC weighted attack selection failed the first inclusive boundary.");
        var secondWeight = RuntimeNpcAttackSelector.Select(weightedRows, 0, 26);
        if (secondWeight.Attack?.Slot != 2)
            throw new InvalidOperationException("NPC weighted attack selection failed the second inclusive boundary.");
        var energyFilteredWeight = RuntimeNpcAttackSelector.Select(weightedRows, 50, 200);
        if (energyFilteredWeight.Attack?.Slot != 3 || energyFilteredWeight.TotalWeight != 200)
            throw new InvalidOperationException("NPC attack weights were not re-normalized after the energy eligibility filter.");
        var classStealthFormula = RuntimeClassicStealthResolver.CalculateModifiedStealth(10, 80, 64, 60, true, false, 7);
        if (classStealthFormula != 85)
            throw new InvalidOperationException($"Modified Stealth formula mismatch: expected 85, got {classStealthFormula}.");
        var raceOnlyStealthFormula = RuntimeClassicStealthResolver.CalculateModifiedStealth(20, 80, 64, 60, false, true, 0);
        if (raceOnlyStealthFormula != 78)
            throw new InvalidOperationException($"Race-only Modified Stealth formula mismatch: expected 78, got {raceOnlyStealthFormula}.");
        var dualStealthFormula = RuntimeClassicStealthResolver.CalculateModifiedStealth(20, 80, 64, 60, true, true, 0);
        if (dualStealthFormula != 103)
            throw new InvalidOperationException($"Class-and-race Modified Stealth formula mismatch: expected 103, got {dualStealthFormula}.");
        var stealthSuccess = RuntimeClassicStealthResolver.ResolvePercentile(80, 30, 50);
        if (!stealthSuccess.Succeeded || stealthSuccess.SuccessChance != 50)
            throw new InvalidOperationException("Stealth percentile boundary failed at the inclusive success roll.");
        var stealthFailure = RuntimeClassicStealthResolver.ResolvePercentile(80, 30, 51);
        if (stealthFailure.Succeeded)
            throw new InvalidOperationException("Stealth percentile boundary failed immediately above the success chance.");
        var stealthCapped = RuntimeClassicStealthResolver.ResolvePercentile(180, 10, 100);
        if (!stealthCapped.Succeeded || stealthCapped.SuccessChance != 100)
            throw new InvalidOperationException("Stealth percentile chance must cap at 100 percent.");
        var failedVisibility = RuntimeLiveGameplayFacade.ClearStealthState(hidden, stealthFailure);
        if (failedVisibility.Conditions.GetValueOrDefault("combat.hidden") != "0" || failedVisibility.Conditions.GetValueOrDefault("stealth.last_result") != "failure-visible")
            throw new InvalidOperationException("Failed stealth must leave the character fully visible.");
        var pursuit = RuntimeLiveGameplayFacade.ApplySeeHiddenPursuit(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["disposition"] = "hostile" },
            stealthCharacter.CharacterId);
        if (pursuit.GetValueOrDefault("current_target_id") != stealthCharacter.CharacterId
            || pursuit.GetValueOrDefault("see_hidden_target_id") != stealthCharacter.CharacterId
            || pursuit.GetValueOrDefault("see_hidden_pursuit") != "1"
            || pursuit.GetValueOrDefault("see_hidden_pursuit_authority") != RuntimeClassicStealthResolver.SeeHiddenFormulaId)
            throw new InvalidOperationException("See Hidden detection must persist the detecting NPC's pursuit target.");

        var statusSmoke = RuntimeClassicLiveStatusEffectsSmokeReport.Run(repositoryRoot, "Classic");
        if (!statusSmoke.Passed)
            throw new InvalidOperationException("Classic live status smoke failed: " + string.Join("; ", statusSmoke.Failures));

        var npcCritical = authority.Records.SingleOrDefault(x => x.FormulaId.Equals("combat.npc_critical_bonus", StringComparison.OrdinalIgnoreCase));
        if (npcCritical is null || npcCritical.Executable || npcCritical.Grade != RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode)
            throw new InvalidOperationException("NPC critical bonuses must remain explicitly absent unless decoded NPC data authors one.");
        var classCritical = authority.Records.SingleOrDefault(x => x.FormulaId.Equals("combat.class_specific_critical_progression", StringComparison.OrdinalIgnoreCase));
        if (classCritical is null || classCritical.Executable || classCritical.Grade != RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode)
            throw new InvalidOperationException("Class-specific critical progression must remain explicitly absent; authored modifiers use the shared critical path.");
        var criticalImmunity = authority.Records.SingleOrDefault(x => x.FormulaId.Equals("combat.critical_immunity", StringComparison.OrdinalIgnoreCase));
        if (criticalImmunity is null || criticalImmunity.Executable || criticalImmunity.Grade != RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode)
            throw new InvalidOperationException("Critical immunity must remain explicitly absent from the exhausted Classic decode.");

        var shieldBlock = authority.Records.SingleOrDefault(x => x.FormulaId.Equals("combat.shield_block_roll", StringComparison.OrdinalIgnoreCase));
        if (shieldBlock is null || shieldBlock.Executable || shieldBlock.Grade != RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode)
            throw new InvalidOperationException("Shield block must remain explicitly classified as absent from the exhausted Classic decode, not executable or unresolved.");
        var parry = authority.Records.SingleOrDefault(x => x.FormulaId.Equals("combat.parry_roll", StringComparison.OrdinalIgnoreCase));
        if (parry is null || parry.Executable || parry.Grade != RuntimeFormulaEvidenceGrade.TrulyAbsentFromAvailableDecode)
            throw new InvalidOperationException("Parry must remain explicitly classified as absent from the exhausted Classic decode, not executable or unresolved.");

        var unresolved = authority.Records
            .Where(x => x.Subsystem.StartsWith("combat", StringComparison.OrdinalIgnoreCase) && !x.Executable &&
                x.Grade is RuntimeFormulaEvidenceGrade.DecodedButNotMapped or RuntimeFormulaEvidenceGrade.RelationshipUnresolved)
            .Select(x => x.FormulaId).OrderBy(x => x).ToArray();
        var conflicts = authority.Records.Where(x => x.Subsystem.StartsWith("combat", StringComparison.OrdinalIgnoreCase) && x.Grade == RuntimeFormulaEvidenceGrade.Conflicting)
            .Select(x => x.FormulaId).OrderBy(x => x).ToArray();
        if (unresolved.Length > 0 || conflicts.Length > 0)
            throw new InvalidOperationException("Bundle B formula authority is not exhausted: " + string.Join(", ", unresolved.Concat(conflicts)));
        return new("PASS", authority.Records.Count, executable.Length,
            executable.Select(x => $"{x.FormulaId}->{x.RuntimeOwner}").OrderBy(x => x).ToArray(), unresolved, conflicts);
    }

    public static string ToJson(RuntimeCombatDecodeBundleAuditResult result) => JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
}

// Consolidated from RuntimeCombatBundleClosure.cs during Bundle B runtime closure.
public sealed class RuntimeCombatBundleClosureSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeCombatBundleClosureSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeCombatBundleClosureSmokeReport();
        try
        {
            Require(RuntimeLiveGameplayFacade.ClassicCombatRoundMilliseconds == 6000, "six-second-round-authority");
            report.Checks.Add("six-second-round-authority");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.TickLiveCombatsAsync)) is not null, "automatic-round-scheduler");
            report.Checks.Add("automatic-round-scheduler");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod("PruneInactiveCombatOwnership", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) is not null, "logout-disconnect-combat-release");
            report.Checks.Add("logout-disconnect-combat-release");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod("FleeLiveCombat", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) is not null, "flee-combat-release");
            report.Checks.Add("flee-combat-release");

            var dataRoot = ServerDataRootResolver.ResolveDefault(projectRoot).DataRoot;
            var rooms = ReadRecords(Path.Combine(dataRoot, "rooms.json"));
            var roomIds = rooms.Select(record => JsonValueReader.ReadString(record, "id", string.Empty)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Require(roomIds.Contains("room.map1.288"), "spawn-room-map1-288-promoted");
            report.Checks.Add("spawn-room-map1-288-promoted");
            Require(roomIds.Contains("room.map1.2306"), "spawn-room-map1-2306-promoted");
            report.Checks.Add("spawn-room-map1-2306-promoted");

            var spawns = ReadRecords(Path.Combine(dataRoot, "spawns.json"));
            foreach (var id in new[] { "spawn.288.43", "spawn.2306.213" })
            {
                var spawn = spawns.Single(record => JsonValueReader.ReadString(record, "id", string.Empty).Equals(id, StringComparison.OrdinalIgnoreCase));
                var qualified = JsonValueReader.ReadString(spawn, "qualified_room_ref", string.Empty);
                Require(roomIds.Contains(qualified), "spawn-room-reference-resolved:" + id);
                report.Checks.Add("spawn-room-reference-resolved:" + id);
            }

            var authority = RuntimeClassicFormulaAuthority.Load(projectRoot);
            var unresolved = authority.Records.Where(record => record.Subsystem.StartsWith("combat", StringComparison.OrdinalIgnoreCase)
                && record.Grade is RuntimeFormulaEvidenceGrade.DecodedButNotMapped or RuntimeFormulaEvidenceGrade.RelationshipUnresolved or RuntimeFormulaEvidenceGrade.Conflicting).ToArray();
            Require(unresolved.Length == 0, "combat-formula-authority-exhausted:" + string.Join(',', unresolved.Select(record => record.FormulaId)));
            report.Checks.Add("combat-formula-authority-exhausted");
        }
        catch (Exception ex)
        {
            report.Failures.Add(ex.Message);
        }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    private static JsonElement[] ReadRecords(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("record_parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            return parts.EnumerateArray()
                .Where(part => part.ValueKind == JsonValueKind.String)
                .Select(part => part.GetString())
                .Where(relative => !string.IsNullOrWhiteSpace(relative))
                .SelectMany(relative =>
                {
                    var partPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, relative!.Replace('/', Path.DirectorySeparatorChar)));
                    return ReadRecords(partPath);
                })
                .ToArray();
        }

        return document.RootElement.GetProperty("records").EnumerateArray().Select(record => record.Clone()).ToArray();
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
