using System.Security.Cryptography;
using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeAccountRole
{
    Player,
    Moderator,
    Sysop,
    Administrator
}

public sealed record RuntimeAccountCredential(
    string AccountId,
    string PasswordHash,
    string Salt,
    int Iterations,
    RuntimeAccountRole Role,
    bool Enabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Version = 1);

public sealed class RuntimeAccountSecurityService
{
    private const int DefaultIterations = 210_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _securityRoot;
    private readonly object _gate = new();

    public RuntimeAccountSecurityService(string projectRoot)
    {
        _securityRoot = Path.Combine(RuntimeSavePaths.HostRoot(projectRoot), "account_security");
        Directory.CreateDirectory(_securityRoot);
    }

    public bool HasPassword(string accountId) => File.Exists(CredentialPath(accountId));

    public RuntimeAccountCredential? TryLoad(string accountId)
    {
        var path = CredentialPath(accountId);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<RuntimeAccountCredential>(File.ReadAllText(path), JsonOptions);
    }

    public RuntimeAccountCredential SetPassword(string accountId, string password, RuntimeAccountRole role = RuntimeAccountRole.Player)
    {
        ValidatePassword(password);
        lock (_gate)
        {
            var existing = TryLoad(accountId);
            var salt = RandomNumberGenerator.GetBytes(SaltLength);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, HashAlgorithmName.SHA256, HashLength);
            var now = DateTimeOffset.UtcNow;
            var credential = new RuntimeAccountCredential(
                RuntimeIdentity.Normalize(accountId, "account"),
                Convert.ToBase64String(hash),
                Convert.ToBase64String(salt),
                DefaultIterations,
                existing?.Role ?? role,
                existing?.Enabled ?? true,
                existing?.CreatedAtUtc ?? now,
                now);
            WriteAtomic(CredentialPath(accountId), credential);
            return credential;
        }
    }

    public bool VerifyPassword(string accountId, string password, out string failure)
    {
        failure = string.Empty;
        var credential = TryLoad(accountId);
        if (credential is null)
        {
            failure = "This account has not been password-protected yet.";
            return false;
        }
        if (!credential.Enabled)
        {
            failure = "This account is disabled.";
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(credential.Salt);
            var expected = Convert.FromBase64String(credential.PasswordHash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, credential.Iterations, HashAlgorithmName.SHA256, expected.Length);
            if (CryptographicOperations.FixedTimeEquals(actual, expected)) return true;
        }
        catch (FormatException)
        {
            failure = "The account security record is invalid.";
            return false;
        }

        failure = "Invalid password.";
        return false;
    }

    private string CredentialPath(string accountId) =>
        Path.Combine(_securityRoot, RuntimeIdentity.Normalize(accountId, "account") + ".json");

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Passwords must contain at least 8 characters.", nameof(password));
        if (password.Length > 128)
            throw new ArgumentException("Passwords cannot exceed 128 characters.", nameof(password));
    }

    private static void WriteAtomic(string path, RuntimeAccountCredential credential)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, JsonSerializer.Serialize(credential, JsonOptions));
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
    }
}
