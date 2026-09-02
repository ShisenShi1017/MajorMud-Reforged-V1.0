using Reforged.Core.Validation;

namespace Reforged.Core.Health;

/// <summary>
/// SDK-free source audit that verifies app projects explicitly reference Core.
/// </summary>
public sealed class ApplicationCoreReferenceHealthCheck
{
    private static readonly string[] ExpectedApplications =
    {
        "Reforged.Server",
        "Reforged.Server.Runtime"
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
            if (!text.Contains("Core/Source/Reforged.Core/Reforged.Core.csproj", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(ValidationSeverity.Error, "APP_CORE_REFERENCE_MISSING", $"Application does not reference Core: {app}", projectPath);
            }
        }

        return result;
    }
}
