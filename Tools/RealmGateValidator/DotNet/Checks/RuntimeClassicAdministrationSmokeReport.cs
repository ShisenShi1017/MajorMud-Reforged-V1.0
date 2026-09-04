using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicAdministrationSmokeCheck(string Name, bool Passed, string Detail);

public sealed record RuntimeClassicAdministrationSmokeResult(
    bool Passed,
    int TotalChecks,
    int PassedChecks,
    IReadOnlyList<RuntimeClassicAdministrationSmokeCheck> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeClassicAdministrationSmokeReport
{
    public static RuntimeClassicAdministrationSmokeResult Run()
    {
        var checks = new List<RuntimeClassicAdministrationSmokeCheck>();
        void Check(string name, bool passed, string detail) => checks.Add(new(name, passed, detail));
        var root = Path.Combine(Path.GetTempPath(), "reforged-classic-admin-smoke-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            var defaults = RuntimeClassicAdministrationPolicyStore.Load(root, "Classic");
            Check("default-preserves-unlimited-items", defaults.UnlimitedItemsEnabled, defaults.UnlimitedItemsEnabled.ToString());
            Check("default-denies-global-sys-goto", !defaults.EveryoneCanUseSysGoto, defaults.EveryoneCanUseSysGoto.ToString());

            var configured = new RuntimeClassicAdministrationPolicy
            {
                WorldId = "Classic",
                UnlimitedItemsEnabled = false,
                EveryoneCanUseSysGoto = true,
                SysopCharacterIds = ["classic:borin", "CLASSIC:BORIN", "classic:builder"]
            };
            RuntimeClassicAdministrationPolicyStore.Save(root, configured, "smoke-test");
            var loaded = RuntimeClassicAdministrationPolicyStore.Load(root, "classic");
            Check("policy-round-trip", !loaded.UnlimitedItemsEnabled && loaded.EveryoneCanUseSysGoto, $"unlimited={loaded.UnlimitedItemsEnabled}; goto={loaded.EveryoneCanUseSysGoto}");
            Check("character-sysop-grants-normalized", loaded.SysopCharacterIds.Count == 2 && loaded.HasCharacterSysopAccess("CLASSIC:BORIN"), string.Join(',', loaded.SysopCharacterIds));
            Check("policy-path-is-world-scoped", RuntimeClassicAdministrationPolicyStore.PathFor(root, "Classic").EndsWith(Path.Combine("administration", "classic.classic-policy.json"), StringComparison.OrdinalIgnoreCase), RuntimeClassicAdministrationPolicyStore.PathFor(root, "Classic"));

            var limitedDefinition = new RuntimeItemDefinition(
                "item.9000", "Smoke Limited Item", "Test", true, false, false, true, 0)
            {
                GameLimit = 2,
                CurrentInstances = 0
            };
            RuntimeClassicItemLimitCoordinator.EnsureCanCreate(root, "classic", limitedDefinition, 2, 0);
            Check("native-item-limit-allows-up-to-cap", true, "quantity=2; limit=2");

            var worlds = Path.Combine(RuntimeSavePaths.RealmRoot(root, "classic"), "worlds");
            Directory.CreateDirectory(worlds);
            var state = new RuntimeItemState(
                [new RuntimeItemInstance("limited-stack", limitedDefinition.Id, null, RuntimeItemLocations.Room, "room.1", null, 0, 2, null)],
                0,
                new Dictionary<string, string>(),
                Array.Empty<RuntimeCorpseState>());
            File.WriteAllText(
                Path.Combine(worlds, "classic.items.json"),
                JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));

            var blocked = false;
            try
            {
                RuntimeClassicItemLimitCoordinator.EnsureCanCreate(root, "classic", limitedDefinition, 1, 0);
            }
            catch (InvalidOperationException)
            {
                blocked = true;
            }
            Check("native-item-limit-blocks-over-cap", blocked, blocked ? "blocked" : "unexpectedly allowed");

            var livingDefinition = limitedDefinition with { Id = "item.9001", Name = "Living World Limited Item", GameLimit = 1 };
            var livingFolder = Path.Combine(RuntimeSavePaths.RealmRoot(root, "classic"), "WorldState");
            Directory.CreateDirectory(livingFolder);
            var livingSnapshot = new RuntimeLivingWorldSnapshot(
                "classic",
                Array.Empty<RuntimeLivingNpcState>(),
                Array.Empty<RuntimeLivingWorldObjectState>(),
                DateTimeOffset.UtcNow,
                [new RuntimeItemInstance("living-limited", livingDefinition.Id, null, RuntimeItemLocations.Room, "room.2", null, 0, 1, null)]);
            File.WriteAllText(
                Path.Combine(livingFolder, "living-world.json"),
                JsonSerializer.Serialize(livingSnapshot, new JsonSerializerOptions { WriteIndented = true }));
            var livingBlocked = false;
            try
            {
                RuntimeClassicItemLimitCoordinator.EnsureCanCreate(root, "classic", livingDefinition, 1, 0);
            }
            catch (InvalidOperationException)
            {
                livingBlocked = true;
            }
            Check("living-world-items-count-toward-native-cap", livingBlocked, livingBlocked ? "blocked" : "unexpectedly allowed");

            loaded.UnlimitedItemsEnabled = true;
            RuntimeClassicAdministrationPolicyStore.Save(root, loaded, "smoke-test-unlimited");
            RuntimeClassicItemLimitCoordinator.EnsureCanCreate(root, "classic", limitedDefinition, 100, 0);
            Check("unlimited-toggle-bypasses-native-cap", true, "quantity=100; limit=2");

            var parsed = RuntimeCommand.Parse("sys goto newhaven");
            Check("sys-goto-client-command-resolves", parsed.Name == "sysop" && parsed.ArgumentText == "goto newhaven", $"name={parsed.Name}; args={parsed.ArgumentText}");
        }
        catch (Exception exception)
        {
            Check("classic-administration-smoke-completed", false, $"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch
            {
            }
        }

        return new RuntimeClassicAdministrationSmokeResult(
            checks.All(check => check.Passed),
            checks.Count,
            checks.Count(check => check.Passed),
            checks);
    }
}
