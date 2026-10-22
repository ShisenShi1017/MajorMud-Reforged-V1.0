using System.Text;
using System.Text.RegularExpressions;

namespace RealmGate.Client.Automation;

public static class ClientAutomationExpansionEngine
{
    public const int MaximumCommandSteps = 25;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    public static IReadOnlyList<string> ExpandAlias(string expansion, string arguments)
    {
        var hasPlaceholders = ContainsPositionalPlaceholder(expansion);
        var expanded = SubstitutePositionals(expansion, arguments);
        if (!hasPlaceholders && !string.IsNullOrWhiteSpace(arguments))
            expanded = $"{expanded.Trim()} {arguments.Trim()}";
        return SplitCommandSteps(expanded);
    }

    public static IReadOnlyList<string> ExpandMacro(
        IEnumerable<string> commands,
        string arguments)
    {
        var expanded = new List<string>();
        foreach (var command in commands)
        {
            foreach (var step in SplitCommandSteps(
                         SubstitutePositionals(command, arguments)))
            {
                expanded.Add(step);
                if (expanded.Count >= MaximumCommandSteps)
                    return expanded;
            }
        }

        return expanded;
    }

    public static IReadOnlyList<string> SplitCommandSteps(string? commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            return [];

        var normalized = commandText
            .Replace("^M", ";", StringComparison.OrdinalIgnoreCase)
            .Replace('\r', ';')
            .Replace('\n', ';');
        return normalized
            .Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(step => step.Length > 0)
            .Take(MaximumCommandSteps)
            .ToArray();
    }

