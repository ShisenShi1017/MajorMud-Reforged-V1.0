using System.Text.Json;
namespace Reforged.Server;
public sealed record RuntimeHelpReferenceSmokeReport(bool Passed, IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeHelpReferenceSmokeReport Run()
    {
        var checks = new List<string>();
        var service = new RuntimeHelpReferenceService(new[]
        {
            new RuntimeHelpTopicDefinition("general.getting-started", "Getting Started", "Welcome.\r\nUse HELP COMMANDS.", RuntimeHelpTopicKind.General, RuntimeHelpAccess.Player, RuntimeHelpContentOwner.Module, new[] { "start", "new player" }, new[] { "command.say" }),
            new RuntimeHelpTopicDefinition("command.say", "Say Command", "SAY <message>", RuntimeHelpTopicKind.Command, RuntimeHelpAccess.Player, RuntimeHelpContentOwner.Generated, new[] { "say", "speak" }, new[] { "general.getting-started" }, "textblock:help-say"),
            new RuntimeHelpTopicDefinition("admin.reset", "Reset Administration", "Administrative reset reference.", RuntimeHelpTopicKind.Administrative, RuntimeHelpAccess.Administrator, RuntimeHelpContentOwner.Module, new[] { "reset admin" }, Array.Empty<string>())
        });
        var player = new RuntimeHelpActor("character:1", RuntimeHelpAccess.Player);
        var admin = new RuntimeHelpActor("host:1", RuntimeHelpAccess.Host);
        var exact = service.Resolve(player, "say"); if (!exact.Success || exact.Topic?.TopicId != "command.say" || exact.Topic.Body.Contains('\r')) checks.Add("exact-alias-render");
        var search = service.Search(player, "getting"); if (!search.Success || search.Hits.Count != 1 || search.Hits[0].TopicId != "general.getting-started") checks.Add("deterministic-search");
        var denied = service.Resolve(player, "admin.reset"); if (denied.Success || denied.Code != "help-topic-not-available") checks.Add("permission-filter");
        var allowed = service.Resolve(admin, "reset admin"); if (!allowed.Success || allowed.Topic?.Kind != RuntimeHelpTopicKind.Administrative) checks.Add("admin-access");
        var injection = service.Resolve(player, "say\nadmin.reset"); if (injection.Success) checks.Add("query-sanitization");
        if (service.ValidateCatalog().Count != 0) checks.Add("catalog-validation");
        return new(checks.Count == 0, checks);
    }
}
