using System.Text.RegularExpressions;

namespace Reforged.Server;

public enum RuntimeIdentityAccess { Player, Builder, Administrator, Host }
public enum RuntimeTitlePosition { Prefix, Suffix, GuildTag }
public enum RuntimeIdentityVisibility { Public, SelfAndAdministrators, AdministratorsOnly }

public sealed record RuntimeIdentityPolicy(
    int MinimumNameLength = 3,
    int MaximumNameLength = 20,
    string AllowedPattern = "^[A-Za-z][A-Za-z'-]*$",
    bool CaseInsensitiveUniqueness = true,
    IReadOnlyCollection<string>? ReservedNames = null,
    IReadOnlyCollection<string>? ReservedPrefixes = null);

public sealed record RuntimeTitleDefinition(
    string TitleId,
    string Text,
    RuntimeTitlePosition Position,
    string OwnerSubsystem,
    RuntimeIdentityAccess MinimumGrantAccess = RuntimeIdentityAccess.Administrator,
    RuntimeIdentityVisibility Visibility = RuntimeIdentityVisibility.Public,
    bool Enabled = true);

public sealed record RuntimeIdentityActor(string ActorId, RuntimeIdentityAccess Access, bool Authenticated = true);
public sealed record RuntimeIdentitySubject(
    string CharacterId,
    string Name,
    IReadOnlyCollection<string>? TitleIds = null,
    string? DisguiseName = null,
    bool HiddenIdentity = false,
    bool IsAdministrator = false);
public sealed record RuntimeIdentityPresentation(string CharacterId, string DisplayName, string CanonicalName, IReadOnlyList<string> AppliedTitles, bool Disguised, bool Hidden);
public sealed record RuntimeIdentityResult(bool Success, string Code, RuntimeIdentityPresentation? Presentation = null);

public sealed class RuntimeCharacterIdentityPolicyService
{
    private readonly RuntimeIdentityPolicy _policy;
    private readonly Dictionary<string, RuntimeTitleDefinition> _titles;
    private readonly HashSet<string> _reservedNames;
    private readonly IReadOnlyList<string> _reservedPrefixes;
    private readonly Regex _allowedName;

