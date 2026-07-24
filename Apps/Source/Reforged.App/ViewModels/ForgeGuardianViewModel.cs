using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using System.Windows.Input;
using System.Windows.Threading;
using Reforged.App.Commands;
using Reforged.App.Forge;

namespace Reforged.App.ViewModels;

public sealed class ForgeGuardianViewModel : ViewModelBase
{
    private readonly IForgeStateStore _store;
    private readonly IForgeRepositoryService _repository;
    private readonly IForgeBrain _brain;
    private readonly IForgeDeveloperTools _tools;
    private readonly IForgeBrainManager _brainManager;
    private ForgeConversationState _state = new();
    private ForgeRepositorySnapshot? _snapshot;
    private string _input = string.Empty;
    private string _status = "Warming the forge...";
    private bool _isBusy;
    private string _brainStatus = "Master Brain not tested";
    private readonly DispatcherTimer _brainHealthTimer;
    private CancellationTokenSource? _activeRequest;
    private string _currentTask = "Idle — awaiting a commission.";
    private string _taskStage = "Forge ready";
    private int _taskProgress;

    public ObservableCollection<ForgeChatMessage> Messages { get; } = [];
    public ObservableCollection<ForgeAttachment> Attachments { get; } = [];
    public string Input { get => _input; set { if (Set(ref _input, value)) ((AsyncRelayCommand)SendCommand).Raise(); } }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string BrainStatus { get => _brainStatus; private set { if (Set(ref _brainStatus, value)) Raise(nameof(ModeSummary)); } }
    public bool IsBusy { get => _isBusy; private set { if (Set(ref _isBusy, value)) Raise(nameof(IsReady)); } }
    public bool IsReady => !IsBusy;
    public ForgeConfiguration Configuration => _state.Configuration;
    public string RepositorySummary => _snapshot is null
        ? "Repository not scanned"
        : $"{_snapshot.ProjectCount} projects • {_snapshot.SourceFileCount} source/UI files • {_snapshot.TestProjectCount} tests";
    public string ModeSummary => $"{Configuration.ProviderName} • {BrainStatus} • {Configuration.PermissionMode}";
    public string KnowledgeSummary => $"{_state.SubsystemStudies.Count} studies • {_state.LearnedMemories.Count} memories • {_state.CraftsmanJournal.Count} journal entries";
    public string CurrentTask { get => _currentTask; private set => Set(ref _currentTask, value); }
    public string TaskStage { get => _taskStage; private set => Set(ref _taskStage, value); }
    public int TaskProgress { get => _taskProgress; private set => Set(ref _taskProgress, value); }

    public ICommand SendCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand BuildCommand { get; }
    public ICommand TestCommand { get; }
    public ICommand ReadLogsCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand ToolStatusCommand { get; }
    public ICommand CreateWorkspaceCommand { get; }
    public ICommand WorkspaceStatusCommand { get; }
    public ICommand ApplyProposedDiffCommand { get; }
    public ICommand BuildWorkspaceCommand { get; }
    public ICommand TestWorkspaceCommand { get; }
    public ICommand ShowDiffCommand { get; }
    public ICommand ApplyWorkspaceCommand { get; }
    public ICommand RollbackCommand { get; }
    public ICommand OpenSolutionCommand { get; }
    public ICommand DetectLmStudioCommand { get; }
    public ICommand TestBrainCommand { get; }
    public ICommand LoadModelsCommand { get; }
    public ICommand AttachFileCommand { get; }
    public ICommand ClearAttachmentsCommand { get; }
    public ICommand ChoosePortraitCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand StudyHostCommand { get; }
    public ICommand StudyRepositoryCommand { get; }
    public ICommand ImplementAttachedCommand { get; }

