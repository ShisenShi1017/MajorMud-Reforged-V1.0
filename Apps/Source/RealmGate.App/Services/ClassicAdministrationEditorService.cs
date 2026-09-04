using System.IO;
using System.Text.Json;
using RealmGate.Server;

namespace RealmGate.App.Services;

public sealed record ClassicAdministrationCharacter(
    string CharacterId,
    string DisplayName,
    string AccountId,
    int Level,
    string Source);

public sealed record ClassicAdministrationEditorSnapshot(
    RuntimeClassicAdministrationPolicy Policy,
    IReadOnlyList<ClassicAdministrationCharacter> Characters,
    string ProjectRoot,
    string PolicyPath,
    IReadOnlyList<string> Warnings);

public sealed record ClassicAdministrationEditorSaveResult(
    RuntimeClassicAdministrationPolicy Policy,
    string PolicyPath);

public sealed class ClassicAdministrationEditorService
{
    private readonly string _projectRoot;
    private readonly IReforgedHostController? _host;

    public ClassicAdministrationEditorService(
        string? startDirectory = null,
        IReforgedHostController? host = null)
    {
        _projectRoot = ResolveProjectRoot(startDirectory);
        _host = host;
    }

    public string ProjectRoot => _projectRoot;

    public ClassicAdministrationEditorSnapshot Load()
    {
        var warnings = new List<string>();
        var policy = RuntimeClassicAdministrationPolicyStore.Load(_projectRoot, "classic");
        var characters = DiscoverCharacters(warnings);
        return new ClassicAdministrationEditorSnapshot(
            policy,
            characters,
            _projectRoot,
            RuntimeClassicAdministrationPolicyStore.PathFor(_projectRoot, "classic"),
            warnings);
    }

    public ClassicAdministrationEditorSaveResult Save(RuntimeClassicAdministrationPolicy policy)
    {
        RuntimeClassicAdministrationPolicyStore.Save(
            _projectRoot,
            policy,
            "RealmGate Classic editor");

        var path = RuntimeClassicAdministrationPolicyStore.PathFor(_projectRoot, "classic");
        if (!File.Exists(path))
        {
            throw new IOException($"Classic settings were not written to disk: {path}");
        }

        var verified = RuntimeClassicAdministrationPolicyStore.Load(_projectRoot, "classic");
        VerifyRoundTrip(policy, verified, path);
        return new ClassicAdministrationEditorSaveResult(verified, path);
    }

