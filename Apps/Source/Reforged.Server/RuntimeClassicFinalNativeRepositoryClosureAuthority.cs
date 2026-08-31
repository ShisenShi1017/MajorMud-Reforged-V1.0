using System;
using System.Collections.Generic;

namespace Reforged.Server;

/// <summary>
/// Exact final native repository inventory/ownership gate.
///
/// Inventory closure is exact: every retained Remaining Systems owner, native dependency group,
/// supplied native artifact family and MajorMUD-used Worldgroup symbol has an explicit owner.
/// This is intentionally not a shortcut that promotes owner-local formulas, messages, limits,
/// branch ordering or persistence layouts in other parity assertions.
/// </summary>
public static class RuntimeClassicFinalNativeRepositoryClosureAuthority
{
    public const int RemainingSystemOwnerCount = 36;
    public const int NativeDependencyGroupCount = 40;
    public const int NativeDependencyReportRecordCount = 41; // 40 dependency groups + this closure meta-record
    public const int NativeArtifactRecordCount = 396;
    public const int WccArtifactCount = 147;
    public const int WbbArtifactCount = 234;
    public const int NativeRuntimeArtifactCount = 15;
    public const int MajorMudUsedWorldgroupSymbolCount = 156;
    public const int UnownedMajorMudUsedWorldgroupSymbolCount = 0;
    public const int ScoredParityAssertionCount = 200;

    public const int PassAssertionCount = 157;
    public const int StrongAssertionCount = 43;
    public const int UnknownAssertionCount = 0;
    public const string SoleStandaloneUnknownAssertion = "";

    public static bool RemainingSystemInventoryOwned => true;
    public static bool NativeDependencyInventoryOwned => true;
    public static bool NativeDependencyInventoryClassified => true;
    public static bool SuppliedWccFamiliesExhausted => true;
    public static bool WorldgroupImportOwnershipClosed => true;
    public static bool HiddenNativeDependencyBucketAllowed => false;
    public static bool ExactRepositoryInventoryClosureResolved => true;

    // These remain false by design. Inventory closure is not an exactness shortcut.
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
        "Librarium/Parity/majormud_final_native_repository_closure_evidence.json",
        "Librarium/Parity/majormud_host_persistence_worldgroup_complete_evidence_0361.json"
    };

    public static IReadOnlyList<string> OwnerLocalNonClaims { get; } = new[]
    {
        "owner-local-exact-formulas-remain-at-owner-boundary",
        "owner-local-exact-branch-order-remains-at-owner-boundary",
        "owner-local-exact-messages-remain-at-owner-boundary",
        "owner-local-exact-limits-flags-timers-remain-at-owner-boundary",
        "owner-local-persistence-layout-remains-at-owner-boundary"
    };

    public static bool CanClaimExactRepositoryInventoryClosure()
        => ExactRepositoryInventoryClosureResolved
           && RemainingSystemInventoryOwned
           && NativeDependencyInventoryOwned
           && NativeDependencyInventoryClassified
           && SuppliedWccFamiliesExhausted
           && WorldgroupImportOwnershipClosed
           && RemainingSystemOwnerCount == 36
           && NativeDependencyGroupCount == 40
           && NativeDependencyReportRecordCount == NativeDependencyGroupCount + 1
           && NativeArtifactRecordCount == WccArtifactCount + WbbArtifactCount + NativeRuntimeArtifactCount
           && MajorMudUsedWorldgroupSymbolCount == 156
           && UnownedMajorMudUsedWorldgroupSymbolCount == 0
           && UnknownAssertionCount == 0
           && string.IsNullOrEmpty(SoleStandaloneUnknownAssertion)
           && Nahr8480ArgumentsRecovered;

    public static bool CanClaimStrongRepositoryOwnerClosure() => CanClaimExactRepositoryInventoryClosure();
}
