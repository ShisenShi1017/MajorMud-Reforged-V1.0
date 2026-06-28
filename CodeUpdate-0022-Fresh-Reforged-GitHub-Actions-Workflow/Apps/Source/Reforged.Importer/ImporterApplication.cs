using Reforged.Core.Applications;

namespace Reforged.Importer;

/// <summary>
/// Milestone 1 application shell. Runtime binaries will be produced into Bin by development/release tooling;
/// normal users must not need the .NET SDK to run Reforged.
/// </summary>
public sealed class ImporterApplication : ReforgedApplicationBase
{
    public ImporterApplication()
        : base(new ApplicationIdentity(ApplicationRole.Importer, "Reforged Importer", "Native data import shell for future Milestone 2 DAT/CSV/MP import work."))
    {
    }
}
