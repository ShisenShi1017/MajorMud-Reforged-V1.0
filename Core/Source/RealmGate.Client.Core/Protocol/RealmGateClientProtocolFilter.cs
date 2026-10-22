using System.Text;

namespace RealmGate.Client.Protocol;

public sealed class RealmGateClientProtocolFilter
{
    // This marker is part of the established server wire contract. Keep it
    // stable even though the desktop product is now named RealmGate.
    private const string Prefix = "@@REFORGED-PROTOCOL|";
    private readonly StringBuilder _candidate = new();
    private readonly Queue<HiddenCommandEchoExpectation> _hiddenCommandEchoes = new();
    private readonly StringBuilder _hiddenEchoCandidate = new();
    private bool _atLineStart = true;
    private bool _capturingProtocol;
    private bool _skipLineFeed;

    public void NoteOutboundCommand(string command)
    {
        var normalized = command?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            return;

        // The Telnet host echoes input one byte at a time. Keep every private
        // protocol command in send order so split TCP reads remain invisible.
        _hiddenCommandEchoes.Enqueue(new HiddenCommandEchoExpectation(
            normalized,
            DateTimeOffset.UtcNow));
    }

    public RealmGateClientProtocolFilterResult Process(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var visible = new StringBuilder(text.Length);
        var messages = new List<RealmGateClientProtocolMessage>();
        foreach (var character in text)
        {
            if (_skipLineFeed)
            {
                _skipLineFeed = false;
                if (character == '\n')
                    continue;
            }

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
                else
                {
                    _candidate.Append(character);
                }

                continue;
            }

            if (_atLineStart)
            {
                _candidate.Append(character);
                var candidate = _candidate.ToString();
                if (Prefix.StartsWith(candidate, StringComparison.Ordinal))
                {
                    if (candidate.Length == Prefix.Length)
                        _capturingProtocol = true;
                    continue;
                }

                visible.Append(candidate);
                _candidate.Clear();
                _atLineStart = character is '\r' or '\n';
                continue;
            }

            visible.Append(character);
            if (character is '\r' or '\n')
                _atLineStart = true;
        }

        return new RealmGateClientProtocolFilterResult(
            RemoveHiddenCommandEchoes(visible.ToString()),
            messages);
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
        if (text.Length == 0)
            return text;

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
                        _hiddenEchoCandidate.Clear();
                        _hiddenCommandEchoes.Dequeue();
                        FlushExpiredHiddenEchoes(visible, DateTimeOffset.UtcNow);
                    }

                    break;
                }

                if (_hiddenEchoCandidate.Length > 0)
                {
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
            if (_hiddenEchoCandidate.Length > 0)
            {
                visible.Append(_hiddenEchoCandidate);
                _hiddenEchoCandidate.Clear();
            }

            _hiddenCommandEchoes.Dequeue();
        }
    }

    private void Decode(ICollection<RealmGateClientProtocolMessage> messages)
    {
        var frame = _candidate.ToString();
        var separator = frame.IndexOf('|', Prefix.Length);
        if (separator < 0)
            return;

        try
        {
            messages.Add(new RealmGateClientProtocolMessage(
                frame[Prefix.Length..separator],
                Encoding.UTF8.GetString(Convert.FromBase64String(frame[(separator + 1)..]))));
        }
        catch (FormatException)
        {
        }
    }

    private sealed record HiddenCommandEchoExpectation(
        string Command,
        DateTimeOffset CreatedAt);
}

public sealed record RealmGateClientProtocolMessage(string Channel, string Payload);

public sealed record RealmGateClientProtocolFilterResult(
    string VisibleText,
    IReadOnlyList<RealmGateClientProtocolMessage> Messages);
