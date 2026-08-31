using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicEconomyParityAuditReport(
    string Status,
    int ShopCount,
    int ZeroMarkupShopCount,
    int HundredMarkupShopCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public bool Passed => Status.Equals("PASS", StringComparison.OrdinalIgnoreCase);
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeClassicEconomyParityAudit
{
    public static RuntimeClassicEconomyParityAuditReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var shopCount = 0;
        var zeroMarkup = 0;
        var hundredMarkup = 0;
        void Check(bool condition, string name)
        {
            if (condition) checks.Add(name);
            else failures.Add(name);
        }

        Check(RuntimeLiveGameplayFacade.CostTypeCopperMultiplier(0) == 1L
              && RuntimeLiveGameplayFacade.CostTypeCopperMultiplier(1) == 10L
              && RuntimeLiveGameplayFacade.CostTypeCopperMultiplier(2) == 100L
              && RuntimeLiveGameplayFacade.CostTypeCopperMultiplier(3) == 10_000L
              && RuntimeLiveGameplayFacade.CostTypeCopperMultiplier(4) == 1_000_000L,
            "stock-five-denomination-copper-conversion");

        Check(RuntimeEconomyNativeMarkup.NeutralMarkupPercent == 0
              && RuntimeEconomyNativeMarkup.ToMultiplierBasisPoints(0) == 10_000
              && RuntimeEconomyNativeMarkup.ToMultiplierBasisPoints(100) == 20_000
              && RuntimeEconomyNativeMarkup.Apply(100, 500, RuntimeEconomyRoundingPolicy.Floor) == 600,
            "stock-additive-shop-markup");

        Check(RuntimeClassicEconomyPricing.QuoteBuyCopper(100, 0, 0) == 110
              && RuntimeClassicEconomyPricing.QuoteBuyCopper(100, 100, 50) == 200
              && RuntimeClassicEconomyPricing.QuoteBuyCopper(100, 500, 50) == 600,
            "stock-buy-markup-and-charm-zero-stage");

        Check(RuntimeClassicEconomyPricing.QuoteBuyCopper(100, 100, 1) == 220
              && RuntimeClassicEconomyPricing.QuoteBuyCopper(100, 100, 50) == 200
              && RuntimeClassicEconomyPricing.QuoteBuyCopper(100, 100, 100) == 180,
            "stock-buy-charm-stage");

        Check(RuntimeClassicEconomyPricing.QuoteSellCopper(100, 0) == 25
              && RuntimeClassicEconomyPricing.QuoteSellCopper(100, 50) == 50
              && RuntimeClassicEconomyPricing.QuoteSellCopper(100, 100) == 75,
            "stock-sell-charm-stage");

        Check(RuntimeClassicEconomyPricing.QuoteBuyCopper(101, 0, 55) == 99
              && RuntimeClassicEconomyPricing.QuoteBuyCopper(99_999, 1, 1) == 111_097,
            "stock-buy-integer-truncation-no-vb-round");

        Check(RuntimeClassicEconomyPricing.QuoteBuyCopper(100_001, 0, 50) == 100_000,
            "stock-buy-over-100k-scale-boundary");

        // Markup is intentionally absent from QuoteSellCopper; buying a 100-copper item
        // from a +500% shop costs 600 at Charm 50, but selling it remains 50 at Charm 50.
        Check(RuntimeClassicEconomyPricing.QuoteBuyCopper(100, 500, 50) == 600
              && RuntimeClassicEconomyPricing.QuoteSellCopper(100, 50) == 50,
            "stock-selling-ignores-shop-markup");

        Check(RuntimeClassicEconomyPricing.TrainingCostCopper(0, 300) == 0
              && RuntimeClassicEconomyPricing.TrainingCostCopper(1, 300) == 200
              && RuntimeClassicEconomyPricing.TrainingCostCopper(9, 0) == 450
              && RuntimeClassicEconomyPricing.TrainingCostCopper(9, 100) == 900,
            "stock-trainer-money-formula");

        Check(RuntimeClassicHealerServiceAuthority.HealingCostCopper(100, 80) == 40
              && RuntimeClassicHealerServiceAuthority.HealingCostCopper(100, 100) == 0
              && RuntimeClassicHealerServiceAuthority.CuringCostCopper(false) == 150
              && RuntimeClassicHealerServiceAuthority.CuringCostCopper(true) == 250,
            "native-shoptype5-healer-price-formulas");
        Check(RuntimeClassicHealerServiceAuthority.IsCommand("healing")
              && RuntimeClassicHealerServiceAuthority.IsCommand("curing")
              && RuntimeClassicHealerServiceAuthority.IsCommand("cure poison")
              && !RuntimeClassicHealerServiceAuthority.IsCommand("repair")
              && RuntimeClassicHealerServiceAuthority.HealingInsufficientFundsText == "You do not have sufficient funds to buy full healing!"
              && RuntimeClassicHealerServiceAuthority.CuringInsufficientFundsText == "You do not have sufficient funds to buy curing!",
            "native-shoptype5-command-and-failure-text");
        Check(RuntimeClassicShopTransactionAuthority.CannotAfford("Test Item") == "You cannot afford Test Item.",
            "native-merchandise-buy-insufficient-funds-text");
        Check(RuntimeClassicPaidCarriageAuthority.Routes.Count == 6
              && RuntimeClassicPaidCarriageAuthority.ValidateDecodedGraph(projectRoot).Count == 0,
            "decoded-paid-carriage-routes-exact");

        try
        {
            var shopPath = Path.Combine(projectRoot, "Librarium", "Objects", "shops", "shop_records_0122.json");
            using var document = JsonDocument.Parse(File.ReadAllText(shopPath));
            var records = document.RootElement.GetProperty("records").EnumerateArray().ToArray();
            shopCount = records.Length;
            var healerShops = records.Where(record => ReadServiceType(record) == 5
                && ReadServiceKind(record).Equals("healer_or_temple", StringComparison.OrdinalIgnoreCase)).ToArray();
            var deedOffices = records.Where(record => ReadServiceType(record) == 12
                && ReadServiceKind(record).Equals("realm_deed_office", StringComparison.OrdinalIgnoreCase)).ToArray();
            Check(healerShops.Length == 1, "single-native-shoptype5-healer");
            Check(deedOffices.Length == 1, "shoptype12-realm-deed-office-classified");
            var markups = records.Select(ReadMarkup).ToArray();
            zeroMarkup = markups.Count(value => value == 0);
            hundredMarkup = markups.Count(value => value == 100);
            Check(shopCount == 178, "classic-shop-count-178");
            Check(markups.All(value => value >= 0), "classic-shop-markups-nonnegative");
            Check(zeroMarkup == 50, "classic-zero-markup-preserved-50");
            Check(hundredMarkup == 44, "classic-hundred-markup-preserved-44");

            var markupCatalogPath = Path.Combine(projectRoot, "Librarium", "Runtime", "Economy", "native_markup_catalog_0162.json");
            using var markupDocument = JsonDocument.Parse(File.ReadAllText(markupCatalogPath));
            var bindings = markupDocument.RootElement.GetProperty("bindings").EnumerateArray().ToArray();
            Check(bindings.Length == 178, "markup-binding-count-178");
            Check(bindings.All(binding =>
            {
                var raw = binding.GetProperty("rawMarkupPercent").GetInt32();
                return binding.GetProperty("multiplierBasisPoints").GetInt32() == checked((100 + raw) * 100);
            }), "markup-binding-additive-conversion");

            var messagesPath = Path.Combine(projectRoot, "Librarium", "Objects", "native_message_payloads", "native_message_payload_records.json");
            using (var messagesDocument = JsonDocument.Parse(File.ReadAllText(messagesPath)))
            {
                var messages = messagesDocument.RootElement.GetProperty("records").EnumerateArray().ToDictionary(
                    row => row.GetProperty("message_id").GetInt32(), row => row.GetProperty("player_payload").GetString() ?? string.Empty);
                Check(messages.GetValueOrDefault(318) == "You cannot afford that service."
                      && messages.GetValueOrDefault(2576) == "You cannnot afford that service."
                      && messages.GetValueOrDefault(2577) == "You pay the fare and buy passage to Khazarad."
                      && messages.GetValueOrDefault(2578) == "You pay the fare and buy passage to Rhudaur."
                      && messages.GetValueOrDefault(2596) == "You pay the fare and buy passage to Silvermere.",
                    "native-service-and-carriage-payloads-exact");
            }

            var coveragePath = Path.Combine(projectRoot, "Reports", "RuntimeVerification", "classic_economy_service_coverage.json");
            if (File.Exists(coveragePath))
            {
                using var coverageDocument = JsonDocument.Parse(File.ReadAllText(coveragePath));
                var root = coverageDocument.RootElement;
                Check(root.GetProperty("ShopCount").GetInt32() == 178
                      && root.GetProperty("BankCount").GetInt32() == 26
                      && root.GetProperty("TrainerCount").GetInt32() == 44
                      && root.GetProperty("InventoryEntryCount").GetInt32() == 1204
                      && root.GetProperty("UnclassifiedServiceCount").GetInt32() == 0,
                    "economy-service-corpus-closed");
            }
            else failures.Add("economy-service-coverage-report-missing");
        }
        catch (Exception exception)
        {
            failures.Add("economy-corpus-audit:" + exception.Message);
        }

        Check(typeof(RuntimeGameplayCompletionStateStore).GetMethod("ExecuteShopTransaction") is not null,
            "persistent-shop-stock-transaction-boundary");
        Check(typeof(RuntimeLiveGameplayFacade).GetMethod("ExecuteClassicShopTransaction",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) is not null,
            "shop-stock-character-compensation-boundary");
        Check(typeof(RuntimeCharacterService).GetMethod("ExecuteCharacterTransaction") is not null,
            "direct-character-transaction-boundary");

        return new RuntimeClassicEconomyParityAuditReport(
            failures.Count == 0 ? "PASS" : "FAIL",
            shopCount,
            zeroMarkup,
            hundredMarkup,
            checks,
            failures);
    }

    private static int ReadServiceType(JsonElement record)
    {
        if (!record.TryGetProperty("service_type", out var value)) return -1;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => -1
        };
    }

    private static string ReadServiceKind(JsonElement record) =>
        record.TryGetProperty("service_kind", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;

    private static int ReadMarkup(JsonElement record)
    {
        var value = record.GetProperty("markup_percent");
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => throw new InvalidDataException("Classic shop record has invalid markup_percent.")
        };
    }
}
