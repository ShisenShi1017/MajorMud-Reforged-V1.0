using System.Collections.ObjectModel;

namespace Reforged.App.Models;

public enum CoordinationRole
{
    Unassigned,
    Leader,
    Follower,
    Tank,
    Healer,
    Support
}

[Flags]
public enum RemoteControlPermission
{
    None = 0,
    QueryStatus = 1 << 0,
    QueryLocation = 1 << 1,
    QueryInventory = 1 << 2,
    QueryExperience = 1 << 3,
    Invite = 1 << 4,
    Movement = 1 << 5,
    Combat = 1 << 6,
    Spellcasting = 1 << 7,
    ItemUse = 1 << 8,
    ExecuteApprovedMacros = 1 << 9,
    ExecuteRawCommands = 1 << 10,
    Disconnect = 1 << 11,
    ChangeSettings = 1 << 12
}

public sealed class ClientCoordinationProfile
{
    public string Name { get; set; } = "Default Party";
    public bool Enabled { get; set; }
    public bool AcceptChatCommands { get; set; } = true;
    public string CommandPrefix { get; set; } = "@do";
    public int MaximumQueuedCommandsPerSession { get; set; } = 10;
    public int MinimumDispatchDelayMs { get; set; } = 500;
    public bool SendAcknowledgements { get; set; } = true;
    public bool AutoResyncOnReconnect { get; set; } = true;
    public int AcknowledgementTimeoutSeconds { get; set; } = 15;
    public ObservableCollection<CoordinatedSession> Sessions { get; set; } = [];
    public ObservableCollection<TrustedController> TrustedControllers { get; set; } = [];
    public ObservableCollection<CoordinationActivity> RecentActivity { get; set; } = [];
}

public sealed class CoordinatedSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = "Session";
    public string CharacterName { get; set; } = string.Empty;
    public ClientTerminalViewMode TerminalViewMode { get; set; } = ClientTerminalViewMode.ClassicMegaMud;
    public CoordinationRole Role { get; set; }
    public bool Enabled { get; set; } = true;
    public bool FollowLeader { get; set; } = true;
    public bool AssistLeader { get; set; } = true;
    public bool Paused { get; set; }
    public int PendingCommands { get; set; }
    public string LastInstruction { get; set; } = string.Empty;
    public string LastResult { get; set; } = "Ready";
    public long LastSequence { get; set; }
    public long LastAcknowledgedSequence { get; set; }
    public DateTimeOffset? LastAcknowledgedAt { get; set; }
    public string LastController { get; set; } = string.Empty;
    public bool NeedsResync { get; set; }
}

public sealed class TrustedController
{
    public string PlayerName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public RemoteControlPermission Permissions { get; set; } = RemoteControlPermission.QueryStatus |
                                                                  RemoteControlPermission.QueryLocation |
                                                                  RemoteControlPermission.Movement |
                                                                  RemoteControlPermission.Combat |
                                                                  RemoteControlPermission.Spellcasting |
                                                                  RemoteControlPermission.ItemUse |
                                                                  RemoteControlPermission.ExecuteApprovedMacros;

    public bool AllowQueries { get => Has(RemoteControlPermission.QueryStatus | RemoteControlPermission.QueryLocation | RemoteControlPermission.QueryInventory | RemoteControlPermission.QueryExperience); set => Set(RemoteControlPermission.QueryStatus | RemoteControlPermission.QueryLocation | RemoteControlPermission.QueryInventory | RemoteControlPermission.QueryExperience, value); }
    public bool AllowMovement { get => Has(RemoteControlPermission.Movement); set => Set(RemoteControlPermission.Movement, value); }
    public bool AllowCombat { get => Has(RemoteControlPermission.Combat); set => Set(RemoteControlPermission.Combat, value); }
    public bool AllowSpellcasting { get => Has(RemoteControlPermission.Spellcasting); set => Set(RemoteControlPermission.Spellcasting, value); }
    public bool AllowItemUse { get => Has(RemoteControlPermission.ItemUse); set => Set(RemoteControlPermission.ItemUse, value); }
    public bool AllowMacros { get => Has(RemoteControlPermission.ExecuteApprovedMacros); set => Set(RemoteControlPermission.ExecuteApprovedMacros, value); }
    public bool AllowRawCommands { get => Has(RemoteControlPermission.ExecuteRawCommands); set => Set(RemoteControlPermission.ExecuteRawCommands, value); }
    public bool AllowDisconnect { get => Has(RemoteControlPermission.Disconnect); set => Set(RemoteControlPermission.Disconnect, value); }
    public bool AllowSettings { get => Has(RemoteControlPermission.ChangeSettings); set => Set(RemoteControlPermission.ChangeSettings, value); }

    private bool Has(RemoteControlPermission permission) => (Permissions & permission) == permission;
    private void Set(RemoteControlPermission permission, bool enabled) => Permissions = enabled ? Permissions | permission : Permissions & ~permission;
}

public sealed record CoordinationInstruction(
    string Source,
    string Target,
    string Command,
    bool IsLocal,
    DateTimeOffset CreatedAt);

public sealed record CoordinatedCommandRequest(
    string SessionId,
    string CharacterName,
    string Command,
    string Source,
    long Sequence,
    DateTimeOffset CreatedAt);

public sealed record CoordinationParseResult(
    bool Recognized,
    string Source,
    string Target,
    string Command,
    string Error);

public enum CoordinationResponseKind
{
    Acknowledgement,
    Refusal,
    QueryResponse,
    Failure
}

public sealed record CoordinationResponse(
    string SourceSessionId,
    string Recipient,
    CoordinationResponseKind Kind,
    string Message,
    DateTimeOffset CreatedAt);

public sealed record CoordinationQueryRequest(
    string SourceSessionId,
    string Sender,
    RemoteControlPermission Permission,
    string Query,
    DateTimeOffset CreatedAt);

public sealed record CoordinationActivity(DateTimeOffset Timestamp, string Source, string Target, string Action, string Result);