    public ForgeGuardianViewModel(IForgeStateStore store, IForgeRepositoryService repository, IForgeBrain brain, IForgeDeveloperTools tools, IForgeBrainManager brainManager)
    {
        _store = store;
        _repository = repository;
        _brain = brain;
        _tools = tools;
        _brainManager = brainManager;
        SendCommand = new AsyncRelayCommand(SendAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(Input));
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        BuildCommand = new AsyncRelayCommand(() => QuickAsync("Build the app"), () => !IsBusy);
        TestCommand = new AsyncRelayCommand(() => QuickAsync("Run tests"), () => !IsBusy);
        ReadLogsCommand = new AsyncRelayCommand(() => QuickAsync("Read the latest logs"), () => !IsBusy);
        ClearCommand = new AsyncRelayCommand(ClearAsync, () => !IsBusy);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => !IsBusy);
        ToolStatusCommand = new AsyncRelayCommand(() => ToolAsync("Inspecting the tool rack...", () => _tools.InspectToolsAsync(Configuration.RepositoryPath)), () => !IsBusy);
        CreateWorkspaceCommand = new AsyncRelayCommand(CreateWorkspaceAsync, () => !IsBusy);
        WorkspaceStatusCommand = new AsyncRelayCommand(WorkspaceStatusAsync, () => !IsBusy);
        ApplyProposedDiffCommand = new AsyncRelayCommand(() => ToolAsync("Checking the proposed patch...", () => _tools.ApplyLatestDiffToWorkspaceAsync(_state)), () => !IsBusy);
        BuildWorkspaceCommand = new AsyncRelayCommand(() => WorkspaceCommandAsync("Building inside the safe workshop...", () => _tools.BuildWorkspaceAsync(_state)), () => !IsBusy);
        TestWorkspaceCommand = new AsyncRelayCommand(() => WorkspaceCommandAsync("Testing inside the safe workshop...", () => _tools.TestWorkspaceAsync(_state)), () => !IsBusy);
        ShowDiffCommand = new AsyncRelayCommand(() => ToolAsync("Reading the workspace changes...", () => _tools.ShowDiffAsync(_state)), () => !IsBusy);
        ApplyWorkspaceCommand = new AsyncRelayCommand(() => ToolAsync("Applying the approved repair...", () => _tools.ApplyWorkspaceToMainAsync(_state)), () => !IsBusy);
        RollbackCommand = new AsyncRelayCommand(() => ToolAsync("Rolling back the last approved repair...", () => _tools.RollbackLastApplyAsync(_state)), () => !IsBusy);
        OpenSolutionCommand = new AsyncRelayCommand(() => ToolAsync("Opening the workshop plans...", () => _tools.OpenSolutionAsync(_state)), () => !IsBusy);
        DetectLmStudioCommand = new AsyncRelayCommand(() => ToolAsync("Looking for LM Studio...", () => _brainManager.DetectLmStudioAsync()), () => !IsBusy);
        TestBrainCommand = new AsyncRelayCommand(TestBrainAsync, () => !IsBusy);
        LoadModelsCommand = new AsyncRelayCommand(LoadModelsAsync, () => !IsBusy);
        AttachFileCommand = new AsyncRelayCommand(AttachFileAsync, () => !IsBusy);
        ClearAttachmentsCommand = new AsyncRelayCommand(ClearAttachmentsAsync, () => !IsBusy && Attachments.Count > 0);
        ChoosePortraitCommand = new AsyncRelayCommand(ChoosePortraitAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(CancelActiveRequest, () => IsBusy);
        StudyHostCommand = new AsyncRelayCommand(() => StudyAsync("Host and Server", "Apps/Source/Reforged.Server.Runtime"), () => !IsBusy);
        StudyRepositoryCommand = new AsyncRelayCommand(() => StudyAsync("Entire Repository", string.Empty), () => !IsBusy);
        ImplementAttachedCommand = new AsyncRelayCommand(ImplementAttachedCommissionAsync, () => !IsBusy && Attachments.Count > 0);
        _brainHealthTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _brainHealthTimer.Tick += async (_, _) => await RefreshBrainHealthAsync(silent: true);
    }

