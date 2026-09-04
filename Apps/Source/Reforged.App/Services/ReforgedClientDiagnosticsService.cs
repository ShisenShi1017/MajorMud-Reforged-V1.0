using System.Text;
using System.Text.RegularExpressions;
using Reforged.App.Models;

namespace Reforged.App.Services;

/// <summary>
/// Reforged-native client diagnostics helpers used by the terminal workbench.
/// This intentionally consumes Reforged's own typed session state and local trace files;
/// it does not import MudPlay's diagnostic storage or UI model.
/// </summary>
public static partial class ReforgedClientDiagnosticsService
{
    private static readonly string[] InternalProtocolPrefixes = ["clientdata", "roomdata"];

    public static IReadOnlyList<ClientTerminalEntry> FilterScrollback(
        ClientSessionConnection session,
        string? query,
        ClientTerminalChannel? channel,
        bool includeOutgoing,
        int maximumRows = 2000)
    {
        ArgumentNullException.ThrowIfNull(session);
        maximumRows = Math.Clamp(maximumRows, 50, 10000);
        var needle = query?.Trim() ?? string.Empty;

        IEnumerable<ClientTerminalEntry> rows = session.TerminalEntries;
        if (channel is { } selected)
            rows = rows.Where(row => row.Channel == selected);
        if (!includeOutgoing)
            rows = rows.Where(row => !row.IsOutgoing);
        if (needle.Length > 0)
            rows = rows.Where(row => row.Text.Contains(needle, StringComparison.OrdinalIgnoreCase));

        return rows.TakeLast(maximumRows).ToArray();
    }

    public static string RenderSessionStats(ClientSessionConnection session, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);
        var elapsed = session.ConnectedAt is { } connected ? now - connected : TimeSpan.Zero;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        var xpPerHour = elapsed.TotalMinutes >= 1
            ? session.SessionExperienceGained / Math.Max(elapsed.TotalHours, 1d / 60d)
            : 0d;
        var remaining = Math.Max(0, session.ExperienceRemaining > 0
            ? session.ExperienceRemaining
            : session.ExperienceNeeded > session.ExperienceCurrent
                ? session.ExperienceNeeded - session.ExperienceCurrent
                : 0);
        var eta = xpPerHour > 0 && remaining > 0
            ? TimeSpan.FromHours(remaining / xpPerHour)
            : (TimeSpan?)null;

