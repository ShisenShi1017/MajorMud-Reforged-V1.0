namespace Reforged.Core.Runtime;

/// <summary>
/// Performs file-layout validation for runtime-ready application handoff artifacts.
/// This class does not build, invoke, or depend on the .NET SDK.
/// </summary>
public sealed class BinRuntimeHandoffValidator
{
    public IReadOnlyList<string> Validate(string projectRoot, IEnumerable<BinRuntimeRequirement> requirements, bool milestoneOneOnly = false)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return new[] { "Project root was not provided." };
        }

        if (requirements is null)
        {
            return new[] { "Runtime artifact requirements were not provided." };
        }

        var issues = new List<string>();
        foreach (var requirement in requirements)
        {
            if (milestoneOneOnly && !requirement.RequiredForMilestoneOne)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(requirement.ApplicationName))
            {
                issues.Add("Runtime artifact requirement is missing an application name.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(requirement.RelativeExecutablePath))
            {
                issues.Add($"Runtime artifact requirement for {requirement.ApplicationName} is missing a relative executable path.");
                continue;
            }

            var fullPath = Path.Combine(projectRoot, requirement.RelativeExecutablePath);
            if (!File.Exists(fullPath))
            {
                issues.Add($"Missing runtime artifact for {requirement.ApplicationName}: {requirement.RelativeExecutablePath}");
            }
        }

        return issues;
    }

    public IReadOnlyList<string> ValidateDeclaredMilestoneOneArtifacts(string projectRoot)
    {
        var requirements = new BinRuntimeHandoffCatalog().LoadRequirements(projectRoot);
        return Validate(projectRoot, requirements, milestoneOneOnly: true);
    }
}
