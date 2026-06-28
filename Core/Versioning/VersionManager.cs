namespace Reforged.Core.Versioning;

/// <summary>
/// Centralized version policy helper. Applications read version state through Core instead of parsing files differently.
/// </summary>
public sealed class VersionManager
{
    public ProjectVersionInfo Current { get; }

    public VersionManager(ProjectVersionInfo current)
    {
        Current = current;
    }

    public bool IsAtLeastCodeUpdate(int requiredCodeUpdate)
    {
        return int.TryParse(Current.CodeUpdate, out var current) && current >= requiredCodeUpdate;
    }

    public string DisplayName => $"Reforged {Current.ProjectVersion} / CodeUpdate-{Current.CodeUpdate} / DatabaseVersion-{Current.DatabaseVersion}";
}
