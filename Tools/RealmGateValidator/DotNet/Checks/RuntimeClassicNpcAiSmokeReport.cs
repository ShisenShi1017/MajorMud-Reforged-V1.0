using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicNpcAiSmokeReport(bool Passed, IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicNpcAiSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        void Check(string name, bool value) => checks.Add(name + ":" + value);

        var nativeAudit = RuntimeClassicNpcAiNativeClosureAudit.Run(projectRoot);
        Check("native-npc-corpus-audit", nativeAudit.Passed);
        Check("classic-npc-count-1100", nativeAudit.NpcCount == 1100);
        Check("monsguards-owner-count-112", nativeAudit.MonsGuardsOwnerCount == 112);

        var protection = RuntimeNpcProtectionAdmissionSmokeReport.Run();
        Check("monsguards-spell-target-smoke", protection.Passed);

        var cadence = RuntimeClassicNpcCombatCadenceAudit.Run();
        Check("npc-combat-cadence-audit", cadence.Status == "PASS");

        Check("native-room-monster-slots-15", RuntimeClassicNpcAiSemanticsAuthority.NativeRoomMonsterSlots == 15);
        Check("native-attack-storage-slots-5", RuntimeClassicNpcAiSemanticsAuthority.NativeAttackStorageSlots == 5);
        Check("native-medium-move-cap-3", RuntimeClassicNpcAiSemanticsAuthority.NativeMediumMovementAttemptCap == 3);
        Check("native-fast-follow-retry-limit-15", RuntimeClassicNpcAiSemanticsAuthority.NativeFastFollowRetryLimit == 15);
        Check("native-confusion-ability-71", RuntimeClassicNpcAiSemanticsAuthority.ConfusionAbilityId == 71);
        Check("native-confusion-message-ability-101", RuntimeClassicNpcAiSemanticsAuthority.ConfusionMessageAbilityId == 101);
        Check("native-monsguards-ability-146", RuntimeClassicNpcAiSemanticsAuthority.MonsGuardsAbilityId == 146);

        Check("monster-type-0-holds", RuntimeClassicNpcAiSemanticsAuthority.MonsterTypeHoldsWithoutRoute(0));
        Check("monster-type-1-probabilistic", RuntimeClassicNpcAiSemanticsAuthority.MonsterTypeUsesProbabilisticRoam(1));
        Check("monster-type-2-holds", RuntimeClassicNpcAiSemanticsAuthority.MonsterTypeHoldsWithoutRoute(2));
        Check("monster-type-3-probabilistic", RuntimeClassicNpcAiSemanticsAuthority.MonsterTypeUsesProbabilisticRoam(3));
        Check("monster-type-4-probabilistic", RuntimeClassicNpcAiSemanticsAuthority.MonsterTypeUsesProbabilisticRoam(4));
        Check("monster-type-5-unconditional", RuntimeClassicNpcAiSemanticsAuthority.MonsterTypeUsesUnconditionalRoam(5));
        Check("monster-type-over-5-probabilistic", RuntimeClassicNpcAiSemanticsAuthority.MonsterTypeUsesProbabilisticRoam(6));
        Check("type-4-not-hold", !RuntimeClassicNpcAiSemanticsAuthority.MonsterTypeHoldsWithoutRoute(4));

        Check("follow-zero-roam-threshold-50", RuntimeClassicNpcAiSemanticsAuthority.ResolveRandomRoamThreshold(0) == 50);
        Check("follow-100-roam-threshold-0", RuntimeClassicNpcAiSemanticsAuthority.ResolveRandomRoamThreshold(100) == 0);
        Check("confusion-roll-below-fumbles", RuntimeClassicNpcAiSemanticsAuthority.ConfusionFumbles(25, 24));
        Check("confusion-roll-equal-does-not-fumble", !RuntimeClassicNpcAiSemanticsAuthority.ConfusionFumbles(25, 25));
        Check("follow-retry-15-retained", !RuntimeClassicNpcAiSemanticsAuthority.FastFollowShouldAbandonAfterFailure(15));
        Check("follow-retry-16-abandons", RuntimeClassicNpcAiSemanticsAuthority.FastFollowShouldAbandonAfterFailure(16));

        Check("generic-threat-not-native", !RuntimeClassicNpcAiSemanticsAuthority.GenericThreatLedgerIsNativeClassic);
        Check("generic-perception-not-native", !RuntimeClassicNpcAiSemanticsAuthority.GenericPerceptionRadiusIsNativeClassic);
        Check("generic-low-hp-flee-not-native", !RuntimeClassicNpcAiSemanticsAuthority.GenericLowHpFleePolicyIsNativeClassic);
        Check("generic-patrol-not-native", !RuntimeClassicNpcAiSemanticsAuthority.GenericPatrolPathIsNativeClassic);
        Check("monsguards-native-owner-resolved", RuntimeClassicNpcAiSemanticsAuthority.MonsGuardsSpellTargetSubstitutionResolved);
        Check("native-update-cadence-resolved", RuntimeClassicNpcAiSemanticsAuthority.ExactNativeUpdateCadencePriorityResolved);
        Check("native-tracking-resolved", RuntimeClassicNpcAiSemanticsAuthority.ExactTrackingSemanticsResolved);

        var passed = checks.All(check => check.EndsWith(":True", StringComparison.OrdinalIgnoreCase));
        return new RuntimeClassicNpcAiSmokeReport(passed, checks);
    }
}