        var text = new StringBuilder();
        text.AppendLine($"Character          {Display(session.CharacterName)}");
        text.AppendLine($"Server / world     {Display(session.ServerProfileName)} / {Display(session.WorldName)}");
        text.AppendLine($"Connection         {session.ConnectionState} — {session.ConnectionMessage}");
        text.AppendLine($"Connected          {FormatTimestamp(session.ConnectedAt)}");
        text.AppendLine($"Elapsed            {FormatDuration(elapsed)}");
        text.AppendLine($"Terminal           {session.TerminalSizeSummary} · {session.TerminalEntries.Count:N0} retained entries");
        text.AppendLine();
        text.AppendLine($"Level              {session.CharacterLevel:N0}");
        text.AppendLine($"Experience         {session.ExperienceCurrent:N0}" + (session.ExperienceNeeded > 0 ? $" / {session.ExperienceNeeded:N0}" : string.Empty));
        text.AppendLine($"Session XP         {session.SessionExperienceGained:N0}");
        text.AppendLine($"XP / hour          {xpPerHour:N0}");
        text.AppendLine($"Next-level ETA     {(eta is null ? "Unavailable" : FormatDuration(eta.Value))}");
        text.AppendLine($"Combat events      {session.SessionCombatEventCount:N0}");
        text.AppendLine($"Rooms observed     {session.SessionRoomsObserved:N0}");
        text.AppendLine();
        text.AppendLine($"HP                 {session.HealthCurrent:N0}/{session.HealthMaximum:N0} ({session.HealthPercent}%)");
        text.AppendLine($"Mana / Kai         {session.ManaCurrent:N0}/{session.ManaMaximum:N0} ({session.ManaPercent}%)");
        text.AppendLine($"Currency           {session.CarriedCurrency:N0}");
        text.AppendLine($"Encumbrance        {session.CarriedWeight:N0}/{session.CarryingCapacity:N0} ({session.EncumbrancePercent}%) {Display(session.EncumbranceBand)}");
        text.AppendLine();
        text.AppendLine($"Inventory          {session.InventoryItems.Count:N0}");
        text.AppendLine($"Equipment          {session.EquippedItems.Count:N0}");
        text.AppendLine($"Learned spells     {session.LearnedSpells.Count:N0}");
        text.AppendLine($"Active effects     {session.ActiveEffects.Count:N0}");
        text.AppendLine($"Party members      {session.PartyMembers.Count:N0}");
        text.AppendLine($"Tracked bosses     {session.BossHomes.Count:N0}");
        text.AppendLine();
        text.AppendLine($"Room               {Display(session.CurrentRoomName)}");
        text.AppendLine($"Room key           {Display(session.CurrentRoomKey)}");
        text.AppendLine($"Route              {session.RouteState} — {Display(session.RouteMessage)}");
        text.AppendLine($"Route verification {session.RouteVerifiedStepCount:N0} verified / {session.RouteUnverifiedStepCount:N0} unverified · {session.RouteDesyncCount:N0} desync(s)");
        text.AppendLine();
        text.AppendLine($"Last command       {Display(session.LastCommandText)}");
        text.AppendLine($"Command origin     {session.LastCommandOrigin}");
        text.AppendLine($"Last command at    {FormatTimestamp(session.LastCommandAt)}");
        text.AppendLine($"Last server data   {FormatTimestamp(session.LastServerActivityAt)}");
        return text.ToString();
    }

    public static string RenderProgramSummary(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var text = new StringBuilder();
        text.AppendLine($"Combat     {Display(session.CombatAutomationStatus)}");
        text.AppendLine($"Recovery   {Display(session.SurvivalAutomationStatus)}");
        text.AppendLine($"Spells     {Display(session.SpellAutomationStatus)}");
        text.AppendLine($"Party      {Display(session.PartyAutomationStatus)}");
        text.AppendLine($"PvP        {Display(session.PvpAutomationStatus)}");
        text.AppendLine($"Navigation {Display(session.RoamingAutomationStatus)}");
        text.AppendLine($"Loot       {Display(session.LootAutomationStatus)}");
        return text.ToString();
    }

    public static bool IsSafeUserCommand(string? command)
    {
        var value = command?.Trim() ?? string.Empty;
        if (value.Length == 0 || value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            return false;
        var first = value.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)[0];
        return !InternalProtocolPrefixes.Any(prefix => first.Equals(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<string> ReadTelnetTraceTailAsync(
        ClientSessionConnection session,
        int maximumLines = 250,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        maximumLines = Math.Clamp(maximumLines, 20, 2000);
        var path = session.TelnetTracePath?.Trim() ?? string.Empty;
        if (path.Length == 0)
            return "Telnet traffic capture is off for this connection. Enable it in Diagnostics & Customization and reconnect to create a new trace.";
        if (!File.Exists(path))
            return $"Trace path is assigned but no trace file exists yet.\n{path}";

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        return string.Join(Environment.NewLine, lines.TakeLast(maximumLines));
    }

    public static async Task<string> ExportSupportBundleAsync(
        ClientSessionConnection session,
        ClientAutomationOptions options,
        string? destinationPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RealmGate", "Diagnostics");
        Directory.CreateDirectory(folder);
        var safeName = string.Concat((string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName)
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        destinationPath ??= Path.Combine(folder, $"reforged-client-support-{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

        var sb = new StringBuilder();
        sb.AppendLine("RealmGate Client Support Bundle");
        sb.AppendLine($"Generated: {DateTimeOffset.Now:O}");
        sb.AppendLine("Privacy: terminal conversation text and raw Telnet trace payloads are intentionally excluded.");
        sb.AppendLine();
        sb.AppendLine(RenderSessionStats(session, DateTimeOffset.Now));
        sb.AppendLine("Automation status");
        sb.AppendLine(RenderProgramSummary(session));
        sb.AppendLine("Client presentation");
        sb.AppendLine($"Font={options.Presentation.FontFamily}; Size={options.Presentation.FontSize}; Scale={options.Presentation.OutputScalePercent}%; Toolbar={options.Presentation.ShowToolbar}; StatusBar={options.Presentation.ShowStatusBar}");
        sb.AppendLine($"Terminal logging={session.TerminalPreferences.LoggingEnabled}; Telnet capture={session.TerminalPreferences.CaptureTelnetTraffic}; Scrollback limit={session.TerminalPreferences.MaximumScrollbackLines}");
        sb.AppendLine();
        sb.AppendLine("Recent program log");
        foreach (var row in session.ProgramLogEntries.TakeLast(200))
            sb.AppendLine($"[{row.Timestamp:O}] [{row.Area}] [{row.Origin?.ToString() ?? "State"}] {Redact(row.Message)}");

        await File.WriteAllTextAsync(destinationPath, sb.ToString(), new UTF8Encoding(false), cancellationToken);
        return destinationPath;
    }

    public static string Redact(string? input)
    {
        var value = input ?? string.Empty;
        value = SecretAssignmentRegex().Replace(value, match => $"{match.Groups[1].Value}=[REDACTED]");
        value = BearerRegex().Replace(value, "Bearer [REDACTED]");
        return value;
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    private static string FormatTimestamp(DateTimeOffset? value) => value is null ? "Unknown" : value.Value.ToLocalTime().ToString("G");
    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalDays >= 1) return $"{(int)value.TotalDays}d {value.Hours}h {value.Minutes}m";
        if (value.TotalHours >= 1) return $"{(int)value.TotalHours}h {value.Minutes}m {value.Seconds}s";
        return $"{value.Minutes}m {value.Seconds}s";
    }

    [GeneratedRegex(@"(?i)\b(password|passwd|token|secret|api[_-]?key)\s*=\s*[^\s;]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(@"(?i)\bBearer\s+[^\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();
}
