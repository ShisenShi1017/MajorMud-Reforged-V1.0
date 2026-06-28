using MajorMud.Reforged.Core.Applications;

namespace MajorMud.Reforged.Launcher;

/// <summary>
/// Milestone 1 application shell. Runtime binaries will be produced into Bin by development/release tooling;
/// normal users must not need the .NET SDK to run MajorMud Reforged.
/// </summary>
public sealed class LauncherApplication : ReforgedApplicationBase
{
    public LauncherApplication()
        : base(new ApplicationIdentity(ApplicationRole.Launcher, "MajorMud Launcher", "Launcher shell for future Milestone 5 update/startup management work."))
    {
    }
}
