namespace Reforged.App.Services;

public static class ClientHelpRegressionSmoke
{
    public static int Run()
    {
        var failures = new List<string>();
        void Require(bool condition, string name)
        {
            if (!condition) failures.Add(name);
        }

        Require(ClientHelpCatalog.Topics.Count >= 14, "extensive-topic-catalog");
        Require(ClientHelpCatalog.Topics.Select(topic => topic.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == ClientHelpCatalog.Topics.Count,
            "unique-topic-identities");
        Require(ClientHelpCatalog.Topics.All(topic =>
            !string.IsNullOrWhiteSpace(topic.Category) && !string.IsNullOrWhiteSpace(topic.Title) &&
            !string.IsNullOrWhiteSpace(topic.Summary) && topic.Content.Length >= 100), "complete-topic-content");
        Require(ClientHelpCatalog.Search("exp").Any(topic => topic.Id == "commands"), "exp-command-search");
        Require(ClientHelpCatalog.Search("par").Any(topic => topic.Id == "party"), "par-command-search");
        Require(ClientHelpCatalog.Search("whohome").Any(topic => topic.Id == "bosses"), "whohome-command-search");
        Require(ClientHelpCatalog.Search("fog hidden").Any(topic => topic.Id == "map"), "map-option-search");
        Require(ClientHelpCatalog.Search("emergency stop").Any(topic => topic.Id == "automation"), "emergency-stop-search");
        Require(ClientHelpCatalog.Search("private terminal").Any(topic => topic.Id == "privacy"), "privacy-search");
        Require(ClientHelpCatalog.Search("reconnect stale").Any(topic => topic.Id == "troubleshooting"), "reconnect-search");
        Require(ClientHelpCatalog.Search(null).Count == ClientHelpCatalog.Topics.Count, "empty-search-lists-all");

        if (failures.Count == 0) return 0;
        Console.Error.WriteLine("Client Help regression failures: " + string.Join(", ", failures));
        return 44;
    }
}