    public static bool TryMatchTrigger(
        ClientTextTrigger trigger,
        string text,
        out IReadOnlyDictionary<string, string> captures,
        out string? error)
    {
        captures = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        error = null;
        if (!trigger.Enabled || string.IsNullOrWhiteSpace(trigger.MatchText))
            return false;

        var comparison = trigger.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        if (trigger.MatchMode == ClientTextTriggerMatchMode.Contains)
            return text.Contains(trigger.MatchText, comparison);
        if (trigger.MatchMode == ClientTextTriggerMatchMode.Exact)
            return string.Equals(
                text.Trim(),
                trigger.MatchText.Trim(),
                comparison);

        try
        {
            var pattern = trigger.MatchMode == ClientTextTriggerMatchMode.Wildcard
                ? WildcardToRegex(trigger.MatchText)
                : trigger.MatchText;
            var options = RegexOptions.CultureInvariant;
            if (!trigger.CaseSensitive)
                options |= RegexOptions.IgnoreCase;
            var regex = new Regex(pattern, options, RegexTimeout);
            var match = regex.Match(text);
            if (!match.Success)
                return false;

            var found = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["match"] = match.Value
            };
            foreach (var groupName in regex.GetGroupNames())
            {
                if (groupName.Length == 0 || char.IsDigit(groupName[0]))
                    continue;
                var group = match.Groups[groupName];
                if (group.Success)
                    found[groupName] = group.Value;
            }

            captures = found;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            error = "Pattern exceeded the 250 ms trigger timeout.";
            return false;
        }
    }

    public static bool TryExpandTriggerResponse(
        string response,
        IReadOnlyDictionary<string, string> captures,
        out IReadOnlyList<string> commands)
    {
        commands = [];
        if (!TrySubstituteNamed(response, captures, out var expanded))
            return false;
        commands = SplitCommandSteps(expanded);
        return commands.Count > 0;
    }

    public static string? ValidateTriggerPattern(
        ClientTextTriggerMatchMode mode,
        string pattern,
        bool caseSensitive = false)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return "Pattern cannot be blank.";
        if (mode is ClientTextTriggerMatchMode.Contains or
            ClientTextTriggerMatchMode.Exact)
        {
            return null;
        }

        try
        {
            var regexPattern = mode == ClientTextTriggerMatchMode.Wildcard
                ? WildcardToRegex(pattern)
                : pattern;
            var options = RegexOptions.CultureInvariant |
                          (caseSensitive
                              ? RegexOptions.None
                              : RegexOptions.IgnoreCase);
            _ = new Regex(regexPattern, options, RegexTimeout);
            return null;
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }

    public static string? AliasNameConflictReason(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Alias name cannot be blank.";
        var trimmed = name.Trim();
        if (trimmed.Any(char.IsWhiteSpace))
            return "Alias names cannot contain whitespace.";
        if (trimmed[0] == '.')
            return "Names starting with '.' would intercept the say channel.";
        if (trimmed[0] == '"')
            return "Names starting with a quote would intercept the yell channel.";
        if (trimmed[0] == '/')
            return "Names starting with '/' are reserved for client and telepath commands.";

        var lower = trimmed.ToLowerInvariant();
        if (IsPrefix(lower, "gossip", 3))
            return $"'{trimmed}' would intercept the gossip channel.";
        if (IsPrefix(lower, "auction", 3))
            return $"'{trimmed}' would intercept the auction channel.";
        if (IsPrefix(lower, "broadcast", 2))
            return $"'{trimmed}' would intercept the broadcast channel.";
        if (lower is "broadg" or "broadga" or "broadgan" or
            "broadgang" or "bg" or "gb")
        {
            return $"'{trimmed}' would intercept the gangpath channel.";
        }

        return null;
    }

    private static bool IsPrefix(
        string candidate,
        string command,
        int minimumLength) =>
        candidate.Length >= minimumLength &&
        candidate.Length <= command.Length &&
        command.StartsWith(candidate, StringComparison.Ordinal);

    private static bool ContainsPositionalPlaceholder(string expansion)
    {
        for (var index = 0; index < expansion.Length; index++)
        {
            if (expansion[index] == '{' &&
                TryReadIndex(expansion, index, out _, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static string SubstitutePositionals(
        string template,
        string arguments)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        string[]? positional = null;
        var builder = new StringBuilder(template.Length + arguments.Length);
        for (var index = 0; index < template.Length;)
        {
            if (template[index] == '{' &&
                TryReadIndex(template, index, out var argumentIndex, out var next))
            {
                if (argumentIndex == 0)
                {
                    builder.Append(arguments);
                }
                else
                {
                    positional ??= arguments.Split(
                        (char[]?)null,
                        StringSplitOptions.RemoveEmptyEntries);
                    if (argumentIndex - 1 < positional.Length)
                        builder.Append(positional[argumentIndex - 1]);
                }

                index = next;
                continue;
            }

            builder.Append(template[index++]);
        }

        return builder.ToString();
    }

    private static bool TryReadIndex(
        string value,
        int start,
        out int index,
        out int next)
    {
        index = -1;
        next = start + 1;
        var end = value.IndexOf('}', start + 1);
        if (end <= start + 1 ||
            !int.TryParse(
                value.AsSpan(start + 1, end - start - 1),
                out var parsed) ||
            parsed < 0)
        {
            return false;
        }

        index = parsed;
        next = end + 1;
        return true;
    }

    private static bool TrySubstituteNamed(
        string template,
        IReadOnlyDictionary<string, string> captures,
        out string result)
    {
        var builder = new StringBuilder(template.Length);
        for (var index = 0; index < template.Length;)
        {
            if (template[index] == '{' &&
                TryReadName(template, index, out var name, out var next))
            {
                if (!captures.TryGetValue(name, out var value))
                {
                    result = string.Empty;
                    return false;
                }

                builder.Append(value);
                index = next;
                continue;
            }

            builder.Append(template[index++]);
        }

        result = builder.ToString();
        return true;
    }

    private static bool TryReadName(
        string value,
        int start,
        out string name,
        out int next)
    {
        name = string.Empty;
        next = start + 1;
        var end = value.IndexOf('}', start + 1);
        if (end <= start + 1)
            return false;
        var candidate = value[(start + 1)..end];
        if (!IsIdentifier(candidate))
            return false;
        name = candidate;
        next = end + 1;
        return true;
    }

    private static bool IsIdentifier(string value) =>
        value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character =>
            char.IsLetterOrDigit(character) || character == '_');

    private static string WildcardToRegex(string pattern)
    {
        var builder = new StringBuilder(pattern.Length + 16);
        for (var index = 0; index < pattern.Length;)
        {
            if (pattern[index] == '{' &&
                TryReadName(pattern, index, out var name, out var next))
            {
                builder.Append("(?<").Append(name).Append(">.+?)");
                index = next;
                continue;
            }

            if (pattern[index] == '*')
            {
                builder.Append(".*?");
                index++;
                continue;
            }

            builder.Append(Regex.Escape(pattern[index].ToString()));
            index++;
        }

        return builder.ToString();
    }
}
