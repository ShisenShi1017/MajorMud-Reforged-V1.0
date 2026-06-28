using System.Text.Json;

namespace Reforged.Core.Runtime;

/// <summary>
/// Loads the repository's declared Bin runtime artifact expectations.
/// The catalog is metadata only; it does not build or run SDK commands.
/// </summary>
public sealed class BinRuntimeHandoffCatalog
{
    public IReadOnlyList<BinRuntimeRequirement> LoadRequirements(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return Array.Empty<BinRuntimeRequirement>();
        }

        var catalogPath = Path.Combine(projectRoot, "Bin", "RuntimeArtifacts.json");
        if (!File.Exists(catalogPath))
        {
            return Array.Empty<BinRuntimeRequirement>();
        }

        using var stream = File.OpenRead(catalogPath);
        var document = JsonSerializer.Deserialize<RuntimeArtifactCatalogDocument>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return document?.Requirements?.Select(r => new BinRuntimeRequirement
        {
            ApplicationName = r.ApplicationName ?? string.Empty,
            RelativeExecutablePath = r.RelativeExecutablePath ?? string.Empty,
            RequiredForMilestoneOne = r.RequiredForMilestoneOne
        }).ToArray() ?? Array.Empty<BinRuntimeRequirement>();
    }

    private sealed class RuntimeArtifactCatalogDocument
    {
        public List<RuntimeArtifactRequirementDocument>? Requirements { get; set; }
    }

    private sealed class RuntimeArtifactRequirementDocument
    {
        public string? ApplicationName { get; set; }
        public string? RelativeExecutablePath { get; set; }
        public bool RequiredForMilestoneOne { get; set; }
    }
}