    public async Task InitializeAsync()
    {
        _state = await _store.LoadAsync();
        if (string.IsNullOrWhiteSpace(_state.Configuration.RepositoryPath))
            _state.Configuration.RepositoryPath = _repository.DiscoverRepositoryRoot();
        if (string.IsNullOrWhiteSpace(_state.Configuration.PortraitPath))
            _state.Configuration.PortraitPath = "pack://application:,,,/Assets/Borin/BorinIronbeard.png";
        Messages.Clear();
        foreach (var message in _state.Messages) Messages.Add(message);
        Attachments.Clear();
        foreach (var attachment in _state.Attachments) Attachments.Add(attachment);
        Raise(nameof(Configuration)); Raise(nameof(ModeSummary)); Raise(nameof(KnowledgeSummary));
        _brainHealthTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(Configuration.AutoReconnectSeconds, 10, 300));
        if (Configuration.AutoReconnectMasterBrain) _brainHealthTimer.Start();
        await ScanAsync();
        if (Configuration.AutoReconnectMasterBrain) await RefreshBrainHealthAsync(silent: true);
        RaiseCommandStates();
    }

    private async Task SendAsync()
    {
        var request = Input.Trim();
        Input = string.Empty;
        Add("You", request);
        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = request;
        TaskStage = "Understanding request";
        TaskProgress = 10;
        Status = "Forge is studying the request...";
        RaiseCommandStates();
        try
        {
            TaskStage = "Consulting Forge / Master Brain";
            TaskProgress = 45;
            var response = await _brain.RespondAsync(request, _state, _snapshot, _activeRequest.Token);
            Add("Borin", response);
            TaskStage = "Response complete";
            TaskProgress = 100;
            Status = "Ready by the hearth.";
        }
        catch (OperationCanceledException)
        {
            Add("Borin", "Aye, I stopped that task at yer word. No unapproved source change was made.");
            Status = "Task cancelled safely.";
        }
        catch (Exception ex)
        {
            Add("Borin", $"Bah, the hammer slipped: {ex.Message}. No source files were changed.");
            Status = "Request failed safely.";
        }
        finally
        {
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            await PersistAsync();
            RaiseCommandStates();
        }
    }

    private Task QuickAsync(string text) { Input = text; return SendAsync(); }

    private async Task ScanAsync()
    {
        IsBusy = true;
        Status = "Walking the repository halls...";
        try
        {
            _snapshot = await _repository.ScanAsync(Configuration.RepositoryPath);
            Status = "Repository mapped. No files changed.";
            Raise(nameof(RepositorySummary));
        }
        catch (Exception ex)
        {
            Status = $"Repository scan failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task ClearAsync()
    {
        _state.Messages.Clear(); Messages.Clear();
        Add("Borin", "Fresh slate, same workshop. What are we buildin' today?");
        await PersistAsync();
    }

    private async Task SaveSettingsAsync()
    {
        await PersistAsync();
        Raise(nameof(Configuration)); Raise(nameof(ModeSummary)); Raise(nameof(KnowledgeSummary));
        _brainHealthTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(Configuration.AutoReconnectSeconds, 10, 300));
        if (Configuration.AutoReconnectMasterBrain) _brainHealthTimer.Start(); else _brainHealthTimer.Stop();
        await ScanAsync();
        Add("Borin", "Aye, the settings are stored. API keys remain outside the file in the named Windows environment variable.");
        await PersistAsync();
    }


    private async Task TestBrainAsync()
    {
        IsBusy = true;
        Status = "Knocking upon the Master Brain door...";
        try
        {
            var result = await _brainManager.TestConnectionAsync(Configuration);
            Add("Borin", result);
            BrainStatus = result.Contains("answers true", StringComparison.OrdinalIgnoreCase)
                ? $"Connected: {Configuration.ModelName}"
                : "Connection fault";
            Status = BrainStatus.StartsWith("Connected", StringComparison.OrdinalIgnoreCase)
                ? "Master Brain connected."
                : "Master Brain test failed safely.";
            await PersistAsync();
        }
        catch (Exception ex)
        {
            Add("Borin", $"Nay, the Master Brain test failed: {ex.Message}");
            BrainStatus = "Disconnected";
            Status = "Master Brain test failed safely.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AttachFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Attach a file for Borin",
            Multiselect = true,
            Filter = "Supported files|*.txt;*.md;*.json;*.xml;*.yaml;*.yml;*.cs;*.xaml;*.csproj;*.sln;*.log;*.zip;*.patch;*.diff;*.ps1;*.bat;*.cmd|All files|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        var inbox = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MajorMUD Reforged",
            "ForgeGuardian",
            "Inbox");
        Directory.CreateDirectory(inbox);

        foreach (var source in dialog.FileNames)
        {
            var safeName = Path.GetFileName(source);
            var stored = Path.Combine(inbox, $"{DateTime.Now:yyyyMMdd_HHmmssfff}_{safeName}");
            File.Copy(source, stored, overwrite: false);
            var info = new FileInfo(stored);
            var attachment = new ForgeAttachment
            {
                OriginalName = safeName,
                StoredPath = stored,
                SizeBytes = info.Length,
                AddedAt = DateTimeOffset.Now
            };
            _state.Attachments.Add(attachment);
            Attachments.Add(attachment);
        }

        Add("Borin", $"Aye, {dialog.FileNames.Length} new scroll(s) be pinned to the workbench. I will read scripts as text only and will not execute attachments.");
        ((AsyncRelayCommand)ClearAttachmentsCommand).Raise();
        ((AsyncRelayCommand)ImplementAttachedCommand).Raise();
        await PersistAsync();
    }

    private async Task ClearAttachmentsAsync()
    {
        foreach (var attachment in _state.Attachments)
        {
            try
            {
                if (File.Exists(attachment.StoredPath))
                    File.Delete(attachment.StoredPath);
            }
            catch { }
        }

        _state.Attachments.Clear();
        Attachments.Clear();
        Add("Borin", "The attachment bench be cleared.");
        ((AsyncRelayCommand)ClearAttachmentsCommand).Raise();
        ((AsyncRelayCommand)ImplementAttachedCommand).Raise();
        await PersistAsync();
    }

    private async Task ChoosePortraitAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose Borin portrait",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files|*.*"
        };
        if (dialog.ShowDialog() != true)
            return;

        var artFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MajorMUD Reforged",
            "ForgeGuardian",
            "Art");
        Directory.CreateDirectory(artFolder);
        var destination = Path.Combine(artFolder, Path.GetFileName(dialog.FileName));
        File.Copy(dialog.FileName, destination, overwrite: true);
        Configuration.PortraitPath = destination;
        Raise(nameof(Configuration));
        Add("Borin", "Aye, that portrait suits the old beard better. The Forge will use it from now on.");
        await PersistAsync();
    }

    private async Task LoadModelsAsync()
    {
        IsBusy = true; Status = "Reading models from LM Studio...";
        try
        {
            var models = await _brainManager.ListModelsAsync(Configuration);
            if (models.Count == 0) Add("Borin", "The server answered, but no model be loaded. Choose a coding model in LM Studio first.");
            else
            {
                if (string.IsNullOrWhiteSpace(Configuration.ModelName)) Configuration.ModelName = models[0];
                Add("Borin", $"Aye, I found these local brains: {string.Join(", ", models)}\n\nSelected: `{Configuration.ModelName}`");
                Raise(nameof(Configuration)); Raise(nameof(ModeSummary));
                await PersistAsync();
            }
            BrainStatus = models.Count == 0 ? "No model loaded" : $"Model found: {Configuration.ModelName}";
            Status = "Local models inspected.";
        }
        catch (Exception ex)
        {
            Add("Borin", $"Nay, I could not read the local models: {ex.Message}");
            BrainStatus = "Disconnected";
            Status = "Brain discovery failed safely.";
        }
        finally { IsBusy = false; }
    }

    private async Task CreateWorkspaceAsync()
    {
        IsBusy = true; Status = "Preparing a safe Git workshop...";
        try
        {
            var result = await _tools.CreateWorkspaceAsync(_state);
            Add("Borin", result.Exists ? $"Aye, a safe workshop be ready at `{result.WorkspacePath}`. Main repository remains untouched." : result.Summary);
            Status = result.Exists ? "Safe workspace ready." : "Workspace creation failed safely.";
        }
        catch (Exception ex) { Add("Borin", $"Nay, the workshop could not be raised: {ex.Message}"); Status = "Workspace creation failed safely."; }
        finally { IsBusy = false; await PersistAsync(); }
    }

    private async Task WorkspaceStatusAsync()
    {
        IsBusy = true; Status = "Inspecting the safe workshop...";
        try
        {
            var result = await _tools.GetWorkspaceStatusAsync(_state);
            Add("Borin", $"Workspace: `{result.WorkspacePath}`\nExists: {result.Exists}\nChanged files: {result.ChangedFileCount}\n\n{result.Summary}");
            Status = "Workspace inspected.";
        }
        catch (Exception ex) { Add("Borin", ex.Message); Status = "Workspace inspection failed safely."; }
        finally { IsBusy = false; await PersistAsync(); }
    }

    private async Task ToolAsync(string busyText, Func<Task<string>> action)
    {
        IsBusy = true; Status = busyText;
        try { Add("Borin", await action()); Status = "Ready by the hearth."; }
        catch (Exception ex) { Add("Borin", $"Bah, the tool slipped: {ex.Message}. No unapproved main-repository change was made."); Status = "Tool action failed safely."; }
        finally { IsBusy = false; await PersistAsync(); }
    }

    private async Task WorkspaceCommandAsync(string busyText, Func<Task<ForgeCommandResult>> action)
    {
        IsBusy = true; Status = busyText;
        try
        {
            var result = await action();
            var tail = result.Output.Length > 16000 ? result.Output[^16000..] : result.Output;
            Add("Borin", $"`{result.Command}` finished with exit code {result.ExitCode}.\n\n{tail}\n\nThis ran only inside the Forge workspace.");
            Status = result.Succeeded ? "Workspace command passed." : "Workspace command found faults.";
            RecordJournal(result.Command, tail, result.Succeeded);
        }
        catch (Exception ex) { Add("Borin", $"Nay, the workspace command failed: {ex.Message}"); Status = "Workspace command failed safely."; }
        finally { IsBusy = false; await PersistAsync(); }
    }

    private async Task RefreshBrainHealthAsync(bool silent)
    {
        if (IsBusy || !Configuration.AutoReconnectMasterBrain) return;
        var health = await _brainManager.GetHealthAsync(Configuration);
        if (health.Reachable && health.ModelLoaded)
        {
            if (string.IsNullOrWhiteSpace(Configuration.ModelName)) Configuration.ModelName = health.ModelName;
            BrainStatus = $"Connected: {Configuration.ModelName}";
            if (!silent) Add("Borin", $"Aye, the Master Brain be awake through `{Configuration.ModelName}`.");
        }
        else if (health.Reachable)
        {
            BrainStatus = "LM Studio awake; no model loaded";
        }
        else
        {
            BrainStatus = "Sleeping — offline Forge Brain active";
        }
        Raise(nameof(ModeSummary));
    }

    private void CancelActiveRequest()
    {
        _activeRequest?.Cancel();
        Status = "Stopping the current task...";
    }

    private async Task StudyAsync(string area, string relativePath)
    {
        if (!Configuration.EnableOfflineStudyMode)
        {
            Add("Borin", "Offline Study Mode be disabled in Settings.");
            return;
        }
        IsBusy = true;
        Status = $"Studying {area} without waking the Master Brain...";
        RaiseCommandStates();
        try
        {
            var record = await _repository.StudyAsync(Configuration.RepositoryPath, area, relativePath);
            _state.SubsystemStudies[area] = record;
            var memory = $"Studied {area} on {record.StudiedAt:g}: {record.SourceFileCount} source/UI files, {record.ClassCount} types, {record.NamespaceCount} namespaces. Summary: {record.SummaryPath}";
            _state.LearnedMemories.RemoveAll(x => x.StartsWith($"Studied {area} ", StringComparison.OrdinalIgnoreCase));
            _state.LearnedMemories.Add(memory);
            RecordJournal($"Study {area}", memory, true);
            Add("Borin", $"Aye, I studied **{area}** whilst staying offline. I mapped {record.SourceFileCount} source/UI files, {record.ClassCount} types, and {record.NamespaceCount} namespaces. The study ledger be stored at `{record.SummaryPath}`.");
            Status = $"{area} study complete.";
            Raise(nameof(KnowledgeSummary));
            await PersistAsync();
        }
        catch (Exception ex)
        {
            RecordJournal($"Study {area}", ex.Message, false);
            Add("Borin", $"Nay, the study was interrupted: {ex.Message}");
            Status = "Study failed safely.";
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }


    private async Task ImplementAttachedCommissionAsync()
    {
        if (Attachments.Count == 0)
        {
            Add("Borin", "No commission scroll be attached. Pin a handoff, log, patch, or design file to the workbench first.");
            return;
        }

        IsBusy = true;
        _activeRequest?.Dispose();
        _activeRequest = new CancellationTokenSource();
        CurrentTask = $"Implement attached commission: {Attachments[0].OriginalName}";
        TaskStage = "Creating safe workspace";
        TaskProgress = 8;
        Status = "Raising a protected workshop for the commission...";
        RaiseCommandStates();

        try
        {
            var workspace = await _tools.CreateWorkspaceAsync(_state, _activeRequest.Token);
            if (!workspace.Exists)
            {
                Add("Borin", workspace.Summary);
                Status = "Workspace creation failed safely.";
                TaskStage = "Stopped — no workspace";
                return;
            }

            Add("Borin", $"Aye, the protected workshop be ready at `{workspace.WorkspacePath}`. I will keep the main repository untouched.");
            TaskStage = "Inspecting attachment and repository";
            TaskProgress = 22;

            var prompt = """
Implement the attached commission in the safe workspace. First inspect the attached file and current repository context. Return a complete unified Git diff only for the smallest architecture-consistent implementation. Preserve existing behavior, never modify generated Classic data, and include tests or validation changes where appropriate.
""";

            TaskStage = "Consulting Master Brain";
            TaskProgress = 40;
            var response = await _brain.RespondAsync(prompt, _state, _snapshot, _activeRequest.Token);
            Add("Borin", response);

            TaskStage = "Applying proposed diff to workspace";
            TaskProgress = 58;
            var applyResult = await _tools.ApplyLatestDiffToWorkspaceAsync(_state, _activeRequest.Token);
            Add("Borin", applyResult);

            if (applyResult.Contains("could not", StringComparison.OrdinalIgnoreCase) ||
                applyResult.Contains("no fenced", StringComparison.OrdinalIgnoreCase) ||
                applyResult.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                Status = "A proposed diff was not safely applicable.";
                TaskStage = "Waiting for review";
                TaskProgress = 65;
                return;
            }

            TaskStage = "Building workspace";
            TaskProgress = 72;
            var build = await _tools.BuildWorkspaceAsync(_state, _activeRequest.Token);
            Add("Borin", $"Workspace build exit code: {build.ExitCode}\n\n{(build.Output.Length > 12000 ? build.Output[^12000..] : build.Output)}");
            RecordJournal("Implement attached commission — build", build.Output, build.Succeeded);

            if (!build.Succeeded)
            {
                Status = "Workspace build found faults. Main repository remains untouched.";
                TaskStage = "Build needs repair";
                TaskProgress = 78;
                return;
            }

            TaskStage = "Testing workspace";
            TaskProgress = 86;
            var tests = await _tools.TestWorkspaceAsync(_state, _activeRequest.Token);
            Add("Borin", $"Workspace tests exit code: {tests.ExitCode}\n\n{(tests.Output.Length > 12000 ? tests.Output[^12000..] : tests.Output)}");
            RecordJournal("Implement attached commission — tests", tests.Output, tests.Succeeded);

            TaskStage = tests.Succeeded ? "Ready for diff review and approval" : "Tests need attention";
            TaskProgress = tests.Succeeded ? 100 : 92;
            Status = tests.Succeeded
                ? "Commission forged in the safe workspace. Review the diff before applying."
                : "Code built, but tests found faults. Main repository remains untouched.";
            Add("Borin", tests.Succeeded
                ? "Aye, lad. The attached commission be forged and tested in the safe workshop. Use **Show Workspace Diff**, then **Apply Approved Patch** only after ye approve every change."
                : "The build be sound, but the trials found trouble. I kept every change in the safe workshop for review.");
        }
        catch (OperationCanceledException)
        {
            Add("Borin", "Aye, the commission be halted. No unapproved main-repository file was changed.");
            Status = "Commission cancelled safely.";
            TaskStage = "Cancelled";
        }
        catch (Exception ex)
        {
            Add("Borin", $"Nay, the commission struck a fault: {ex.Message}. The main repository remains untouched.");
            Status = "Commission failed safely.";
            TaskStage = "Failed safely";
        }
        finally
        {
            _activeRequest?.Dispose();
            _activeRequest = null;
            IsBusy = false;
            await PersistAsync();
            RaiseCommandStates();
        }
    }

    private void RecordJournal(string action, string result, bool succeeded)
    {
        _state.CraftsmanJournal.Add(new ForgeJournalEntry
        {
            Action = action,
            Result = result.Length > 4000 ? result[..4000] : result,
            Succeeded = succeeded,
            WorkspacePath = _state.ActiveWorkspacePath,
            Timestamp = DateTimeOffset.Now
        });
        while (_state.CraftsmanJournal.Count > 500) _state.CraftsmanJournal.RemoveAt(0);
        Raise(nameof(KnowledgeSummary));
    }

    private void RaiseCommandStates()
    {
        ((AsyncRelayCommand)SendCommand).Raise();
        ((RelayCommand)CancelCommand).Raise();
        ((AsyncRelayCommand)ImplementAttachedCommand).Raise();
    }

    private void Add(string role, string text)
    {
        var message = new ForgeChatMessage { Role = role, Text = text, Timestamp = DateTimeOffset.Now };
        _state.Messages.Add(message); Messages.Add(message);
        while (_state.Messages.Count > 500) { _state.Messages.RemoveAt(0); if (Messages.Count > 0) Messages.RemoveAt(0); }
    }

    private Task PersistAsync() => _store.SaveAsync(_state);
}
