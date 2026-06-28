namespace Reforged.Core.Applications;

/// <summary>
/// Describes a Reforged application without depending on that application's project.
/// </summary>
public sealed class ApplicationIdentity
{
    public ApplicationIdentity(ApplicationRole role, string name, string description)
    {
        Role = role;
        Name = name;
        Description = description;
    }

    public ApplicationRole Role { get; }
    public string Name { get; }
    public string Description { get; }
}
