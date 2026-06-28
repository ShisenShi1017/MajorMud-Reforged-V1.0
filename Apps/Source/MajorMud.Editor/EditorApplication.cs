using MajorMud.Reforged.Core.Applications;

namespace MajorMud.Reforged.Editor;

/// <summary>
/// Milestone 1 application shell. Runtime binaries will be produced into Bin by development/release tooling;
/// normal users must not need the .NET SDK to run MajorMud Reforged.
/// </summary>
public sealed class EditorApplication : ReforgedApplicationBase
{
    public EditorApplication()
        : base(new ApplicationIdentity(ApplicationRole.Editor, "MajorMud Editor", "Live editor shell for future Milestone 4 server-connected editing work."))
    {
    }
}
