namespace Reforged.Core.Health;

public sealed class RepositoryHealthIssue
{
    public string Severity { get; set; } = "Info";
    public string Area { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Path { get; set; }
}
