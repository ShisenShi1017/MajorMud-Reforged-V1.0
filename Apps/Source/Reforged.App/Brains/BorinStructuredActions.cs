using System.Text.Json;
using Reforged.App.Forge;

namespace Reforged.App.Brains;

public enum BorinAiIntent
{
    None,
    Speak,
    ConnectAndLogin,
    Disconnect,
    LookRoom,
    Move,
    Inventory,
    Equipment,
    Attack,
    Cast,
    Interact,
    Shop,
    Quest,
    Pathfind,
    GameCommand,
    MonitorWorld,
    InspectRoom,
    InspectExit,
    InspectMonster,
    InspectItem,
    InspectScript,
    InspectTrigger,
    ReportIssue,
    ProposeWorldChange,
    ApplyTestWorldChange,
    ResetTestRealm,
    SearchCode,
    ReadFile,
    ReadDirectory,
    SearchReferences,
    ReadBuildErrors,
    ReadRuntimeLogs,
    ReadCrashLogs,
    ReadTestResults,
    ProposePatch,
    PreviewPatch,
    ApplyPatch,
    IncorporatePatch,
    RevertPatch,
    Build,
    Test,
    RunSpecificTest,
    LaunchTestServer,
    StopTestServer,
    InspectRuntime,
    CompareBeforeAfter,
    CreateIssue,
    UpdateIssue,
    GenerateRepairReport
}

public sealed record BorinAiDecision(
    string? Speech,
    BorinAiIntent Intent,
    string? Target,
    double Confidence,
    string Reason,
    string StateFingerprint)
{
    public bool RequestsAction => Intent is not BorinAiIntent.None and not BorinAiIntent.Speak;
}

public static class BorinAiDecisionParser
{
    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        "speech", "intent", "target", "confidence", "reason", "stateFingerprint"
    };

    public static bool TryParse(string? json, out BorinAiDecision? decision, out string error)
    {
        decision = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The provider returned an empty structured decision.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Fail("The structured decision must be one JSON object.", out decision, out error);
            foreach (var property in root.EnumerateObject())
                if (!AllowedProperties.Contains(property.Name))
                    return Fail($"Unknown structured decision field '{property.Name}'.", out decision, out error);

            if (!TryReadNullableString(root, "speech", 1000, out var speech, out error) ||
                !TryReadRequiredString(root, "intent", 64, out var intentText, out error) ||
                !TryReadNullableString(root, "target", 500, out var target, out error) ||
                !TryReadNumber(root, "confidence", out var confidence, out error) ||
                !TryReadRequiredString(root, "reason", 1200, out var reason, out error) ||
                !TryReadRequiredString(root, "stateFingerprint", 128, out var fingerprint, out error))
                return false;

            if (!Enum.TryParse<BorinAiIntent>(intentText, ignoreCase: true, out var intent) || !Enum.IsDefined(intent))
                return Fail($"Unknown structured intent '{intentText}'.", out decision, out error);
            if (confidence is < 0 or > 1)
                return Fail("Decision confidence must be between 0 and 1.", out decision, out error);
            if (intent is BorinAiIntent.None or BorinAiIntent.Speak && string.IsNullOrWhiteSpace(speech))
                return Fail("A non-action response requires speech.", out decision, out error);

            decision = new BorinAiDecision(speech, intent, target, confidence, reason, fingerprint);
            return true;
        }
        catch (JsonException exception)
        {
            error = $"The provider returned invalid decision JSON: {exception.Message}";
            return false;
        }
    }

    private static bool TryReadRequiredString(JsonElement root, string name, int maximum, out string value, out string error)
    {
        value = string.Empty;
        error = string.Empty;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            error = $"Structured decision field '{name}' must be a string.";
            return false;
        }
        value = property.GetString()?.Trim() ?? string.Empty;
        if (value.Length == 0 || value.Length > maximum)
        {
            error = $"Structured decision field '{name}' must contain 1-{maximum} characters.";
            return false;
        }
        return true;
    }

    private static bool TryReadNullableString(JsonElement root, string name, int maximum, out string? value, out string error)
    {
        value = null;
        error = string.Empty;
        if (!root.TryGetProperty(name, out var property))
        {
            error = $"Structured decision field '{name}' is required and may be null.";
            return false;
        }
        if (property.ValueKind == JsonValueKind.Null) return true;
        if (property.ValueKind != JsonValueKind.String)
        {
            error = $"Structured decision field '{name}' must be a string or null.";
            return false;
        }
        value = property.GetString()?.Trim();
        if (value?.Length > maximum)
        {
            error = $"Structured decision field '{name}' exceeds {maximum} characters.";
            return false;
        }
        return true;
    }

    private static bool TryReadNumber(JsonElement root, string name, out double value, out string error)
    {
        value = 0;
        error = string.Empty;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number || !property.TryGetDouble(out value) || !double.IsFinite(value))
        {
            error = $"Structured decision field '{name}' must be a finite number.";
            return false;
        }
        return true;
    }

    private static bool Fail(string message, out BorinAiDecision? decision, out string error)
    {
        decision = null;
        error = message;
        return false;
    }
}