    private IReadOnlyList<ClassicAdministrationCharacter> DiscoverCharacters(List<string> warnings)
    {
        var characters = new Dictionary<string, ClassicAdministrationCharacter>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var character in new RuntimeCharacterService(_projectRoot).ListAllCharacters())
            {
                if (!character.WorldId.Equals("classic", StringComparison.OrdinalIgnoreCase)) continue;
                Merge(characters, new ClassicAdministrationCharacter(
                    character.CharacterId,
                    character.DisplayName,
                    character.AccountId,
                    character.Level,
                    "Runtime save"));
            }
        }
        catch (Exception exception)
        {
            warnings.Add("Validated runtime character loading failed; raw-save discovery was used: " + exception.Message);
        }

        DiscoverRawRuntimeCharacters(characters, warnings);
        DiscoverActiveSessions(characters, warnings);
        DiscoverLegacyCharacters(characters, warnings);

        return characters.Values
            .OrderBy(character => character.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(character => character.CharacterId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void DiscoverRawRuntimeCharacters(
        IDictionary<string, ClassicAdministrationCharacter> characters,
        ICollection<string> warnings)
    {
        var directory = Path.Combine(_projectRoot, "Saves", "Classic", "characters");
        if (!Directory.Exists(directory)) return;

        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var characterId = ReadString(root, "CharacterId");
                var displayName = ReadString(root, "DisplayName");
                var worldId = ReadString(root, "WorldId");
                var accountId = ReadString(root, "AccountId");
                var level = ReadInt(root, "Level");

                if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(displayName)) continue;
                if (string.IsNullOrWhiteSpace(worldId) && characterId.StartsWith("classic:", StringComparison.OrdinalIgnoreCase))
                    worldId = "classic";
                if (!string.Equals(worldId, "classic", StringComparison.OrdinalIgnoreCase)) continue;

                Merge(characters, new ClassicAdministrationCharacter(
                    characterId.Trim(),
                    displayName.Trim(),
                    accountId.Trim(),
                    level,
                    "Runtime save"));
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Skipped unreadable character save '{Path.GetFileName(path)}': {exception.Message}");
            }
        }
    }

    private void DiscoverActiveSessions(
        IDictionary<string, ClassicAdministrationCharacter> characters,
        ICollection<string> warnings)
    {
        if (_host is null) return;

        try
        {
            foreach (var session in _host.ActiveSessions)
            {
                if (!session.WorldId.Equals("classic", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrWhiteSpace(session.CharacterId) || string.IsNullOrWhiteSpace(session.CharacterName)) continue;

                Merge(characters, new ClassicAdministrationCharacter(
                    session.CharacterId,
                    session.CharacterName,
                    session.AccountId,
                    0,
                    "Active session"));
            }
        }
        catch (Exception exception)
        {
            warnings.Add("Active server sessions could not be read: " + exception.Message);
        }
    }

    private void DiscoverLegacyCharacters(
        IDictionary<string, ClassicAdministrationCharacter> characters,
        ICollection<string> warnings)
    {
        var savesRoot = Path.Combine(_projectRoot, "Saves");
        if (!Directory.Exists(savesRoot)) return;

        foreach (var path in Directory.EnumerateFiles(savesRoot, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var name = ReadString(root, "name");
                var race = ReadString(root, "race");
                var className = ReadString(root, "class");
                if (string.IsNullOrWhiteSpace(name) ||
                    (string.IsNullOrWhiteSpace(race) && string.IsNullOrWhiteSpace(className)))
                {
                    continue;
                }

                Merge(characters, new ClassicAdministrationCharacter(
                    RuntimeIdentity.CharacterId("classic", name),
                    name.Trim(),
                    string.Empty,
                    ReadInt(root, "level"),
                    "Legacy save"));
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Skipped unreadable legacy save '{Path.GetFileName(path)}': {exception.Message}");
            }
        }
    }

    private static void Merge(
        IDictionary<string, ClassicAdministrationCharacter> characters,
        ClassicAdministrationCharacter candidate)
    {
        if (!characters.TryGetValue(candidate.CharacterId, out var existing))
        {
            characters[candidate.CharacterId] = candidate;
            return;
        }

        var sources = existing.Source.Split(" + ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(candidate.Source.Split(" + ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        characters[candidate.CharacterId] = existing with
        {
            DisplayName = Prefer(candidate.DisplayName, existing.DisplayName),
            AccountId = Prefer(candidate.AccountId, existing.AccountId),
            Level = Math.Max(existing.Level, candidate.Level),
            Source = string.Join(" + ", sources)
        };
    }

    private static string Prefer(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();

    private static void VerifyRoundTrip(
        RuntimeClassicAdministrationPolicy requested,
        RuntimeClassicAdministrationPolicy verified,
        string path)
    {
        var requestedIds = NormalizeIds(requested.SysopCharacterIds);
        var verifiedIds = NormalizeIds(verified.SysopCharacterIds);
        var matches = requested.UnlimitedItemsEnabled == verified.UnlimitedItemsEnabled &&
                      requested.EveryoneCanUseSysGoto == verified.EveryoneCanUseSysGoto &&
                      requestedIds.SequenceEqual(verifiedIds, StringComparer.OrdinalIgnoreCase);
        if (!matches)
        {
            throw new IOException(
                "Classic settings were written but failed read-back verification. " +
                $"Review or remove the policy file and save again: {path}");
        }
    }

    private static string[] NormalizeIds(IEnumerable<string>? values) =>
        (values ?? Array.Empty<string>())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string ResolveProjectRoot(string? startDirectory)
    {
        var candidate = Path.GetFullPath(string.IsNullOrWhiteSpace(startDirectory)
            ? AppContext.BaseDirectory
            : startDirectory);

        if (File.Exists(Path.Combine(candidate, "ProjectManifest.json")) &&
            Directory.Exists(Path.Combine(candidate, "Apps")) &&
            Directory.Exists(Path.Combine(candidate, "Data")))
        {
            return candidate;
        }

        var report = new ServerBootstrap().Run(candidate);
        return report.ProjectRoot
            ?? throw new InvalidOperationException("The Classic editor could not locate the RealmGate repository root.");
    }

    private static string ReadString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value)) return string.Empty;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static int ReadInt(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)) return number;
        return 0;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