    public RuntimeCharacterIdentityPolicyService(RuntimeIdentityPolicy policy, IEnumerable<RuntimeTitleDefinition>? titles = null)
    {
        if (policy.MinimumNameLength < 1 || policy.MaximumNameLength < policy.MinimumNameLength || policy.MaximumNameLength > 64)
            throw new ArgumentException("identity-name-length-policy-invalid", nameof(policy));
        _policy = policy;
        _allowedName = new Regex(policy.AllowedPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        _reservedNames = new HashSet<string>(policy.ReservedNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        _reservedPrefixes = (policy.ReservedPrefixes ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        _titles = new(StringComparer.OrdinalIgnoreCase);
        foreach (var title in titles ?? Array.Empty<RuntimeTitleDefinition>())
        {
            ValidateTitle(title);
            if (!_titles.TryAdd(title.TitleId, title)) throw new ArgumentException($"duplicate-title:{title.TitleId}", nameof(titles));
        }
    }

    public RuntimeIdentityResult ValidateName(string requestedName, Func<string, bool>? nameExists = null)
    {
        var canonical = Canonicalize(requestedName);
        if (canonical.Length < _policy.MinimumNameLength) return new(false, "identity-name-too-short");
        if (canonical.Length > _policy.MaximumNameLength) return new(false, "identity-name-too-long");
        if (!_allowedName.IsMatch(canonical)) return new(false, "identity-name-characters-invalid");
        if (_reservedNames.Contains(canonical)) return new(false, "identity-name-reserved");
        if (_reservedPrefixes.Any(prefix => canonical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return new(false, "identity-name-prefix-reserved");
        if (nameExists is not null && nameExists(canonical)) return new(false, "identity-name-duplicate");
        return new(true, "identity-name-valid", new("", canonical, canonical, Array.Empty<string>(), false, false));
    }

    public RuntimeIdentityResult Present(RuntimeIdentityActor viewer, RuntimeIdentitySubject subject)
    {
        if (!viewer.Authenticated) return new(false, "identity-viewer-authentication-required");
        if (string.IsNullOrWhiteSpace(subject.CharacterId)) return new(false, "identity-character-required");
        var nameCheck = ValidateName(subject.Name);
        if (!nameCheck.Success) return new(false, "identity-stored-name-invalid");
        var privileged = viewer.Access >= RuntimeIdentityAccess.Administrator || string.Equals(viewer.ActorId, subject.CharacterId, StringComparison.OrdinalIgnoreCase);
        if (subject.HiddenIdentity && !privileged)
            return new(true, "identity-hidden", new(subject.CharacterId, "Someone", subject.Name, Array.Empty<string>(), false, true));

        var displayBase = !string.IsNullOrWhiteSpace(subject.DisguiseName) && !privileged ? CanonicalizePresentation(subject.DisguiseName) : subject.Name;
        var disguised = !string.Equals(displayBase, subject.Name, StringComparison.Ordinal);
        var applied = new List<RuntimeTitleDefinition>();
        foreach (var titleId in subject.TitleIds ?? Array.Empty<string>())
        {
            if (!_titles.TryGetValue(titleId, out var title) || !title.Enabled) continue;
            if (title.Visibility == RuntimeIdentityVisibility.AdministratorsOnly && viewer.Access < RuntimeIdentityAccess.Administrator) continue;
            if (title.Visibility == RuntimeIdentityVisibility.SelfAndAdministrators && !privileged) continue;
            applied.Add(title);
        }
        var prefixes = applied.Where(t => t.Position == RuntimeTitlePosition.Prefix).OrderBy(t => t.TitleId, StringComparer.OrdinalIgnoreCase).Select(t => t.Text);
        var suffixes = applied.Where(t => t.Position == RuntimeTitlePosition.Suffix).OrderBy(t => t.TitleId, StringComparer.OrdinalIgnoreCase).Select(t => t.Text);
        var tags = applied.Where(t => t.Position == RuntimeTitlePosition.GuildTag).OrderBy(t => t.TitleId, StringComparer.OrdinalIgnoreCase).Select(t => t.Text);
        var display = string.Join(" ", prefixes.Append(displayBase).Concat(suffixes).Concat(tags)).Trim();
        return new(true, "identity-presented", new(subject.CharacterId, display, subject.Name, applied.Select(t => t.TitleId).ToArray(), disguised, false));
    }

    public RuntimeIdentityResult AuthorizeTitleGrant(RuntimeIdentityActor actor, string ownerSubsystem, string titleId)
    {
        if (!actor.Authenticated) return new(false, "identity-authentication-required");
        if (!_titles.TryGetValue(titleId, out var title) || !title.Enabled) return new(false, "identity-title-not-found");
        if (!string.Equals(ownerSubsystem, title.OwnerSubsystem, StringComparison.OrdinalIgnoreCase)) return new(false, "identity-title-owner-mismatch");
        if (actor.Access < title.MinimumGrantAccess) return new(false, "identity-title-grant-denied");
        return new(true, "identity-title-grant-authorized");
    }

    public IReadOnlyList<string> ValidateCatalog()
    {
        var issues = new List<string>();
        foreach (var title in _titles.Values)
        {
            if (string.IsNullOrWhiteSpace(title.OwnerSubsystem)) issues.Add($"title-owner-required:{title.TitleId}");
            if (title.Text.Contains('\n') || title.Text.Contains('\r')) issues.Add($"title-rendering-invalid:{title.TitleId}");
        }
        return issues;
    }

    private static string Canonicalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = value.Trim();
        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }

    private static string CanonicalizePresentation(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void ValidateTitle(RuntimeTitleDefinition title)
    {
        if (string.IsNullOrWhiteSpace(title.TitleId) || string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(title.OwnerSubsystem))
            throw new ArgumentException("identity-title-invalid");
        if (title.Text.Contains('\n') || title.Text.Contains('\r')) throw new ArgumentException($"identity-title-rendering-invalid:{title.TitleId}");
    }
}
