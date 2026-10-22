namespace RealmGate.Client.Automation;

public sealed class ClientInputAutomationRuntime
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DateTimeOffset> _triggerLastRun =
        new(StringComparer.OrdinalIgnoreCase);

    public ClientInputAutomationRuntime(ClientInputAutomationProfile? profile = null) =>
        Profile = profile ?? new ClientInputAutomationProfile();

    public ClientInputAutomationProfile Profile { get; private set; }

    public void UpdateProfile(ClientInputAutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ClientInputAutomationProfileStore.Normalize(profile);
        lock (_sync)
        {
            Profile = profile;
            _triggerLastRun.Clear();
        }
    }

    public bool TryResolveHumanInput(
        string input,
        out IReadOnlyList<string> commands)
    {
        commands = [];
        var text = input?.Trim() ?? string.Empty;
        ClientInputAutomationProfile profile;
        lock (_sync)
            profile = Profile;

        if (profile.EmergencyStop || text.Length == 0)
            return false;

        const string macroPrefix = "/macro ";
        if (profile.MacrosEnabled &&
            text.StartsWith(macroPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var invocation = text[macroPrefix.Length..].Trim();
            var separator = invocation.IndexOf(' ');
            var name = separator < 0 ? invocation : invocation[..separator];
            var arguments = separator < 0
                ? string.Empty
                : invocation[(separator + 1)..].Trim();
            var macro = profile.Macros.FirstOrDefault(candidate =>
                candidate.Enabled &&
                candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (macro is null)
                return false;

            commands = ClientAutomationExpansionEngine.ExpandMacro(
                macro.Commands,
                arguments);
            return commands.Count > 0;
        }

        if (!profile.AliasesEnabled)
            return false;

        var aliasSeparator = text.IndexOf(' ');
        var namePart = aliasSeparator < 0 ? text : text[..aliasSeparator];
        var argumentsPart = aliasSeparator < 0
            ? string.Empty
            : text[(aliasSeparator + 1)..].Trim();
        var alias = profile.Aliases.FirstOrDefault(candidate =>
            candidate.Enabled &&
            candidate.Name.Equals(namePart, StringComparison.OrdinalIgnoreCase));
        if (alias is null || string.IsNullOrWhiteSpace(alias.Command))
            return false;

        commands = ClientAutomationExpansionEngine.ExpandAlias(
            alias.Command,
            argumentsPart);
        return commands.Count > 0;
    }

    public ClientInputAutomationMatch? EvaluateText(
        string text,
        bool? inCombat,
        string sessionId,
        DateTimeOffset timestamp)
    {
        lock (_sync)
        {
            var profile = Profile;
            if (profile.EmergencyStop || !profile.TextTriggersEnabled)
                return null;

            foreach (var trigger in profile.TextTriggers
                         .Where(trigger => trigger.Enabled)
                         .OrderBy(trigger => trigger.Priority)
                         .ThenBy(trigger => trigger.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(trigger.MatchText) ||
                    string.IsNullOrWhiteSpace(trigger.Command) ||
                    trigger.OnlyOutOfCombat && inCombat != false ||
                    trigger.OnlyInCombat && inCombat != true)
                {
                    continue;
                }

                var runKey = $"{sessionId}\u001f{trigger.Id}";
                if (_triggerLastRun.TryGetValue(runKey, out var lastRun) &&
                    (timestamp - lastRun).TotalSeconds <
                    Math.Max(0, trigger.CooldownSeconds))
                {
                    continue;
                }

                if (!ClientAutomationExpansionEngine.TryMatchTrigger(
                        trigger,
                        text,
                        out var captures,
                        out _) ||
                    !ClientAutomationExpansionEngine.TryExpandTriggerResponse(
                        trigger.Command,
                        captures,
                        out var commands))
                {
                    continue;
                }

                _triggerLastRun[runKey] = timestamp;
                return new ClientInputAutomationMatch(
                    trigger.Id,
                    string.IsNullOrWhiteSpace(trigger.Name)
                        ? $"Text trigger: {trigger.MatchText.Trim()}"
                        : trigger.Name.Trim(),
                    commands,
                    trigger.Priority);
            }

            return null;
        }
    }
}
