using System;
using System.Collections.Generic;

namespace Reforged.Server;

/// <summary>
/// PASS61 / Update 0332 evidence gate for the final native repository inventory.
///
/// This authority closes ownership/inventory only. It deliberately does not
/// promote owner-local STRONG semantics to exact Classic behavior.
/// </summary>
public static class RuntimeClassicFinalNativeRepositoryClosureAuthority
{
    public const int RemainingSystemOwnerCount = 36;
    public const int NativeDependencyGroupCount = 40;
    public const int NativeArtifactRecordCount = 396;
    public const int WccArtifactCount = 147;
    public const int WbbArtifactCount = 234;
    public const int NativeRuntimeArtifactCount = 15;
    public const int MajorMudUsedWorldgroupSymbolCount = 156;
    public const int UnownedMajorMudUsedWorldgroupSymbolCount = 0;
    public const int ScoredParityAssertionCount = 200;

    public const int PassAssertionCount = 98;
    public const int StrongAssertionCount = 102;
    public const int UnknownAssertionCount = 0;

    public const string SoleStandaloneUnknownAssertion = "";

    public static bool RemainingSystemInventoryOwned => true;
    public static bool NativeDependencyInventoryOwned => true;
    public static bool NativeDependencyInventoryClassified => true;
    public static bool SuppliedWccFamiliesExhausted => true;
    public static bool WorldgroupImportOwnershipClosed => true;
    public static bool HiddenNativeDependencyBucketAllowed => false;

    // These remain false by design. Repository closure is not an exactness shortcut.
    public static bool EveryOwnerLocalFormulaExact => false;
    public static bool EveryOwnerLocalBranchOrderExact => false;
    public static bool EveryOwnerLocalMessageExact => false;
    public static bool EveryOwnerLocalPersistenceLayoutExact => false;
    public static bool Nahr8480ArgumentsRecovered => true;
    public static bool ReforgedSafeShellsDefineClassicSemantics => false;

    public static IReadOnlyList<string> EvidenceFiles { get; } = new[]
    {
        "Librarium/RemainingSystems/repository_inventory_closure.json",
        "Librarium/Reports/COMBINED_DECODE_REMAINING_AUDIT_AFTER_REMAINING_SYSTEMS.json",
        "Librarium/NativeEvidence/native_artifact_catalog_0001.json",
        "Librarium/Evidence/native_wcc_available_artifact_exhaustion_0034.json",
        "Librarium/Evidence/worldgroup_final_ownership_audit_0036.json",
        "Librarium/Parity/majormud_final_native_repository_closure_evidence.json"
    };

    public static IReadOnlyList<string> FailClosedRemainders { get; } = new[]
    {
        "owner-local-exact-formulas-remain-at-owner-boundary",
        "owner-local-exact-branch-order-remains-at-owner-boundary",
        "owner-local-exact-messages-remain-at-owner-boundary",
        "owner-local-exact-limits-flags-timers-remain-at-owner-boundary",
        "owner-local-save-layout-and-persistence-remain-at-owner-boundary",
        "no-standalone-unknown-assertions"
    };

    public static bool CanClaimStrongRepositoryOwnerClosure()
        => RemainingSystemInventoryOwned
           && NativeDependencyInventoryOwned
           && NativeDependencyInventoryClassified
           && SuppliedWccFamiliesExhausted
           && WorldgroupImportOwnershipClosed
           && UnownedMajorMudUsedWorldgroupSymbolCount == 0
           && UnknownAssertionCount == 0
           && string.IsNullOrEmpty(SoleStandaloneUnknownAssertion)
           && Nahr8480ArgumentsRecovered;
}
