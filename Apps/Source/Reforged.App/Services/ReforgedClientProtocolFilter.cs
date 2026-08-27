using System.Text;
using System.Text.RegularExpressions;

namespace Reforged.App.Services;

internal sealed class ReforgedClientProtocolFilter
{
    private const string Prefix = "@@REFORGED-PROTOCOL|";
    private readonly StringBuilder _candidate = new();
    private bool _atLineStart = true;
    private bool _capturingProtocol;
    private bool _skipLineFeed;
    private sealed class HiddenCommandEchoExpectation
    {
        public HiddenCommandEchoExpectation(string command, DateTimeOffset createdAt)
        {
            Command = command;
            CreatedAt = createdAt;
        }

        public string Command { get; }
        public DateTimeOffset CreatedAt { get; }
    }

    private readonly Queue<HiddenCommandEchoExpectation> _hiddenCommandEchoes = new();
    private readonly StringBuilder _hiddenEchoCandidate = new();

    public void NoteOutboundCommand(string command)
    {
        var normalized = command?.Trim() ?? string.Empty;
        if (normalized.Length == 0) return;

        // Reforged's Telnet host echoes input one byte at a time. Keep every
        // private protocol command in send order so its echoed characters can
        // be removed even when TCP splits the echo across multiple reads.
        _hiddenCommandEchoes.Enqueue(new HiddenCommandEchoExpectation(normalized, DateTimeOffset.UtcNow));
    }

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
        return new ReforgedClientProtocolFilterResult(RemoveHiddenCommandEchoes(visible.ToString()), messages);
    }

    public void Reset()
    {
        _candidate.Clear();
        _atLineStart = true;
        _capturingProtocol = false;
        _skipLineFeed = false;
        _hiddenCommandEchoes.Clear();
        _hiddenEchoCandidate.Clear();
    }

    private string RemoveHiddenCommandEchoes(string text)
    {
        if (text.Length == 0) return text;

        var visible = new StringBuilder(text.Length);
        FlushExpiredHiddenEchoes(visible, DateTimeOffset.UtcNow);

        foreach (var character in text)
        {
            while (true)
            {
                if (_hiddenCommandEchoes.Count == 0)
                {
                    if (_hiddenEchoCandidate.Length > 0)
                    {
                        visible.Append(_hiddenEchoCandidate);
                        _hiddenEchoCandidate.Clear();
                    }
                    visible.Append(character);
                    break;
                }

                var expected = _hiddenCommandEchoes.Peek().Command;
                var matched = _hiddenEchoCandidate.Length;

                if (matched < expected.Length && character == expected[matched])
                {
                    _hiddenEchoCandidate.Append(character);
                    if (_hiddenEchoCandidate.Length == expected.Length)
                    {
                        // Exact byte-for-byte server echo of the private command:
                        // discard it and advance to the next expected echo.
                        _hiddenEchoCandidate.Clear();
                        _hiddenCommandEchoes.Dequeue();
                        FlushExpiredHiddenEchoes(visible, DateTimeOffset.UtcNow);
                    }
                    break;
                }

                if (_hiddenEchoCandidate.Length > 0)
                {
                    // This looked like a private echo prefix but diverged. It was
                    // ordinary game text, so restore it and re-evaluate the current
                    // character as a possible start of the real echo.
                    visible.Append(_hiddenEchoCandidate);
                    _hiddenEchoCandidate.Clear();
                    continue;
                }

                visible.Append(character);
                break;
            }
        }

        return visible.ToString();
    }

    private void FlushExpiredHiddenEchoes(StringBuilder visible, DateTimeOffset now)
    {
        while (_hiddenCommandEchoes.Count > 0 &&
               now - _hiddenCommandEchoes.Peek().CreatedAt > TimeSpan.FromSeconds(15))
        {
            // If a false-positive prefix was being held when the expectation
            // expired, restore it rather than losing legitimate game output.
            if (_hiddenEchoCandidate.Length > 0)
            {
                visible.Append(_hiddenEchoCandidate);
                _hiddenEchoCandidate.Clear();
            }
            _hiddenCommandEchoes.Dequeue();
        }
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
