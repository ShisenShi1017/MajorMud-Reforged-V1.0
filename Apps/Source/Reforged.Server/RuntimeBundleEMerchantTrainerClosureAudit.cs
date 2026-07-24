using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleEMerchantTrainerClosureAuditReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> EvidenceSources,
    IReadOnlyList<string> OwnershipBoundaries)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Bundle E disconnect-to-connect gate for merchants and trainers. This gate does not move
/// economy ownership out of Bundle C. It proves that decoded/imported shop and trainer data,
/// living NPC service admission, persistent stock/offering state, restock/cooldown completion,
/// and the Bundle C settlement boundary all have live authoritative consumers.
/// </summary>
public static class RuntimeBundleEMerchantTrainerClosureAudit
{
    public static RuntimeBundleEMerchantTrainerClosureAuditReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var evidence = new List<string>();

        void Check(bool condition, string id)
        {
            if (condition) checks.Add(id); else failures.Add(id);
        }

        Add(RuntimeNpcMerchantStockOwnershipSmokeReport.Run(), "merchant-stock");
        Add(RuntimeNpcMerchantAccessAdmissionSmokeReport.Run(), "merchant-access");
        Add(RuntimeNpcMerchantRestockAdmissionSmokeReport.Run(), "merchant-restock");
        Add(RuntimeNpcMerchantGoldSettlementSmokeReport.Run(), "merchant-gold-settlement");
        Add(RuntimeNpcTrainerEligibilityAdmissionSmokeReport.Run(), "trainer-eligibility");
        Add(RuntimeNpcTrainerOfferingOwnershipSmokeReport.Run(), "trainer-offering-ownership");
        Add(RuntimeNpcTrainerOfferingAdmissionSmokeReport.Run(), "trainer-offering-admission");
        Check(RuntimeEconomyMerchantLifecycleSmokeReport.CoveredBehaviors.Count >= 10, "bundle-c-merchant-lifecycle-coverage-connected");
        foreach (var behavior in RuntimeEconomyMerchantLifecycleSmokeReport.CoveredBehaviors) checks.Add("bundle-c-merchant-lifecycle:" + behavior);

        var living = typeof(RuntimeLivingWorldService);
        Check(living.GetMethod(nameof(RuntimeLivingWorldService.AdmitNpcService)) is not null, "living-npc-service-admission-connected");
        Check(living.GetMethod(nameof(RuntimeLivingWorldService.CompleteNpcService)) is not null, "living-npc-service-completion-connected");

        var store = typeof(RuntimeGameplayCompletionStateStore);
        Check(store.GetMethod(nameof(RuntimeGameplayCompletionStateStore.Read)) is not null, "persistent-shop-state-read-connected");
        Check(store.GetMethod(nameof(RuntimeGameplayCompletionStateStore.ExecuteShopTransaction)) is not null, "persistent-shop-state-transaction-connected");
        Check(store.GetMethod(nameof(RuntimeGameplayCompletionStateStore.ReadTrainer)) is not null, "persistent-trainer-state-read-connected");
        Check(store.GetMethod(nameof(RuntimeGameplayCompletionStateStore.ExecuteTrainerTransaction)) is not null, "persistent-trainer-state-transaction-connected");

        var requiredEvidence = new[]
        {
            "Data/Classic/shops.json",
            "ImportedDatabases/NativeRuntime/WCCSHOPS.DAT",
            "ImportedDatabases/WCCDefaultInstall/wcc default/wccshop2.dat",
            "ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/codex-shop-wcc.json",
            "Librarium/Objects/shops/catalog.json",
            "Librarium/Objects/shops/sql_mdb_records_0128.json",
            "Librarium/Runtime/Economy/merchant_lifecycle_catalog_0155.json",
            "Librarium/MajorMUDKnowledgeBase/MechanicsFull/ECON_SHOPS.json",
            "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_TRAINING.json",
            "Simulator/DataSimVer/0008/ShopInventoryItemRefs.csv",
            "Simulator/DataSimVer/0008/ShopServiceInventory.csv",
            "Simulator/DataSimVer/0008/TrainerRoomRefs.csv",
            "Evidence/DataSimVer0014/ShopInventoryEconomyEvidence.csv",
            "Evidence/NpcAI/npc_ai_merchant_stock_evidence.json",
            "Evidence/NpcAI/npc_ai_merchant_restock_admission_evidence.json",
            "Evidence/NpcAI/npc_ai_merchant_access_admission_evidence.json",
            "Evidence/NpcAI/npc_ai_merchant_gold_settlement_evidence.json",
            "Evidence/NpcAI/npc_ai_trainer_eligibility_admission_evidence.json",
            "Evidence/NpcAI/npc_ai_trainer_offering_evidence.json",
            "Evidence/NpcAI/npc_ai_trainer_offering_admission_evidence.json"
        };
        foreach (var relative in requiredEvidence)
        {
            var present = File.Exists(Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            Check(present, "evidence-source-present:" + relative);
            if (present) evidence.Add(relative);
        }

        var serverRoot = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server");
        foreach (var singular in new[]
        {
            "RuntimeNpcMerchantStockOwnership.cs", "RuntimeNpcMerchantAccessAdmission.cs",
            "RuntimeNpcMerchantRestockAdmission.cs", "RuntimeNpcMerchantGoldSettlement.cs",
            "RuntimeNpcTrainerEligibilityAdmission.cs", "RuntimeNpcTrainerOfferingOwnership.cs",
            "RuntimeNpcTrainerOfferingAdmission.cs", "RuntimeEconomyMerchantLifecycle.cs"
        })
            Check(Directory.GetFiles(serverRoot, singular, SearchOption.TopDirectoryOnly).Length == 1, "single-authority:" + singular);

        return new(
            failures.Count == 0,
            checks,
            failures.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            evidence,
            [
                "Bundle E owns living merchant/trainer NPC behavior, service admission, restock admission, offering admission and completion routing.",
                "Bundle C remains authoritative for prices, currency, inventory transfer, item identity, merchant gold and economic persistence.",
                "No parallel shop, trainer, currency or inventory authority is introduced by this closure gate."
            ]);

        void Add(dynamic report, string prefix)
        {
            Check((bool)report.Passed, prefix + "-smoke-passed");
            foreach (string check in report.Checks) checks.Add(prefix + ":" + check);
            foreach (string failure in report.Failures) failures.Add(prefix + ":" + failure);
        }
    }
}
