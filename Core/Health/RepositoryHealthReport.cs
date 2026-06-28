namespace Reforged.Core.Health;

public sealed class RepositoryHealthReport
{
    public DateTimeOffset GeneratedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string RepositoryVersion { get; set; } = string.Empty;
    public List<RepositoryHealthIssue> Issues { get; set; } = new();
    public bool IsHealthy => Issues.All(i => i.Severity != "Error" && i.Severity != "Critical");
}
