using Reforged.Core.Applications;

namespace Reforged.Launcher;

/// <summary>
/// Milestone 1 application shell. Runtime binaries will be produced into Bin by development/release tooling;
/// normal users must not need the .NET SDK to run Reforged.
/// </summary>
public sealed class LauncherApplication : ReforgedApplicationBase
{
    public LauncherApplication()
        : base(new ApplicationIdentity(ApplicationRole.Launcher, "Reforged Launcher", "Launcher shell for future Milestone 5 update/startup management work."))
    {
    }
}
