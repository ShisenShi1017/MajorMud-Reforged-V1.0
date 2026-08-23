using System.Text.Json;

namespace Reforged.Server;

public sealed class RuntimeClassicCommandCatalog
{
    private readonly IReadOnlyList<RuntimeCommandRegistration> _registrations;
    private readonly IReadOnlyDictionary<string, RuntimeCommandRegistration> _exactTokens;

    private RuntimeClassicCommandCatalog(
        IReadOnlyList<RuntimeCommandRegistration> registrations,
        IReadOnlyList<RuntimeGeneratedCommandEvidence> generatedEvidence,
        IReadOnlyList<string> diagnostics)
    {
        _registrations = registrations;
        GeneratedEvidence = generatedEvidence;
        Diagnostics = diagnostics;
        _exactTokens = registrations
            .SelectMany(registration => registration.Tokens.Select(token => (token, registration)))
            .GroupBy(entry => entry.token, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().registration, StringComparer.OrdinalIgnoreCase);
    }

    internal IReadOnlyList<RuntimeCommandRegistration> Registrations => _registrations;
    public IReadOnlyList<RuntimeGeneratedCommandEvidence> GeneratedEvidence { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public int ActiveCommandCount => _registrations.Count;
    public int GeneratedEvidenceCount => GeneratedEvidence.Count;
    public int ActivatedGeneratedCommandCount => GeneratedEvidence.Count(record => record.Activated);
    public int QuarantinedGeneratedCommandCount => GeneratedEvidence.Count(record => !record.Activated);

    public static RuntimeClassicCommandCatalog Load(string commandsPath)
    {
        var registrations = RuntimeCommand.DefaultRegistrations.ToList();
        var evidence = new List<RuntimeGeneratedCommandEvidence>();
        var diagnostics = new List<string>();

        if (!File.Exists(commandsPath))
        {
            diagnostics.Add($"Generated command catalog was not found: {commandsPath}");
            return new RuntimeClassicCommandCatalog(registrations, evidence, diagnostics);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(commandsPath));
            foreach (var record in EnumerateRecords(document.RootElement))
            {
                var name = ReadString(record, "name").Trim().ToLowerInvariant();
                if (name.Length == 0)
                {
                    diagnostics.Add("Generated command record without a name was quarantined.");
                    continue;
                }

                var status = ReadString(record, "status");
                var mappingStatus = record.TryGetProperty("runtime_mapping", out var mapping) && mapping.ValueKind == JsonValueKind.Object
                    ? ReadString(mapping, "status")
                    : string.Empty;
                var confidence = record.TryGetProperty("evidence_summary_0124", out var summary) && summary.ValueKind == JsonValueKind.Object
                    ? ReadString(summary, "confidence")
                    : string.Empty;

                var existing = registrations.FirstOrDefault(registration =>
                    registration.Canonical.Equals(name, StringComparison.OrdinalIgnoreCase));
                var authoritative = IsAuthoritative(status, mappingStatus, confidence);
                if (authoritative && existing is null)
                {
                    existing = new RuntimeCommandRegistration(name, Array.Empty<string>(), Math.Max(1, Math.Min(3, name.Length)));
                    registrations.Add(existing);
                }

                evidence.Add(new RuntimeGeneratedCommandEvidence(
                    name, status, mappingStatus, confidence, authoritative,
                    authoritative
                        ? "Evidence-backed command activated as authoritative runtime vocabulary."
                        : "Candidate command remains quarantined until parser evidence is confirmed."));
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            diagnostics.Add($"Generated command catalog could not be read: {commandsPath}: {ex.Message}");
        }

        return new RuntimeClassicCommandCatalog(registrations, evidence, diagnostics);
    }

    public RuntimeCommandResolution Resolve(string token)
    {
        if (_exactTokens.TryGetValue(token, out var exact))
        {
            return new RuntimeCommandResolution(exact.Canonical, string.Empty);
        }

        var possibleMatches = _registrations
            .Where(registration => registration.Tokens.Any(candidate => candidate.StartsWith(token, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var eligibleMatches = possibleMatches
            .Where(registration => token.Length >= registration.MinimumAbbreviationLength &&
                                   registration.Canonical.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            .Select(registration => registration.Canonical)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (eligibleMatches.Count == 0 && possibleMatches
                .Select(registration => registration.Canonical)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Skip(1)
                .Any())
        {
            eligibleMatches = possibleMatches
                .Select(registration => registration.Canonical)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return eligibleMatches.Count switch
        {
            0 => new RuntimeCommandResolution(token, string.Empty),
            1 => new RuntimeCommandResolution(eligibleMatches[0], string.Empty),
            _ => new RuntimeCommandResolution(token, $"Ambiguous command '{token}': {string.Join(", ", eligibleMatches)}.")
        };
    }

    public bool IsRegistered(string canonical) =>
        _registrations.Any(registration => registration.Canonical.Equals(canonical, StringComparison.OrdinalIgnoreCase));

    public bool IsMovement(string canonical) =>
        _registrations.Any(registration => registration.IsMovement && registration.Canonical.Equals(canonical, StringComparison.OrdinalIgnoreCase));

    private static bool IsAuthoritative(string status, string mappingStatus, string confidence)
    {
        if (status.Contains("requires", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("pending", StringComparison.OrdinalIgnoreCase) ||
            mappingStatus.Contains("pending", StringComparison.OrdinalIgnoreCase) ||
            confidence.Equals("unscored", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return status.Contains("proven", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("authoritative", StringComparison.OrdinalIgnoreCase) ||
               confidence.Equals("high", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<JsonElement> EnumerateRecords(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root.EnumerateArray();
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
            return records.EnumerateArray();
        return Array.Empty<JsonElement>();
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

public sealed record RuntimeGeneratedCommandEvidence(
    string Name,
    string Status,
    string RuntimeMappingStatus,
    string Confidence,
    bool Activated,
    string Reason);

internal sealed record RuntimeCommandRegistration(
    string Canonical,
    IReadOnlyList<string> Aliases,
    int MinimumAbbreviationLength,
    bool IsMovement = false)
{
    public IEnumerable<string> Tokens => new[] { Canonical }.Concat(Aliases);
}

public sealed record RuntimeCommandResolution(string Name, string Error);
