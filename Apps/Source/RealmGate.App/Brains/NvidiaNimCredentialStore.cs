using RealmGate.Client.Security;

namespace RealmGate.App.Brains;

public enum NvidiaNimKeySlot { Shared, Lightning, Ultra }

public interface INvidiaNimCredentialStore
{
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);
    Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<string?> ReadApiKeyAsync(CancellationToken cancellationToken = default);
    Task ClearApiKeyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores the NVIDIA NIM API key in the operating system credential vault. Callers can
/// query only whether a key exists; the UI must never bind the result of ReadApiKeyAsync.
/// </summary>
public sealed class WindowsNvidiaNimCredentialStore : INvidiaNimCredentialStore
{
    private readonly ISecureCredentialStore _credentials =
        PlatformSecureCredentialStore.CreateForCurrentPlatform();
    private readonly string _targetName;
    private readonly string _legacyTargetName;

    public WindowsNvidiaNimCredentialStore(NvidiaNimKeySlot slot = NvidiaNimKeySlot.Shared)
    {
        Slot = slot;
        _targetName = slot == NvidiaNimKeySlot.Ultra
            ? "RealmGate/Borin/NvidiaNim/Ultra"
            : "RealmGate/Borin/NvidiaNim";
        _legacyTargetName = slot == NvidiaNimKeySlot.Ultra
            ? "MajorMudReforged/Borin/NvidiaNim/Ultra"
            : "MajorMudReforged/Borin/NvidiaNim";
    }

    public NvidiaNimKeySlot Slot { get; }
    public string CredentialTargetName => _targetName;

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        if (!_credentials.IsAvailable)
            return false;
        return await _credentials.ContainsAsync(_targetName, cancellationToken).ConfigureAwait(false) ||
               await _credentials.ContainsAsync(_legacyTargetName, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("An NVIDIA NIM API key is required.", nameof(apiKey));
        return _credentials.SaveAsync(
            _targetName,
            new SecureCredential("Borin", apiKey.Trim()),
            cancellationToken);
    }

    public async Task<string?> ReadApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (!_credentials.IsAvailable)
            return null;
        var current = await _credentials.ReadAsync(_targetName, cancellationToken).ConfigureAwait(false);
        if (current is not null)
            return current.Secret;

        var legacy = await _credentials.ReadAsync(_legacyTargetName, cancellationToken).ConfigureAwait(false);
        if (legacy is null)
            return null;

        await _credentials.SaveAsync(_targetName, legacy, cancellationToken).ConfigureAwait(false);
        return legacy.Secret;
    }

    public async Task ClearApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (!_credentials.IsAvailable)
            return;
        await _credentials.DeleteAsync(_targetName, cancellationToken).ConfigureAwait(false);
        await _credentials.DeleteAsync(_legacyTargetName, cancellationToken).ConfigureAwait(false);
    }
}

public static class NvidiaNimCredentialMigration
{
    /// <summary>Copies a legacy tier credential into the shared slot only when the shared slot is empty.</summary>
    public static async Task<bool> EnsureSharedCredentialAsync(
        INvidiaNimCredentialStore shared,
        INvidiaNimCredentialStore legacyUltra,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shared);
        ArgumentNullException.ThrowIfNull(legacyUltra);
        if (await shared.IsConfiguredAsync(cancellationToken).ConfigureAwait(false))
            return false;

        var legacyKey = await legacyUltra.ReadApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(legacyKey))
            return false;

        await shared.SaveApiKeyAsync(legacyKey, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