public sealed record BorinEngineeringProposal(
    string Diagnosis,
    string Artifact,
    double Confidence,
    string Reason,
    string StateFingerprint)
{
    public string Render() => $"{Diagnosis.Trim()}\n\n{Artifact.Trim()}\n\nConfidence: {Confidence:0.00}\nVerification rationale: {Reason.Trim()}";
}

public static class BorinEngineeringProposalParser
{
    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        "diagnosis", "artifact", "confidence", "reason", "stateFingerprint"
    };

    public static bool TryParse(string? json, out BorinEngineeringProposal? proposal, out string error)
    {
        proposal = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json)) { error = "The provider returned an empty engineering proposal."; return false; }
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { error = "The engineering proposal must be one JSON object."; return false; }
            foreach (var property in root.EnumerateObject())
                if (!AllowedProperties.Contains(property.Name)) { error = $"Unknown engineering proposal field '{property.Name}'."; return false; }
            if (!ReadString(root, "diagnosis", 4000, out var diagnosis, out error) ||
                !ReadString(root, "artifact", 100000, out var artifact, out error) ||
                !ReadString(root, "reason", 1200, out var reason, out error) ||
                !ReadString(root, "stateFingerprint", 128, out var fingerprint, out error)) return false;
            if (!root.TryGetProperty("confidence", out var confidenceNode) || !confidenceNode.TryGetDouble(out var confidence) || !double.IsFinite(confidence) || confidence is < 0 or > 1)
            { error = "Engineering proposal confidence must be a finite number between 0 and 1."; return false; }
            if (!artifact.Contains("```diff", StringComparison.Ordinal))
            { error = "Engineering proposal artifact must contain one fenced unified diff."; return false; }
            proposal = new(diagnosis, artifact, confidence, reason, fingerprint);
            return true;
        }
        catch (JsonException exception) { error = $"The provider returned invalid engineering proposal JSON: {exception.Message}"; return false; }
    }

    private static bool ReadString(JsonElement root, string name, int maximum, out string value, out string error)
    {
        value = string.Empty; error = string.Empty;
        if (!root.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.String)
        { error = $"Engineering proposal field '{name}' must be a string."; return false; }
        value = node.GetString()?.Trim() ?? string.Empty;
        if (value.Length == 0 || value.Length > maximum)
        { error = $"Engineering proposal field '{name}' must contain 1-{maximum} characters."; return false; }
        return true;
    }
}

public sealed record BorinActionValidationResult(bool Allowed, string Code, string Reason)
{
    public static BorinActionValidationResult Permit(string reason) => new(true, "allowed", reason);
    public static BorinActionValidationResult Reject(string code, string reason) => new(false, code, reason);
}

public static class BorinPlayerCommandPolicy
{
    private static readonly HashSet<string> ProtectedRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYSOP", "GAMEOP", "ADMIN", "ACCOUNT", "PASSWORD", "PASSWD", "USER-ID", "USERID",
        "SCRIPT", "MACRO", "EXEC", "SHELL", "POWERSHELL", "CMD"
    };

    public static bool IsProtectedControlCommand(string? command)
    {
        var normalized = command?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Contains('\r') || normalized.Contains('\n') || normalized.IndexOfAny(['\0', '\u001b']) >= 0)
            return true;
        var root = normalized.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return ProtectedRoots.Contains(root) || root.StartsWith('!') || root.StartsWith('/');
    }
}

