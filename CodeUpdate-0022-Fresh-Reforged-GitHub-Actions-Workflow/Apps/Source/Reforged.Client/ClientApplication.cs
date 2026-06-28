using Reforged.Core.Applications;

namespace Reforged.Client;

/// <summary>
/// Milestone 1 application shell. Runtime binaries will be produced into Bin by development/release tooling;
/// normal users must not need the .NET SDK to run Reforged.
/// </summary>
public sealed class ClientApplication : ReforgedApplicationBase
{
    public ClientApplication()
        : base(new ApplicationIdentity(ApplicationRole.Client, "Reforged Client", "Windows client shell for future Milestone 6 client work."))
    {
    }
}
