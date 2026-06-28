namespace Reforged.Core.Applications;

/// <summary>
/// Minimal base class for application stubs and future compiled applications.
/// </summary>
public abstract class ReforgedApplicationBase : IReforgedApplication
{
    protected ReforgedApplicationBase(ApplicationIdentity identity)
    {
        Identity = identity;
    }

    public ApplicationIdentity Identity { get; }
}
