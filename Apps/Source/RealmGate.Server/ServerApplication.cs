using RealmGate.Core.Applications;

namespace RealmGate.Server;

/// <summary>
/// Milestone 1 application shell. Runtime binaries will be produced into Bin by development/release tooling;
/// normal users must not need the .NET SDK to run Reforged.
/// </summary>
public sealed class ServerApplication : ReforgedApplicationBase
{
    public ServerApplication()
        : base(new ApplicationIdentity(ApplicationRole.Server, "RealmGate Server", "Standalone game server shell for future Milestone 3 world/session work."))
    {
    }
}
