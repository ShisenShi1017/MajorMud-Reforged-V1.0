using MajorMud.Reforged.Core.Applications;

namespace MajorMud.Reforged.Importer;

/// <summary>
/// Milestone 1 application shell. Runtime binaries will be produced into Bin by development/release tooling;
/// normal users must not need the .NET SDK to run MajorMud Reforged.
/// </summary>
public sealed class ImporterApplication : ReforgedApplicationBase
{
    public ImporterApplication()
        : base(new ApplicationIdentity(ApplicationRole.Importer, "MajorMud Importer", "Native data import shell for future Milestone 2 DAT/CSV/MP import work."))
    {
    }
}