public interface IBorinActionValidator
{
    BorinActionValidationResult Validate(BorinAiDecision decision, BorinProviderContext requestedContext, BrainContext currentContext);
}

/// <summary>
/// Validates proposed actions only. It does not translate or execute them.
/// Existing terminal, editor, and engineering gateways remain the final authorities.
/// </summary>
public sealed class BorinActionValidator(
    TimeProvider? timeProvider = null,
    IBorinContextBuilder? contextBuilder = null) : IBorinActionValidator
{
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly IBorinContextBuilder _contextBuilder = contextBuilder ?? new BorinContextBuilder();
    private readonly object _gate = new();
    private string _lastSignature = string.Empty;
    private DateTimeOffset _lastAcceptedAt = DateTimeOffset.MinValue;

    public BorinActionValidationResult Validate(BorinAiDecision decision, BorinProviderContext requestedContext, BrainContext currentContext)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(requestedContext);
        ArgumentNullException.ThrowIfNull(currentContext);
        var game = currentContext.Conversation.GameState;
        var configuration = currentContext.Conversation.State.Configuration;

        if (!string.Equals(decision.StateFingerprint, requestedContext.StateFingerprint, StringComparison.Ordinal))
            return BorinActionValidationResult.Reject("stale-state", "The decision does not match the state fingerprint sent to the provider.");
        var currentFingerprint = _contextBuilder.Build(currentContext).StateFingerprint;
        if (!string.Equals(requestedContext.StateFingerprint, currentFingerprint, StringComparison.Ordinal))
            return BorinActionValidationResult.Reject("stale-state", "The authoritative game state changed while the provider was reasoning.");
        if (decision.Confidence is < 0 or > 1)
            return BorinActionValidationResult.Reject("invalid-confidence", "Confidence must be between 0 and 1.");
        if (decision.Intent is BorinAiIntent.None or BorinAiIntent.Speak)
            return BorinActionValidationResult.Permit("Speech-only response; no local action requested.");

        var roleCheck = ValidateRole(decision.Intent, requestedContext.Role);
        if (!roleCheck.Allowed) return roleCheck;
        var permissionCheck = ValidatePermission(decision.Intent, configuration);
        if (!permissionCheck.Allowed) return permissionCheck;
        var targetCheck = ValidateTargetAndLiveState(decision, game);
        if (!targetCheck.Allowed) return targetCheck;

        var signature = $"{requestedContext.StateFingerprint}|{decision.Intent}|{decision.Target?.Trim().ToUpperInvariant()}";
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (signature.Equals(_lastSignature, StringComparison.Ordinal) && now - _lastAcceptedAt <= DuplicateWindow)
                return BorinActionValidationResult.Reject("duplicate-action", "The same action was already accepted for this state recently.");
            _lastSignature = signature;
            _lastAcceptedAt = now;
        }
        return BorinActionValidationResult.Permit("The proposal passed local schema, role, permission, state, target, and duplicate checks.");
    }

    private static BorinActionValidationResult ValidateRole(BorinAiIntent intent, BorinRole role)
    {
        if (intent == BorinAiIntent.InspectRuntime && (role & (BorinRole.Sysop | BorinRole.Developer)) == 0)
            return BorinActionValidationResult.Reject("role-denied", "Runtime inspection requires the Sysop or Developer role.");
        if (IsPlayer(intent) && !role.HasFlag(BorinRole.Player)) return BorinActionValidationResult.Reject("role-denied", "The current objective does not grant the Player role.");
        if (IsSysop(intent) && !role.HasFlag(BorinRole.Sysop)) return BorinActionValidationResult.Reject("role-denied", "The current objective does not grant the Sysop role.");
        if (IsDeveloper(intent) && !role.HasFlag(BorinRole.Developer)) return BorinActionValidationResult.Reject("role-denied", "The current objective does not grant the Developer role.");
        return BorinActionValidationResult.Permit("Role permits this proposal.");
    }

    private static BorinActionValidationResult ValidatePermission(BorinAiIntent intent, ForgeConfiguration configuration)
    {
        if (intent is (BorinAiIntent.CreateIssue or BorinAiIntent.UpdateIssue or BorinAiIntent.ReportIssue or BorinAiIntent.GenerateRepairReport) && configuration.PermissionMode < ForgePermissionMode.Recommend)
            return BorinActionValidationResult.Reject("permission-denied", "Recommend permission is required.");
        if (intent is (BorinAiIntent.ProposePatch or BorinAiIntent.PreviewPatch or BorinAiIntent.ProposeWorldChange) && configuration.PermissionMode < ForgePermissionMode.Draft)
            return BorinActionValidationResult.Reject("permission-denied", "Draft permission is required.");
        if (intent == BorinAiIntent.ApplyPatch && (configuration.PermissionMode < ForgePermissionMode.Draft || !configuration.AllowWorkspaceEdits))
            return BorinActionValidationResult.Reject("permission-denied", "Draft and isolated workspace editing permissions are required.");
        if (intent is (BorinAiIntent.IncorporatePatch or BorinAiIntent.RevertPatch))
            return BorinActionValidationResult.Reject("human-approval-required", "Main-repository incorporation or rollback requires William's explicit local approval action.");
        if (intent is (BorinAiIntent.ApplyTestWorldChange or BorinAiIntent.ResetTestRealm) && configuration.PermissionMode != ForgePermissionMode.ApplyWithApproval)
            return BorinActionValidationResult.Reject("permission-denied", "Approval-gated Test Realm permission is required.");
        if (intent is BorinAiIntent.Build && (configuration.PermissionMode < ForgePermissionMode.Validate || !configuration.AllowBuildCommands))
            return BorinActionValidationResult.Reject("permission-denied", "Validate and build permissions are required.");
        if (intent is (BorinAiIntent.Test or BorinAiIntent.RunSpecificTest) && (configuration.PermissionMode < ForgePermissionMode.Validate || !configuration.AllowTestCommands))
            return BorinActionValidationResult.Reject("permission-denied", "Validate and test permissions are required.");
        if (intent is (BorinAiIntent.LaunchTestServer or BorinAiIntent.StopTestServer) && (configuration.PermissionMode < ForgePermissionMode.Validate || !configuration.AllowLocalProgramLaunch))
            return BorinActionValidationResult.Reject("permission-denied", "Validate and local test-program permissions are required.");
        return BorinActionValidationResult.Permit("Configured permission permits this proposal.");
    }

    private static BorinActionValidationResult ValidateTargetAndLiveState(BorinAiDecision decision, BorinGameStateSnapshot game)
    {
        if (IsPlayer(decision.Intent))
        {
            if (decision.Intent == BorinAiIntent.ConnectAndLogin)
            {
                if (game.IsConnected && game.IsLoggedIntoMajorMud)
                    return BorinActionValidationResult.Reject("already-connected", "Borin is already connected and logged into MajorMUD.");
                return BorinActionValidationResult.Permit("The authoritative login controller may handle this proposal.");
            }
            if (decision.Intent == BorinAiIntent.Disconnect)
            {
                return game.IsConnected
                    ? BorinActionValidationResult.Permit("The existing session controller may handle this proposal.")
                    : BorinActionValidationResult.Reject("not-connected", "There is no active session to disconnect.");
            }
            if (!game.IsConnected || !game.IsLoggedIntoMajorMud || !game.HasVerifiedPrompt)
                return BorinActionValidationResult.Reject("game-state-unverified", "A connected, logged-in session with a verified prompt is required.");
            if (decision.Intent == BorinAiIntent.Move)
            {
                var direction = NormalizeDirection(decision.Target);
                if (direction.Length == 0) return BorinActionValidationResult.Reject("invalid-target", "MOVE requires one canonical direction.");
                if (!game.HasVerifiedExits || !game.CurrentExits.Select(NormalizeDirection).Contains(direction, StringComparer.Ordinal))
                    return BorinActionValidationResult.Reject("target-not-present", "The requested direction is not a currently verified exit.");
            }
            if (decision.Intent is (BorinAiIntent.Attack or BorinAiIntent.Cast or BorinAiIntent.Interact or BorinAiIntent.Shop or BorinAiIntent.Quest or BorinAiIntent.Pathfind or BorinAiIntent.GameCommand) && string.IsNullOrWhiteSpace(decision.Target))
                return BorinActionValidationResult.Reject("invalid-target", $"{decision.Intent} requires a target.");
            if (decision.Intent == BorinAiIntent.GameCommand && BorinPlayerCommandPolicy.IsProtectedControlCommand(decision.Target))
                return BorinActionValidationResult.Reject("protected-command", "Account, administration, scripting, macro, or operating-system commands are outside Player authority.");
        }
        if (decision.Intent is (BorinAiIntent.ReadFile or BorinAiIntent.ReadDirectory or BorinAiIntent.SearchCode or BorinAiIntent.SearchReferences or BorinAiIntent.RunSpecificTest or BorinAiIntent.InspectExit or BorinAiIntent.InspectMonster or BorinAiIntent.InspectItem or BorinAiIntent.InspectScript or BorinAiIntent.InspectTrigger or BorinAiIntent.ProposeWorldChange or BorinAiIntent.ApplyTestWorldChange) && string.IsNullOrWhiteSpace(decision.Target))
            return BorinActionValidationResult.Reject("invalid-target", $"{decision.Intent} requires a target.");
        if (decision.Target?.IndexOfAny(['\r', '\n', '\0', '\u001b']) >= 0)
            return BorinActionValidationResult.Reject("invalid-target", "Control or multiline target text is blocked.");
        return BorinActionValidationResult.Permit("State and target pass local checks.");
    }

    private static bool IsPlayer(BorinAiIntent intent) => intent is BorinAiIntent.ConnectAndLogin or BorinAiIntent.Disconnect or BorinAiIntent.LookRoom or BorinAiIntent.Move or BorinAiIntent.Inventory or BorinAiIntent.Equipment or BorinAiIntent.Attack or BorinAiIntent.Cast or BorinAiIntent.Interact or BorinAiIntent.Shop or BorinAiIntent.Quest or BorinAiIntent.Pathfind or BorinAiIntent.GameCommand;
    private static bool IsSysop(BorinAiIntent intent) => intent is BorinAiIntent.MonitorWorld or BorinAiIntent.InspectRoom or BorinAiIntent.InspectExit or BorinAiIntent.InspectMonster or BorinAiIntent.InspectItem or BorinAiIntent.InspectScript or BorinAiIntent.InspectTrigger or BorinAiIntent.ReportIssue or BorinAiIntent.ProposeWorldChange or BorinAiIntent.ApplyTestWorldChange or BorinAiIntent.ResetTestRealm;
    private static bool IsDeveloper(BorinAiIntent intent) => intent is BorinAiIntent.SearchCode or BorinAiIntent.ReadFile or BorinAiIntent.ReadDirectory or BorinAiIntent.SearchReferences or BorinAiIntent.ReadBuildErrors or BorinAiIntent.ReadRuntimeLogs or BorinAiIntent.ReadCrashLogs or BorinAiIntent.ReadTestResults or BorinAiIntent.ProposePatch or BorinAiIntent.PreviewPatch or BorinAiIntent.ApplyPatch or BorinAiIntent.IncorporatePatch or BorinAiIntent.RevertPatch or BorinAiIntent.Build or BorinAiIntent.Test or BorinAiIntent.RunSpecificTest or BorinAiIntent.LaunchTestServer or BorinAiIntent.StopTestServer or BorinAiIntent.CompareBeforeAfter or BorinAiIntent.CreateIssue or BorinAiIntent.UpdateIssue or BorinAiIntent.GenerateRepairReport;

    private static string NormalizeDirection(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "n" or "north" => "north", "s" or "south" => "south", "e" or "east" => "east", "w" or "west" => "west",
        "u" or "up" => "up", "d" or "down" => "down", "ne" or "northeast" => "northeast", "nw" or "northwest" => "northwest",
        "se" or "southeast" => "southeast", "sw" or "southwest" => "southwest", _ => string.Empty
    };
}
