using System.Text.Json;
using RealmGate.Client.Gameplay;

namespace RealmGate.Client.Diagnostics;

public sealed record ClientSessionStatisticsSnapshot(
    DateTimeOffset TrackingStartedAtUtc,
    DateTimeOffset? ConnectedAtUtc,
    DateTimeOffset? LastActivityAtUtc,
    TimeSpan ConnectedDuration,
    bool IsConnected,
    string HostName,
    int Port,
    long ConnectionAttempts,
    long SuccessfulConnections,
    long Disconnects,
    long TransportErrors,
    long LinesSent,
    long SensitiveLinesSent,
    long RawWrites,
    long SentCharacters,
    long ReceivedCharacters,
    long VisibleTextChunks,
    long ProtocolMessages);

public sealed record ClientSupportSnapshot(
    string Product,
    string Version,
    DateTimeOffset GeneratedAtUtc,
    string PrivacyNotice,
    string EndpointScope,
    ClientSessionStatisticsSnapshot Session,
    IReadOnlyList<ClientDiagnosticCheck> Checks)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static ClientSupportSnapshot Create(
        string version,
        ClientSessionStatisticsSnapshot session,
        IReadOnlyList<ClientDiagnosticCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(checks);
        var endpointScope = session.HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                            session.HostName.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                            session.HostName.Equals("::1", StringComparison.OrdinalIgnoreCase)
            ? "local"
            : string.IsNullOrWhiteSpace(session.HostName) ? "not connected" : "remote";
        return new ClientSupportSnapshot(
            "RealmGate",
            string.IsNullOrWhiteSpace(version) ? "unknown" : version.Trim(),
            DateTimeOffset.UtcNow,
            "Passwords, command text, terminal conversation text, and raw network payloads are excluded.",
            endpointScope,
            session with { HostName = endpointScope },
            checks.ToArray());
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public async Task WriteAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(destinationPath, ToJson(), cancellationToken);
    }
}
