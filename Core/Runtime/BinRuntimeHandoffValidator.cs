namespace MajorMud.Reforged.Core.Runtime;

/// <summary>
/// Performs file-layout validation for runtime-ready application handoff artifacts.
/// This class does not build, invoke, or depend on the .NET SDK.
/// </summary>
public sealed class BinRuntimeHandoffValidator
{
    public IReadOnlyList<string> Validate(string projectRoot, IEnumerable<BinRuntimeRequirement> requirements)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return new[] { "Project root was not provided." };
        }

        var issues = new List<string>();
        foreach (var requirement in requirements)
        {
            var fullPath = Path.Combine(projectRoot, requirement.RelativeExecutablePath);
            if (!File.Exists(fullPath))
            {
                issues.Add($"Missing runtime artifact for {requirement.ApplicationName}: {requirement.RelativeExecutablePath}");
            }
        }

        return issues;
    }
}
