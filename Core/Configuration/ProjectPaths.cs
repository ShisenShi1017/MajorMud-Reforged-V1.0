namespace MajorMud.Reforged.Core.Configuration;

/// <summary>
/// Central project path contract. Applications should resolve paths once and pass this model around.
/// </summary>
public sealed class ProjectPaths
{
    public string Root { get; set; } = string.Empty;
    public string Apps { get; set; } = "Apps";
    public string Core { get; set; } = "Core";
    public string Data { get; set; } = "Data";
    public string Docs { get; set; } = "Docs";
    public string ImportedDatabases { get; set; } = "ImportedDatabases";
    public string Reports { get; set; } = "Reports";
    public string Saves { get; set; } = "Saves";
    public string Tools { get; set; } = "Tools";
    public string Updates { get; set; } = "Updates";
    public string Logs { get; set; } = "Logs";
    public string Bin { get; set; } = "Bin";
}
