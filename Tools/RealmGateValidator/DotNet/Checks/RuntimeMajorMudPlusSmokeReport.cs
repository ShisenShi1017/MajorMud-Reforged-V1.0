using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeMajorMudPlusSmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeMajorMudPlusSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var temp = Path.Combine(Path.GetTempPath(), "reforged-mmplus-" + Guid.NewGuid().ToString("N"));
        void Check(string name, bool condition)
        {
            checks.Add(name);
            if (!condition) failures.Add(name);
        }

        try
        {
            var data = Path.Combine(temp, "Data", "Classic");
            Directory.CreateDirectory(data);
            File.Copy(Path.Combine(projectRoot, "Data", "Classic", "majormud_plus.json"), Path.Combine(data, "majormud_plus.json"));
            var characters = new RuntimeCharacterService(temp);
            var security = new RuntimeAccountSecurityService(temp);
            security.SetPassword("sysop", "MajorMudPlusSmoke!", RuntimeAccountRole.Sysop);
            var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
            var character = new RuntimeCharacterRecord(
                "classic:plus-smoke", "sysop", "Plus Smoke", "Classic", "race.1", "Human", "class.1", "Warrior",
                1, 0, 20, 20, 0, 0, "room.map1.1", 1,
                new Dictionary<string, int>(), new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(),
                new Dictionary<string, string?>(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["credits"] = "100000",
                    ["membership_days"] = "100",
                    ["lives"] = "1"
                },
                now, now, ItemState: new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 100000, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>()));
            characters.SaveCharacter(character, "mmplus-smoke-seed");
            var service = new RuntimeMajorMudPlusService(characters);
            var session = new RuntimeSession("plus-smoke", null!, new RuntimeRoom(), new RuntimeCharacterActor(
                character.CharacterId, character.DisplayName, character.RaceName, character.ClassName, character.Level), "smoke");

            Check("generated-catalog", RuntimeMajorMudPlusCatalog.Load(temp, "Classic").Settings.MaximumDescriptionLines == 6);
            Check("description-submit", service.Execute(session, RuntimeCommand.Parse("description set A tested hero|Ready for adventure")).Contains("awaiting", StringComparison.OrdinalIgnoreCase));
            Check("registry-set", service.Execute(session, RuntimeCommand.Parse("mudreg set birthplace Newhaven")).Contains("saved", StringComparison.OrdinalIgnoreCase));
            Check("registry-lookup", service.Execute(session, RuntimeCommand.Parse("/mudreg Plus Smoke")).Contains("Newhaven", StringComparison.Ordinal));
            Check("life-purchase", service.Execute(session, RuntimeCommand.Parse("buylife")).Contains("additional life", StringComparison.OrdinalIgnoreCase));
            Check("pending-description", service.Execute(session, RuntimeCommand.Parse("plusadmin pending")).Contains("Plus Smoke", StringComparison.Ordinal));
            Check("approve-description", service.Execute(session, RuntimeCommand.Parse("plusadmin approve Plus Smoke")).Contains("approved", StringComparison.OrdinalIgnoreCase));
            var plusCatalog = RuntimeMajorMudPlusCatalog.Load(temp, "Classic");
            Check("native-action-count", plusCatalog.DefaultActions.Count == 64);
            Check("native-action-message-families", plusCatalog.DefaultActions["yawn"].Messages.Count == 11);
            var nativeMessages = plusCatalog.DefaultActions.Values.SelectMany(action => action.Messages.Values).ToArray();
            Check("native-action-message-count", nativeMessages.Length == 500);
            Check("native-action-placeholders-normalized", nativeMessages.All(message => !message.Contains("%s", StringComparison.Ordinal)));
            Check("native-action-execute", service.TryExecuteAction(session, RuntimeCommand.Parse("yawn"), out var nativeActionText) && nativeActionText == "You yawn.");
            Check("action-create", service.Execute(session, RuntimeCommand.Parse("plusadmin action new testwave single_actor_message You test wave.")).Contains("saved", StringComparison.OrdinalIgnoreCase));
            Check("action-board-specific", service.Execute(session, RuntimeCommand.Parse("plusadmin action change testwave board_specific no")).Contains("saved", StringComparison.OrdinalIgnoreCase));
            Check("action-rename", service.Execute(session, RuntimeCommand.Parse("plusadmin action change testwave action_name testgesture")).Contains("renamed", StringComparison.OrdinalIgnoreCase));
            Check("action-list", service.Execute(session, RuntimeCommand.Parse("plusadmin action list")).Contains("testgesture", StringComparison.OrdinalIgnoreCase));
            Check("action-execute", service.TryExecuteAction(session, RuntimeCommand.Parse("testgesture"), out var actionText) && actionText == "You test wave.");
            Check("clear-plus-data-scope", service.Execute(session, RuntimeCommand.Parse("plusadmin clear confirm")).Contains("descriptions and registries", StringComparison.OrdinalIgnoreCase));
            Check("clear-preserves-actions", service.TryExecuteAction(session, RuntimeCommand.Parse("testgesture"), out var preservedActionText) && preservedActionText == "You test wave.");
            Check("clear-removes-description", service.Execute(session, RuntimeCommand.Parse("description")).Contains("no custom", StringComparison.OrdinalIgnoreCase));
            var contest = RuntimeMajorMudPlusCatalog.Load(temp, "Classic").ContestContract;
            Check("contest-native-item", contest.CanonicalItemId == "item.965" && contest.LegacyItemNumber == 965);
            Check("contest-native-ranges", contest.PlacementRanges.Count == 17 && contest.PlacementRanges.Sum(range => range.Quantity) == 40);
            Check("contest-native-capacities", contest.MaximumAttemptsPerItem == 50 && contest.VisibleRoomCapacity == 17 && contest.HiddenRoomCapacity == 15);
            try
            {
                service.Execute(session, RuntimeCommand.Parse("plusadmin contest start hidden"));
                Check("contest-living-world-required", false);
            }
            catch (InvalidOperationException exception)
            {
                Check("contest-living-world-required", exception.Message.Contains("living world", StringComparison.OrdinalIgnoreCase));
            }
            var definition = ServerRuntimeOptions.Load(projectRoot).Worlds.Single(world => world.Id.Equals("Classic", StringComparison.OrdinalIgnoreCase));
            var world = new RuntimeWorldLoader().Load(projectRoot, definition);
            var living = new RuntimeLivingWorldService(projectRoot, world, Path.Combine(temp, "LivingWorld"));
            var contestStart = service.Execute(session, RuntimeCommand.Parse("plusadmin contest start hidden"), living);
            var contestItems = living.Capture().Items!.Where(item => item.Metadata?.ContainsKey("native_contest_item") == true).ToArray();
            Check("contest-live-placement", contestStart.Contains("40/40", StringComparison.Ordinal) && contestItems.Length == 40);
            Check("contest-hidden-placement", contestItems.All(RuntimeClassicHiddenSearchService.IsHidden));
            Check("contest-status", service.Execute(session, RuntimeCommand.Parse("plusadmin contest status"), living).Contains("Found hidden", StringComparison.Ordinal));
            Check("contest-finish", service.Execute(session, RuntimeCommand.Parse("plusadmin contest finish"), living).Contains("finished", StringComparison.OrdinalIgnoreCase));
            Check("contest-finish-removes-runes", living.Capture().Items!.All(item => !item.CanonicalItemId.Equals("item.965", StringComparison.OrdinalIgnoreCase)));

            var saved = characters.LoadCharacter(character.CharacterId)!;
            Check("cost-persistence", saved.Conditions["credits"] == "67600");
            Check("life-persistence", saved.Conditions["lives"] == "2");
            Check("currency-persistence", RuntimeBundleCInventoryAuthority.ReadCurrency(saved.ItemState!).CopperValue == 90000);
        }
        catch (Exception exception)
        {
            failures.Add($"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
        }
        return new(failures.Count == 0, checks, failures);
    }
}
