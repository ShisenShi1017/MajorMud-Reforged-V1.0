using System.Text;

namespace RealmGate.Server;

/// <summary>
/// Owns raw terminal line termination and asynchronous prompt refresh formatting.
/// Combat/gameplay code supplies messages; the session/terminal layer decides when to redraw.
/// </summary>
public static class RuntimeTerminalOutputFormatter
{
    public const string NetworkNewLine = "\r\n";

    public static string NormalizeNetworkNewLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", NetworkNewLine, StringComparison.Ordinal);
    }

    public static string ComposeAsynchronousOutput(
        IReadOnlyList<string> messages,
        string? prompt,
        bool promptAlreadyVisible)
    {
        if (messages.Count == 0) return string.Empty;

        var output = new StringBuilder();
        if (promptAlreadyVisible)
            output.Append(NetworkNewLine);

        foreach (var message in messages)
        {
            if (string.IsNullOrEmpty(message)) continue;
            var normalized = NormalizeNetworkNewLines(message).TrimEnd('\r', '\n');
            if (normalized.Length == 0) continue;
            output.Append(normalized);
            output.Append(NetworkNewLine);
        }

        if (!string.IsNullOrEmpty(prompt))
            output.Append(prompt);

        return output.ToString();
    }

    public static bool ContainsOnlyCrLfLineEndings(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                if (index + 1 >= text.Length || text[index + 1] != '\n') return false;
                index++;
                continue;
            }

            if (text[index] == '\n') return false;
        }

        return true;
    }

    public static int CountOccurrences(string text, string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }
}
