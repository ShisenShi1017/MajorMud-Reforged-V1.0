namespace RealmGate.App.Models;

public sealed record ClientHelpTopic(
    string Id,
    string Category,
    string Title,
    string Summary,
    string Content,
    IReadOnlyList<string> Keywords)
{
    public string SearchText => string.Join(' ', new[] { Category, Title, Summary, Content }.Concat(Keywords));
}
