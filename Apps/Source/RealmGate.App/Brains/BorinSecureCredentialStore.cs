using RealmGate.Client.Security;

namespace RealmGate.App.Brains;

public interface IBorinSecureCredentialStore
{
    Task<bool> HasPasswordAsync(string sessionId, CancellationToken token = default);
    Task SavePasswordAsync(string sessionId, string loginName, string password, CancellationToken token = default);
    Task<string?> ReadPasswordAsync(string sessionId, CancellationToken token = default);
    Task ClearPasswordAsync(string sessionId, CancellationToken token = default);
}

/// <summary>
/// Keeps Borin's external-server password in the operating system credential vault.
/// The password never enters repository files, Borin memory, journals, logs, or model prompts.
/// </summary>
public sealed class WindowsBorinSecureCredentialStore : IBorinSecureCredentialStore
{
    private readonly ISecureCredentialStore _credentials =
        PlatformSecureCredentialStore.CreateForCurrentPlatform();

    public async Task<bool> HasPasswordAsync(string sessionId, CancellationToken token = default)
    {
        if (!_credentials.IsAvailable)
            return false;
        return await _credentials.ContainsAsync(GetTargetName(sessionId), token).ConfigureAwait(false) ||
               await _credentials.ContainsAsync(GetLegacyTargetName(sessionId), token).ConfigureAwait(false);
    }

    public Task SavePasswordAsync(
        string sessionId,
        string loginName,
        string password,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Enter Borin's password before saving it securely.");
        return _credentials.SaveAsync(
            GetTargetName(sessionId),
            new SecureCredential(
                string.IsNullOrWhiteSpace(loginName) ? "borin" : loginName.Trim(),
                password),
            token);
    }

    public async Task<string?> ReadPasswordAsync(
        string sessionId,
        CancellationToken token = default)
    {
        if (!_credentials.IsAvailable)
            return null;

        var current = await _credentials.ReadAsync(GetTargetName(sessionId), token).ConfigureAwait(false);
        if (current is not null)
            return current.Secret;

        var legacy = await _credentials.ReadAsync(GetLegacyTargetName(sessionId), token).ConfigureAwait(false);
        if (legacy is null)
            return null;

        await _credentials.SaveAsync(GetTargetName(sessionId), legacy, token).ConfigureAwait(false);
        return legacy.Secret;
    }

    public async Task ClearPasswordAsync(string sessionId, CancellationToken token = default)
    {
        if (!_credentials.IsAvailable)
            return;
        await _credentials.DeleteAsync(GetTargetName(sessionId), token).ConfigureAwait(false);
        await _credentials.DeleteAsync(GetLegacyTargetName(sessionId), token).ConfigureAwait(false);
    }

    private static string GetTargetName(string sessionId) =>
        $"RealmGate/BorinPlayer/{NormalizeSessionId(sessionId)}";

    private static string GetLegacyTargetName(string sessionId) =>
        $"MajorMudReforged/BorinPlayer/{NormalizeSessionId(sessionId)}";

    private static string NormalizeSessionId(string sessionId)
    {
        var safe = new string((sessionId ?? string.Empty)
            .Where(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-')
            .Take(96)
            .ToArray());
        return safe.Length == 0 ? "borin-training-01" : safe;
    }
}
