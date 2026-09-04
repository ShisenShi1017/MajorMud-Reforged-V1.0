using RealmGate.Core.Validation;

namespace RealmGate.Core.Health;

/// <summary>
/// SDK-free source audit that verifies app projects explicitly reference Core.
/// </summary>
public sealed class ApplicationCoreReferenceHealthCheck
{
    private static readonly string[] ExpectedApplications =
    {
        "RealmGate.Server",
        "RealmGate.Server.Runtime"
    };

    public ValidationResult Validate(string appsSourceFolder)
    {
        var result = new ValidationResult();

        foreach (var app in ExpectedApplications)
        {
            var projectPath = Path.Combine(appsSourceFolder, app, $"{app}.csproj");
            if (!File.Exists(projectPath))
            {
                result.Add(ValidationSeverity.Error, "APP_PROJECT_MISSING", $"Application project is missing: {app}", projectPath);
                continue;
            }

            var text = File.ReadAllText(projectPath);
            if (!text.Contains("Core/Source/RealmGate.Core/RealmGate.Core.csproj", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(ValidationSeverity.Error, "APP_CORE_REFERENCE_MISSING", $"Application does not reference Core: {app}", projectPath);
            }
        }

        return result;
    }
}
