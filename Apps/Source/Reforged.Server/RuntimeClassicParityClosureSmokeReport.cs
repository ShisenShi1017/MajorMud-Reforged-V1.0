using System.Collections.Generic;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicParityClosureSmokeReport(bool Passed, IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicParityClosureSmokeReport Run(string projectRoot)
    {
        var identityLaw = RuntimeIdentityLawParitySmokeReport.Run();
        var evilReputation = RuntimeEvilReputationParitySmokeReport.Run();
        var extensionBoundary = RuntimeClassicExtensionBoundarySmokeReport.Run();
        var deathExperience = RuntimeClassicDeathExperienceSmokeReport.Run(projectRoot);
        var rerollWarning = RuntimeRerollWarningSmokeReport.Run();
        var communicationRendering = RuntimeClassicCommunicationRenderingSmokeReport.Run();
        var dataReconstruction = RuntimeClassicDataReconstructionSmokeReport.Run(projectRoot);
        var hostPersistenceWorldgroup = RuntimeClassicHostPersistenceWorldgroupSmokeReport.Run(projectRoot);
        var importExportLimitsRecovery = RuntimeClassicImportExportLimitsRecoverySmokeReport.Run();
        var itemsInventory = RuntimeClassicItemsInventorySmokeReport.Run(projectRoot);
        var npcAi = RuntimeClassicNpcAiSmokeReport.Run(projectRoot);
        var questsScriptsTriggers = RuntimeClassicQuestScriptTriggerClosureSmokeReport.Run(projectRoot);
        var characterStatus = RuntimeClassicCharacterStatusClosureSmokeReport.Run();
        var roomsWorld = RuntimeClassicRoomsWorldClosureSmokeReport.Run(projectRoot);
        var timersResetsWorldRules = RuntimeClassicTimersResetsWorldRulesClosureSmokeReport.Run(projectRoot);
        var finalStrongClosure = RuntimeClassicFinalStrongClosureSmokeReport.Run(projectRoot);
        var breakAuthorityFailures = RuntimeClassicBreakAutocombatAuthority.Validate();
        var checks = new List<string>
        {
            "identity-law-parity:" + identityLaw.Passed,
            "evil-reputation-parity:" + evilReputation.Passed,
            "classic-extension-boundary:" + extensionBoundary.Passed,
            "classic-death-experience:" + deathExperience.Passed,
            "classic-reroll-warning:" + rerollWarning.Passed,
            "classic-communication-rendering:" + communicationRendering.Passed,
            "classic-data-reconstruction:" + dataReconstruction.Passed,
            "classic-host-persistence-worldgroup:" + hostPersistenceWorldgroup.Passed,
            "classic-import-export-limits-recovery:" + importExportLimitsRecovery.Passed,
            "classic-items-inventory:" + itemsInventory.Passed,
            "classic-npc-ai:" + npcAi.Passed,
            "classic-quests-scripts-triggers:" + questsScriptsTriggers.Passed,
            "classic-character-status:" + characterStatus.Passed,
            "classic-rooms-world:" + roomsWorld.Passed,
            "classic-timers-resets-world-rules:" + timersResetsWorldRules.Passed,
            "classic-final-strong-closure:" + finalStrongClosure.Passed,
            "classic-break-autocombat-authority:" + (breakAuthorityFailures.Count == 0),
            "remaining-system-owner-count:" + (RuntimeClassicFinalNativeRepositoryClosureAuthority.RemainingSystemOwnerCount == 36),
            "native-dependency-group-count:" + (RuntimeClassicFinalNativeRepositoryClosureAuthority.NativeDependencyGroupCount == 40),
            "native-artifact-count:" + (RuntimeClassicFinalNativeRepositoryClosureAuthority.NativeArtifactRecordCount == 396),
            "worldgroup-symbol-count:" + (RuntimeClassicFinalNativeRepositoryClosureAuthority.MajorMudUsedWorldgroupSymbolCount == 156),
            "worldgroup-unowned-zero:" + (RuntimeClassicFinalNativeRepositoryClosureAuthority.UnownedMajorMudUsedWorldgroupSymbolCount == 0),
            "all-wcc-families-exhausted:" + RuntimeClassicFinalNativeRepositoryClosureAuthority.SuppliedWccFamiliesExhausted,
            "dependency-inventory-owned:" + RuntimeClassicFinalNativeRepositoryClosureAuthority.NativeDependencyInventoryOwned,
            "dependency-inventory-classified:" + RuntimeClassicFinalNativeRepositoryClosureAuthority.NativeDependencyInventoryClassified,
            "hidden-dependency-bucket-denied:" + (!RuntimeClassicFinalNativeRepositoryClosureAuthority.HiddenNativeDependencyBucketAllowed),
            "standalone-unknowns-zero:" + (RuntimeClassicFinalNativeRepositoryClosureAuthority.UnknownAssertionCount == 0 && RuntimeClassicFinalNativeRepositoryClosureAuthority.SoleStandaloneUnknownAssertion.Length == 0),
            "not-all-formulas-exact:" + (!RuntimeClassicFinalNativeRepositoryClosureAuthority.EveryOwnerLocalFormulaExact),
            "not-all-branch-order-exact:" + (!RuntimeClassicFinalNativeRepositoryClosureAuthority.EveryOwnerLocalBranchOrderExact),
            "not-all-messages-exact:" + (!RuntimeClassicFinalNativeRepositoryClosureAuthority.EveryOwnerLocalMessageExact),
            "nahr-8480-native-empty-arguments-recovered:" + RuntimeClassicFinalNativeRepositoryClosureAuthority.Nahr8480ArgumentsRecovered,
            "exact-repository-inventory-closure:" + RuntimeClassicFinalNativeRepositoryClosureAuthority.CanClaimExactRepositoryInventoryClosure()
        };

        var passed = true;
        foreach (var check in checks)
            passed &= check.EndsWith(":True", System.StringComparison.OrdinalIgnoreCase);

        return new RuntimeClassicParityClosureSmokeReport(passed, checks);
    }
}
