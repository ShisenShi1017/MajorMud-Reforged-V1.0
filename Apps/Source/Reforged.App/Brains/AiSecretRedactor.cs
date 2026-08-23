using System.Text.RegularExpressions;

namespace Reforged.App.Brains;

public static partial class AiSecretRedactor
{
    public const string Redacted = "[REDACTED CREDENTIAL]";

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var result = AuthorizationHeaderRegex().Replace(value, "$1" + Redacted);
        result = NvidiaKeyRegex().Replace(result, Redacted);
        result = JsonSecretRegex().Replace(result, "$1" + Redacted + "$3");
        return result;
    }

    [GeneratedRegex("(?i)(authorization\\s*[:=]\\s*(?:bearer\\s+)?)[^\\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex("(?i)\\bnvapi-[A-Za-z0-9_-]{12,}\\b", RegexOptions.CultureInvariant)]
    private static partial Regex NvidiaKeyRegex();

    [GeneratedRegex("(?i)([\\\"]?(?:api[_-]?key|token|secret|password)[\\\"]?\\s*[:=]\\s*[\\\"]?)([^\\\"\\s,}]+)([\\\"]?)", RegexOptions.CultureInvariant)]
    private static partial Regex JsonSecretRegex();
}
