using System.Text;

namespace Reforged.App.Services;

internal sealed class ReforgedClientProtocolFilter
{
    private const string Prefix = "@@REFORGED-PROTOCOL|";
    private readonly StringBuilder _candidate = new();
    private bool _atLineStart = true;
    private bool _capturingProtocol;
    private bool _skipLineFeed;

    public ReforgedClientProtocolFilterResult Process(string text)
    {
        var visible = new StringBuilder(text.Length);
        var messages = new List<ReforgedClientProtocolMessage>();
        foreach (var character in text)
        {
            if (_skipLineFeed) { _skipLineFeed = false; if (character == '\n') continue; }
            if (_capturingProtocol)
            {
                if (character is '\r' or '\n')
                {
                    Decode(messages);
                    _candidate.Clear();
                    _capturingProtocol = false;
                    _atLineStart = true;
                    _skipLineFeed = character == '\r';
                }
                else _candidate.Append(character);
                continue;
            }
            if (_atLineStart)
            {
                _candidate.Append(character);
                var candidate = _candidate.ToString();
                if (Prefix.StartsWith(candidate, StringComparison.Ordinal))
                {
                    if (candidate.Length == Prefix.Length) _capturingProtocol = true;
                    continue;
                }
                visible.Append(candidate);
                _candidate.Clear();
                _atLineStart = character is '\r' or '\n';
                continue;
            }
            visible.Append(character);
            if (character is '\r' or '\n') _atLineStart = true;
        }
        return new ReforgedClientProtocolFilterResult(visible.ToString(), messages);
    }

    public void Reset()
    {
        _candidate.Clear();
        _atLineStart = true;
        _capturingProtocol = false;
        _skipLineFeed = false;
    }

    private void Decode(ICollection<ReforgedClientProtocolMessage> messages)
    {
        var frame = _candidate.ToString();
        var separator = frame.IndexOf('|', Prefix.Length);
        if (separator < 0) return;
        try
        {
            messages.Add(new ReforgedClientProtocolMessage(
                frame[Prefix.Length..separator],
                Encoding.UTF8.GetString(Convert.FromBase64String(frame[(separator + 1)..]))));
        }
        catch (FormatException) { }
    }
}

internal sealed record ReforgedClientProtocolMessage(string Channel, string Payload);
internal sealed record ReforgedClientProtocolFilterResult(string VisibleText, IReadOnlyList<ReforgedClientProtocolMessage> Messages);
