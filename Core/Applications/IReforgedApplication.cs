namespace RealmGate.Core.Applications;

/// <summary>
/// Common contract implemented by all top-level Reforged applications.
/// This keeps application bootstrapping consistent without making Core depend on any app project.
/// </summary>
public interface IReforgedApplication
{
    ApplicationIdentity Identity { get; }
}
