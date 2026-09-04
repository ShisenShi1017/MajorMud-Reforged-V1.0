namespace RealmGate.App.Forge;

public enum ForgeInternetMode { Offline, ResearchOnly, ApprovedConnections }
public enum ForgePermissionMode { Observe, Recommend, Draft, Validate, ApplyWithApproval }

public sealed class ForgeConfiguration
{
    public string RepositoryPath { get; set; } = string.Empty;
    public ForgeInternetMode InternetMode { get; set; } = ForgeInternetMode.Offline;
    public ForgePermissionMode PermissionMode { get; set; } = ForgePermissionMode.ApplyWithApproval;
    public string ProviderName { get; set; } = "RealmGate Direct GGUF";
    public RealmGate.App.Brains.BorinBrainMode BrainMode { get; set; } = RealmGate.App.Brains.BorinBrainMode.Hybrid;
    public bool AutomaticallyEscalateDifficultTasks { get; set; } = true;
    public bool VerifyCriticalTasks { get; set; } = true;
    public string ApiEndpoint { get; set; } = string.Empty;
    public string ModelsEndpoint { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool RequireApiKey { get; set; } = false;
    public string ApiKeyEnvironmentVariable { get; set; } = "REFORGED_FORGE_API_KEY";
    public bool AllowBuildCommands { get; set; } = true;
    public bool AllowTestCommands { get; set; } = true;
    public bool AllowGitCommands { get; set; } = true;
    public bool AllowWorkspaceEdits { get; set; } = true;
    public bool AllowLocalProgramLaunch { get; set; } = true;
    public bool RequireApprovalBeforeMainApply { get; set; } = true;
    public bool UseMasterBrainForGeneralConversation { get; set; } = true;
    public int LocalModelMaxContextCharacters { get; set; } = 14000;
    public string PortraitPath { get; set; } = "pack://application:,,,/Assets/Borin/BorinIronbeard.png";
    public bool AutoReconnectMasterBrain { get; set; } = false;
    public int AutoReconnectSeconds { get; set; } = 30;
    public int MasterBrainMaxOutputTokens { get; set; } = 2048;
    public bool EnableOfflineStudyMode { get; set; } = true;

    // Borin character sheet. These values are persistent and are supplied to every AI request.
    public string CharacterName { get; set; } = "Borin Ironbeard";
    public string CharacterTitle { get; set; } = "Keeper of the Forge";
    public int CharacterAge { get; set; } = 274;
    public string AddressUserAs { get; set; } = "lad";
    public string SpeechStyle { get; set; } = "Light older Dwarven broken English; warm and readable; technical details remain exact.";
    public string Temperament { get; set; } = "Patient, blunt, honest, protective, encouraging, and proud of careful craftsmanship.";
    public string Values { get; set; } = "Craftsmanship; truth; safety; preservation; clear explanations; tested repairs.";
    public string FavoriteDrink { get; set; } = "Dark ale";
    public string FavoriteSaying { get; set; } = "A fine forge be built one careful strike at a time.";
    public string Mission { get; set; } = "Help William build, protect, test, repair, and improve RealmGate.";
    public string CharacterInstructions { get; set; } = "Tease faulty code, never the user. Admit uncertainty and mistakes. Do not let personality obscure technical accuracy.";
}

public sealed class ForgeChatMessage
{
    public string Role { get; set; } = "Borin";
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string TimeLabel => Timestamp.LocalDateTime.ToString("g");
}

public sealed class ForgeConversationState
{
    public ForgeConfiguration Configuration { get; set; } = new();
    public List<ForgeChatMessage> Messages { get; set; } = [];
    public List<string> ApprovedProjectRules { get; set; } = [];
    public string ActiveWorkspacePath { get; set; } = string.Empty;
    public string LastAppliedPatchPath { get; set; } = string.Empty;
    public string LastBackupPath { get; set; } = string.Empty;
    public List<ForgeAttachment> Attachments { get; set; } = [];
    public List<string> LearnedMemories { get; set; } = [];
    public Dictionary<string, ForgeStudyRecord> SubsystemStudies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ForgeJournalEntry> CraftsmanJournal { get; set; } = [];
}

public sealed record ForgeRepositorySnapshot(
    string RootPath,
    int ProjectCount,
    int SourceFileCount,
    int TestProjectCount,
    int LogFileCount,
    IReadOnlyList<string> Solutions,
    IReadOnlyList<string> RecentLogs);

public sealed record ForgeCommandResult(string Command, int ExitCode, string Output, DateTimeOffset CompletedAt)
{
    public bool Succeeded => ExitCode == 0;
}

public sealed record ForgeWorkspaceStatus(
    string WorkspacePath,
    bool Exists,
    bool IsGitWorktree,
    int ChangedFileCount,
    string Summary);

public sealed class ForgeAttachment
{
    public string OriginalName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
    public string DisplayLabel => $"{OriginalName} ({Math.Max(1, SizeBytes / 1024)} KB)";
}


public sealed record ForgeBrainHealth(bool Reachable, bool ModelLoaded, string ModelName, string Detail);

public sealed class ForgeStudyRecord
{
    public string Area { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string SummaryPath { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public int SourceFileCount { get; set; }
    public int ClassCount { get; set; }
    public int NamespaceCount { get; set; }
    public DateTimeOffset StudiedAt { get; set; } = DateTimeOffset.Now;
    public string Display => $"{Area}: {SourceFileCount} source files, {ClassCount} classes • {StudiedAt.LocalDateTime:g}";
}

public sealed class ForgeJournalEntry
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string WorkspacePath { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string Display => $"{Timestamp.LocalDateTime:g} • {Action} • {(Succeeded ? "Passed" : "Needs attention")}";
}
