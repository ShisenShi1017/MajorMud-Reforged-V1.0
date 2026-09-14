using System.IO;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

/// <summary>
/// Focused regression checks for RealmGate inventory/equipment policy behavior.
/// This smoke intentionally avoids requiring generated game data so it can verify
/// persistence and command-template safety in an isolated application-settings root.
/// </summary>
public static class RealmGateInventoryAutomationRegressionSmoke
{
    public static Task<int> RunAsync(IApplicationSettingsService settings)
    {
        var failures = new List<string>();
        void Require(bool condition, string name)
        {
            if (!condition) failures.Add(name);
        }

        Require(
            RealmGateInventoryAutomationService.ApplyTemplate("buy {item} {quantity}", "iron sword", 3, 0) == "buy iron sword 3",
            "buy-template-quantity");
        Require(
            RealmGateInventoryAutomationService.ApplyTemplate("deposit {amount}", string.Empty, 1, 1250) == "deposit 1250",
            "deposit-template-amount");

        var sessionA = new ClientSessionConnection
        {
            ServerProfileName = "External BBS",
            WorldName = "Classic",
            HostName = "bbs.example.test",
            Port = 4000,
            CharacterName = "Alpha"
        };
        var sessionB = new ClientSessionConnection
        {
            ServerProfileName = "External BBS",
            WorldName = "Classic",
            HostName = "bbs.example.test",
            Port = 4000,
            CharacterName = "Beta"
        };
        Require(
            !RealmGateInventoryAutomationService.BuildProfileKey(sessionA)
                .Equals(RealmGateInventoryAutomationService.BuildProfileKey(sessionB), StringComparison.OrdinalIgnoreCase),
            "profile-key-character-isolation");

        var root = Path.Combine(settings.RootPath, "Smoke", "RealmGateInventoryAutomation", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new RealmGateInventoryAutomationService(root);
            var profile = service.LoadProfile(sessionA);
            Require(!profile.Preferences.AutoReequipAfterInventoryChange, "auto-reequip-default-off");
            Require(!profile.Preferences.AllowAutomaticDiscard, "discard-default-off");
            Require(!profile.Preferences.AllowAutomaticBuy, "buy-default-off");
            Require(!profile.Preferences.AllowAutomaticSell, "sell-default-off");
            Require(!profile.Preferences.AutoDepositExcessCurrency, "deposit-default-off");

            profile.Preferences.AllowAutomaticSell = true;
            profile.Preferences.KeepCurrency = 10;
            profile.EquipmentSets.Add(new RealmGateEquipmentSet { Name = "Default", Enabled = true, AutoApply = true });
            profile.Rules.Add(new RealmGateInventoryRule
            {
                Priority = 10,
                Disposition = RealmGateInventoryDisposition.Keep,
                MatchMode = RealmGateInventoryRuleMatchMode.ExactName,
                MatchValue = "test item"
            });
            service.SaveProfile(sessionA, profile);

            var reloaded = service.LoadProfile(sessionA);
            Require(reloaded.Preferences.AllowAutomaticSell, "profile-setting-persists");
            Require(reloaded.Preferences.KeepCurrency == 10, "currency-threshold-persists");
            Require(reloaded.EquipmentSets.Count == 1 && reloaded.EquipmentSets[0].Name == "Default", "equipment-set-persists");
            Require(reloaded.Rules.Count == 1 && reloaded.Rules[0].MatchValue == "test item", "inventory-rule-persists");
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        if (failures.Count == 0) return Task.FromResult(0);
        Console.Error.WriteLine("RealmGate inventory automation regression failures: " + string.Join(", ", failures));
        return Task.FromResult(45);
    }
}
