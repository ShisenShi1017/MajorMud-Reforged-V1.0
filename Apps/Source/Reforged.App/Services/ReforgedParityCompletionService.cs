using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Reforged.App.Models;

namespace Reforged.App.Services;

/// <summary>
/// Reforged-owned parity-completion helpers for the conversation desk, creature intel,
/// and user-captured problem reports. These tools consume the existing Reforged session
/// model and effective game-data authority; they do not introduce a second runtime state.
/// </summary>
public static partial class ReforgedParityCompletionService
{
    public static ReforgedConversationRow ToConversationRow(ClientTerminalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var detail = entry.Channel switch
        {
            ClientTerminalChannel.Tell => "Tell",
            ClientTerminalChannel.Party => "Party",
            ClientTerminalChannel.System => "System",
            ClientTerminalChannel.Chat => ClassifyChatDetail(entry.Text),
            _ => "Other"
        };
        return new ReforgedConversationRow(
            entry.Timestamp,
            detail,
            entry.IsOutgoing ? "Outgoing" : "Incoming",
            entry.Text,
            entry.Channel);
    }

    public static IReadOnlyList<ReforgedConversationRow> FilterConversation(
        ClientSessionConnection session,
        string? query,
        string? channel,
        bool includeIncoming,
        bool includeOutgoing,
        int maximumRows = 5000)
    {
        ArgumentNullException.ThrowIfNull(session);
        maximumRows = Math.Clamp(maximumRows, 50, 10000);
        var needle = query?.Trim() ?? string.Empty;
        var selected = channel?.Trim() ?? "All";

        return session.TerminalEntries
            .Where(entry => entry.Channel is ClientTerminalChannel.Chat or ClientTerminalChannel.Tell or ClientTerminalChannel.Party or ClientTerminalChannel.System)
            .Select(ToConversationRow)
            .Where(row => includeIncoming && row.Direction == "Incoming" || includeOutgoing && row.Direction == "Outgoing")
            .Where(row => selected.Equals("All", StringComparison.OrdinalIgnoreCase) || row.Channel.Equals(selected, StringComparison.OrdinalIgnoreCase))
            .Where(row => needle.Length == 0 || row.Text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .TakeLast(maximumRows)
            .ToArray();
    }

    public static string ConversationHistoryFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RealmGate", "ClientLogs");

    public static string GetDailyConversationLogPath(ClientSessionConnection session, DateTime? localDate = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        var character = string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName;
        var safe = SafeFileName(character);
        return Path.Combine(ConversationHistoryFolder, $"{safe}-{(localDate ?? DateTime.Now):yyyyMMdd}.log");
    }

    /// <summary>
    /// Reads an existing Reforged terminal log as conversation history without changing the active
    /// session collections. Historical terminal logs did not persist the outgoing flag, so imported
    /// rows are deliberately labelled Recorded rather than guessing a direction.
    /// </summary>
    public static async Task<IReadOnlyList<ReforgedConversationRow>> ReadConversationHistoryAsync(
        string path,
        int maximumRows = 20000,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return [];
        maximumRows = Math.Clamp(maximumRows, 100, 50000);

        var info = new FileInfo(path);
        if (info.Length > 64L * 1024L * 1024L)
            throw new InvalidDataException("Conversation history log is larger than the 64 MB safety limit.");

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var rows = new List<ReforgedConversationRow>();
        foreach (var line in lines.TakeLast(maximumRows))
        {
            var match = TerminalLogRowRegex().Match(line);
            if (!match.Success) continue;
            if (!DateTimeOffset.TryParse(match.Groups["time"].Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
                continue;
            if (!Enum.TryParse<ClientTerminalChannel>(match.Groups["channel"].Value, ignoreCase: true, out var source))
                continue;

            var text = match.Groups["text"].Value;
            if (source is not (ClientTerminalChannel.Chat or ClientTerminalChannel.Tell or ClientTerminalChannel.Party or ClientTerminalChannel.System))
                continue;

            var detail = source switch
            {
                ClientTerminalChannel.Tell => "Tell",
                ClientTerminalChannel.Party => "Party",
                ClientTerminalChannel.System => "System",
                ClientTerminalChannel.Chat => ClassifyChatDetail(text),
                _ => "Other"
            };
            rows.Add(new ReforgedConversationRow(timestamp, detail, "Recorded", text, source));
        }
        return rows;
    }

    public static IReadOnlyList<ReforgedCreatureIntelEntry> LoadCreatureIntel(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var overlay = new MudPlayClientGameDataOverlayService();
        var liveById = session.CurrentRoomNpcDetails
            .Where(npc => !string.IsNullOrWhiteSpace(npc.CanonicalNpcId))
            .GroupBy(npc => npc.CanonicalNpcId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var liveByName = session.CurrentRoomNpcDetails
            .Where(npc => !string.IsNullOrWhiteSpace(npc.Name))
            .GroupBy(npc => NormalizeName(npc.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var simpleLiveNames = session.CurrentRoomNpcs
            .Select(NormalizeName)
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new List<ReforgedCreatureIntelEntry>();
        foreach (var record in overlay.LoadFamily(session, "Monsters / NPCs"))
        {
            var root = record.EffectiveRecord;
            var id = Text(root, "id");
            var name = FirstText(root,
                ["name"],
                ["public_reference_0139", "fields", "name"]);
            if (name.Length == 0) continue;

            var attacks = ReadAttacks(root);
            var abilities = ReadAbilities(root);
            ClientRoomInspectionNpc? live = null;
            if (id.Length > 0) liveById.TryGetValue(id, out live);
            live ??= liveByName.GetValueOrDefault(NormalizeName(name));
            var isHere = live is not null || simpleLiveNames.Contains(NormalizeName(name));

            result.Add(new ReforgedCreatureIntelEntry(
                id,
                name,
                FirstInt(root, ["combat", "level"], ["public_reference_0139", "fields", "level"]),
                FirstInt(root, ["combat", "hp"], ["public_reference_0139", "fields", "hit_points"]),
                FirstInt(root, ["combat", "energy"], ["public_reference_0139", "fields", "energy"]),
                FirstInt(root, ["combat", "armor_class"], ["public_reference_0139", "fields", "armor_class"]),
                FirstInt(root, ["combat", "damage_resistance"], ["public_reference_0139", "fields", "damage_resistance"]),
                FirstInt(root, ["combat", "magic_resistance"], ["public_reference_0139", "fields", "magic_resistance"]),
                FirstLong(root, ["rewards", "xp_base"], ["public_reference_0139", "fields", "base_experience"]),
                FirstBool(root, ["behavior", "undead"], ["public_reference_0139", "fields", "undead"]),
                FirstText(root, ["behavior", "alignment"], ["public_reference_0139", "fields", "alignment"]),
                FirstInt(root, ["behavior", "respawn_hours"], ["public_reference_0139", "fields", "respawn_hours"]),
                FirstInt(root, ["combat", "attack_count"]),
                attacks.Count == 0 ? 0 : attacks.Max(attack => attack.MaximumDamage),
                attacks.Sum(attack => Math.Max(0, attack.MaximumDamage)),
                attacks,
                abilities,
                isHere,
                live?.CurrentHitPoints,
                live?.MaximumHitPoints,
                live?.Disposition ?? string.Empty,
                FirstText(root, ["confidence"])));
        }

        return result
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string RenderCreatureDetails(ReforgedCreatureIntelEntry creature, ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(session);
        var sb = new StringBuilder();
        sb.AppendLine($"{creature.Name}  [{creature.Id}]");
        sb.AppendLine($"Level {creature.Level:N0} · HP {creature.HitPoints:N0} · Energy {creature.Energy:N0}");
        sb.AppendLine($"AC {creature.ArmorClass:N0} · DR {creature.DamageResistance:N0} · MR {creature.MagicResistance:N0}");
        sb.AppendLine($"XP {creature.ExperienceReward:N0} · Alignment {Display(creature.Alignment)} · Undead {(creature.Undead ? "Yes" : "No")}");
        if (creature.RespawnHours > 0) sb.AppendLine($"Decoded respawn interval: {creature.RespawnHours:N0} hour(s)");
        if (creature.IsInCurrentRoom)
            sb.AppendLine($"Live room state: {creature.LiveState}" + (string.IsNullOrWhiteSpace(creature.LiveDisposition) ? string.Empty : $" · {creature.LiveDisposition}"));
        sb.AppendLine();
        sb.AppendLine("Decoded attacks");
        if (creature.Attacks.Count == 0)
        {
            sb.AppendLine("  No attack profile is present in the effective data record.");
        }
        else
        {
            foreach (var attack in creature.Attacks.OrderBy(attack => attack.Slot))
                sb.AppendLine($"  #{attack.Slot}: {attack.DamageRange} dmg · accuracy {attack.Accuracy:N0} · select {attack.SelectionPercent:N0}% · energy {attack.EnergyCost:N0}" +
                              (string.IsNullOrWhiteSpace(attack.WeaponName) ? string.Empty : $" · {attack.WeaponName}"));
        }
        if (creature.Abilities.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Decoded abilities");
            foreach (var ability in creature.Abilities) sb.AppendLine("  " + ability);
        }
        sb.AppendLine();
        sb.AppendLine("Current-character comparison");
        sb.AppendLine($"  Character level: {(session.CharacterLevel > 0 ? session.CharacterLevel.ToString("N0", CultureInfo.InvariantCulture) : "not captured")}");
        sb.AppendLine($"  Character HP: {(session.HealthMaximum > 0 ? session.HealthMaximum.ToString("N0", CultureInfo.InvariantCulture) : "not captured")}");
        sb.AppendLine($"  Creature max decoded single hit: {(creature.MaximumSingleHit > 0 ? creature.MaximumSingleHit.ToString("N0", CultureInfo.InvariantCulture) : "not decoded")}");
        sb.AppendLine($"  Creature max decoded attack-profile total: {(creature.MaximumProfileDamage > 0 ? creature.MaximumProfileDamage.ToString("N0", CultureInfo.InvariantCulture) : "not decoded")}");
        sb.AppendLine();
        sb.AppendLine("RealmGate does not label this encounter safe/unsafe from incomplete formulas. These values are evidence-backed comparisons for your decision and for the combat engine's configured limits.");
        if (!string.IsNullOrWhiteSpace(creature.Confidence)) sb.AppendLine($"Data confidence: {creature.Confidence}");
        return sb.ToString();
    }

    public static async Task<string> WriteBugReportAsync(
        ClientSessionConnection session,
        ClientAutomationOptions options,
        ReforgedBugReportRequest request,
        string? destinationPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RealmGate", "Diagnostics");
        Directory.CreateDirectory(folder);
        var realm = SafeFileName(string.IsNullOrWhiteSpace(session.ServerProfileName) ? "reforged" : session.ServerProfileName);
        destinationPath ??= Path.Combine(folder, $"reforged-{realm}-{DateTime.Now:yyyyMMdd-HHmmss}.md");

        var sb = new StringBuilder();
        sb.AppendLine("# RealmGate Problem Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTimeOffset.Now:O}");
        sb.AppendLine($"Area: {Clean(request.Area)}");
        sb.AppendLine($"Summary: {Clean(request.Summary)}");
        sb.AppendLine();
        Section(sb, "What happened", request.WhatHappened);
        Section(sb, "Expected behavior", request.Expected);
        Section(sb, "Steps to reproduce", request.Steps);
        Section(sb, "Additional notes", request.Notes);
        sb.AppendLine("## Frozen client state");
        sb.AppendLine();
        sb.AppendLine("```text");
        sb.AppendLine(ReforgedClientDiagnosticsService.Redact(ReforgedClientDiagnosticsService.RenderSessionStats(session, DateTimeOffset.Now)).TrimEnd());
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Automation state");
        sb.AppendLine();
        sb.AppendLine("```text");
        sb.AppendLine(ReforgedClientDiagnosticsService.Redact(ReforgedClientDiagnosticsService.RenderProgramSummary(session)).TrimEnd());
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Client presentation");
        sb.AppendLine();
        sb.AppendLine($"- Font: {Clean(options.Presentation.FontFamily)} {options.Presentation.FontSize}");
        sb.AppendLine($"- Output scale: {options.Presentation.OutputScalePercent}%");
        sb.AppendLine($"- Scrollback limit: {session.TerminalPreferences.MaximumScrollbackLines:N0}");
        sb.AppendLine($"- Terminal logging: {session.TerminalPreferences.LoggingEnabled}");
        sb.AppendLine($"- Telnet capture: {session.TerminalPreferences.CaptureTelnetTraffic}");
        sb.AppendLine();

        if (request.IncludeInventoryNames)
        {
            sb.AppendLine("## Captured inventory/equipment");
            sb.AppendLine();
            sb.AppendLine("Inventory: " + (session.InventoryItems.Count == 0 ? "not captured" : string.Join(", ", session.InventoryItems.Select(Clean))));
            sb.AppendLine();
            sb.AppendLine("Equipment: " + (session.EquippedItems.Count == 0 ? "not captured" : string.Join(", ", session.EquippedItems.Select(Clean))));
            sb.AppendLine();
        }

        sb.AppendLine("## Recent program log");
        sb.AppendLine();
        sb.AppendLine("```text");
        foreach (var row in session.ProgramLogEntries.TakeLast(300))
            sb.AppendLine(ReforgedClientDiagnosticsService.Redact($"[{row.Timestamp:O}] [{row.Area}] [{row.Origin?.ToString() ?? "State"}] {row.Message}"));
        sb.AppendLine("```");
        sb.AppendLine();

        if (request.IncludeRecentScrollback)
        {
            var max = Math.Clamp(request.MaximumScrollbackLines, 50, 2000);
            sb.AppendLine("## Recent terminal output");
            sb.AppendLine();
            sb.AppendLine("This section may contain ordinary in-game conversation text. Login passwords and sensitive command-entry paths are not intentionally captured here.");
            sb.AppendLine();
            sb.AppendLine("```text");
            foreach (var row in session.TerminalEntries.TakeLast(max))
                sb.AppendLine(ReforgedClientDiagnosticsService.Redact($"[{row.Timestamp:O}] [{row.Channel}] {row.Text}"));
            sb.AppendLine("```");
        }

        await File.WriteAllTextAsync(destinationPath, sb.ToString(), new UTF8Encoding(false), cancellationToken);
        return destinationPath;
    }

    private static string ClassifyChatDetail(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        if (GossipRegex().IsMatch(value)) return "Gossip";
        if (AuctionRegex().IsMatch(value)) return "Auction";
        if (BroadcastRegex().IsMatch(value)) return "Broadcast";
        if (GangpathRegex().IsMatch(value)) return "Gangpath";
        if (YellRegex().IsMatch(value)) return "Yell";
        if (SayRegex().IsMatch(value)) return "Say";
        return "Chat";
    }

    private static IReadOnlyList<ReforgedCreatureAttackIntel> ReadAttacks(JsonObject root)
    {
        if (root["attacks"] is not JsonArray attacks) return [];
        var result = new List<ReforgedCreatureAttackIntel>();
        foreach (var node in attacks.OfType<JsonObject>())
        {
            result.Add(new ReforgedCreatureAttackIntel(
                Int(node, "slot"),
                Text(node, "type"),
                Int(node, "minimum_damage"),
                Int(node, "maximum_damage"),
                Int(node, "accuracy"),
                Int(node, "selection_percent"),
                Int(node, "energy_cost"),
                Text(node, "weapon_name")));
        }
        return result;
    }

    private static IReadOnlyList<string> ReadAbilities(JsonObject root)
    {
        if (root["abilities"] is not JsonArray abilities) return [];
        return abilities.OfType<JsonObject>()
            .Select(node =>
            {
                var name = Text(node, "ability");
                var value = Text(node, "value");
                return name.Length == 0 ? string.Empty : value.Length == 0 ? name : $"{name}: {value}";
            })
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string FirstText(JsonObject root, params string[][] paths)
    {
        foreach (var path in paths)
            if (TryNode(root, path, out var node))
            {
                var value = NodeText(node);
                if (value.Length > 0) return value;
            }
        return string.Empty;
    }

    private static int FirstInt(JsonObject root, params string[][] paths)
    {
        foreach (var path in paths)
            if (TryNode(root, path, out var node) && TryInt(node, out var value)) return value;
        return 0;
    }

    private static long FirstLong(JsonObject root, params string[][] paths)
    {
        foreach (var path in paths)
            if (TryNode(root, path, out var node) && TryLong(node, out var value)) return value;
        return 0;
    }

    private static bool FirstBool(JsonObject root, params string[][] paths)
    {
        foreach (var path in paths)
            if (TryNode(root, path, out var node))
            {
                if (node is JsonValue value && value.TryGetValue<bool>(out var flag)) return flag;
                if (bool.TryParse(NodeText(node), out flag)) return flag;
            }
        return false;
    }

    private static bool TryNode(JsonObject root, IReadOnlyList<string> path, out JsonNode? node)
    {
        node = root;
        foreach (var part in path)
        {
            if (node is not JsonObject obj || !obj.TryGetPropertyValue(part, out node) || node is null)
                return false;
        }
        return true;
    }

    private static string Text(JsonObject obj, string property) => obj.TryGetPropertyValue(property, out var node) ? NodeText(node) : string.Empty;
    private static int Int(JsonObject obj, string property) => obj.TryGetPropertyValue(property, out var node) && TryInt(node, out var value) ? value : 0;
    private static string NodeText(JsonNode? node)
    {
        if (node is null) return string.Empty;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text)) return text?.Trim() ?? string.Empty;
            if (value.TryGetValue<int>(out var i)) return i.ToString(CultureInfo.InvariantCulture);
            if (value.TryGetValue<long>(out var l)) return l.ToString(CultureInfo.InvariantCulture);
            if (value.TryGetValue<double>(out var d)) return d.ToString(CultureInfo.InvariantCulture);
            if (value.TryGetValue<bool>(out var b)) return b.ToString();
        }
        return node.ToJsonString().Trim('"').Trim();
    }
    private static bool TryInt(JsonNode? node, out int value) => int.TryParse(NodeText(node), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    private static bool TryLong(JsonNode? node, out long value) => long.TryParse(NodeText(node), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    private static string NormalizeName(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        text = Regex.Replace(text, @"^\s*\d+\s*[xX]\s+", string.Empty);
        text = Regex.Replace(text, @"^(a|an|the)\s+", string.Empty);
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }
    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    private static string SafeFileName(string value) => string.Concat(value.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
    private static string Clean(string? value) => ReforgedClientDiagnosticsService.Redact(value ?? string.Empty).Trim();
    private static void Section(StringBuilder sb, string header, string? value)
    {
        sb.AppendLine($"## {header}");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(value) ? "_Not provided._" : Clean(value));
        sb.AppendLine();
    }

    [GeneratedRegex(@"^\[(?<time>[^\]]+)\]\s+\[(?<channel>[^\]]+)\]\s(?<text>.*)$", RegexOptions.CultureInvariant)] private static partial Regex TerminalLogRowRegex();
    [GeneratedRegex(@"^(?:You\s+)?gossip\b|\bgossips?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex GossipRegex();
    [GeneratedRegex(@"^(?:You\s+)?auction\b|\bauctions?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex AuctionRegex();
    [GeneratedRegex(@"^(?:You\s+)?broadcast\b|\bbroadcasts?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex BroadcastRegex();
    [GeneratedRegex(@"\bgangpath\b|^(?:bg|gb)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex GangpathRegex();
    [GeneratedRegex(@"^(?:You\s+)?yell\b|\byells?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex YellRegex();
    [GeneratedRegex(@"^(?:You\s+)?say\b|\bsays?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex SayRegex();
}
