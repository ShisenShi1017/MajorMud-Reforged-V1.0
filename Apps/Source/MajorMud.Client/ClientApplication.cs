using MajorMud.Reforged.Core.Applications;

namespace MajorMud.Reforged.Client;

/// <summary>
/// Milestone 1 application shell. Runtime binaries will be produced into Bin by development/release tooling;
/// normal users must not need the .NET SDK to run MajorMud Reforged.
/// </summary>
public sealed class ClientApplication : ReforgedApplicationBase
{
    public ClientApplication()
        : base(new ApplicationIdentity(ApplicationRole.Client, "MajorMud Client", "Windows client shell for future Milestone 6 client work."))
    {
    }
}
