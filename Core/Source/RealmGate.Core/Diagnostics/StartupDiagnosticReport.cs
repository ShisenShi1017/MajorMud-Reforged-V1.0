using RealmGate.Core.Validation;

namespace RealmGate.Core.Diagnostics;

public sealed class StartupDiagnosticReport
{
    private readonly List<string> checks = new();

    public IReadOnlyList<string> Checks => checks;

    public ValidationResult Validation { get; } = new();

    public bool Passed => !Validation.HasErrors;

    public void AddCheck(string name) => checks.Add(name);
}
