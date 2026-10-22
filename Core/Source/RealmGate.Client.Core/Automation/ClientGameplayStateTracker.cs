using System.Text;
using System.Text.RegularExpressions;

namespace RealmGate.Client.Automation;

/// <summary>
/// Tracks only state proven by the terminal stream. Unknown combat state stays
/// unknown so combat-restricted automation cannot run on an assumption.
/// </summary>
public sealed partial class ClientGameplayStateTracker
{
    private const int BufferCapacity = 2048;
    private readonly object _sync = new();
    private readonly StringBuilder _plain = new(BufferCapacity);
    private StripState _stripState;
    private bool _gameplayReady;
    private bool? _inCombat;

    public bool GameplayReady { get { lock (_sync) return _gameplayReady; } }
    public bool? InCombat { get { lock (_sync) return _inCombat; } }

    public void Append(ReadOnlySpan<char> text)
    {
        lock (_sync)
        {
        foreach (var value in text)
        {
            switch (_stripState)
            {
                case StripState.Normal:
                    if (value == '\x1b') _stripState = StripState.Escape;
                    else if (value == '\u009b') _stripState = StripState.Csi;
                    else if (value is '\r' or '\n' or >= ' ') _plain.Append(value);
                    break;
                case StripState.Escape:
                    _stripState = value == '[' ? StripState.Csi : StripState.Normal;
                    break;
                case StripState.Csi:
                    if (value is >= '@' and <= '~') _stripState = StripState.Normal;
                    break;
            }
        }

        var content = _plain.ToString();
        if (GamePrompt().IsMatch(content)) _gameplayReady = true;
        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("*Combat Engaged*", StringComparison.OrdinalIgnoreCase))
                _inCombat = true;
            else if (trimmed.StartsWith("*Combat Off*", StringComparison.OrdinalIgnoreCase))
                _inCombat = false;
        }

        if (_plain.Length > BufferCapacity)
            _plain.Remove(0, _plain.Length - BufferCapacity);
        }
    }

    public void MarkGameplayReady()
    {
        lock (_sync) _gameplayReady = true;
    }

    public void Reset()
    {
        lock (_sync)
        {
        _plain.Clear();
        _stripState = StripState.Normal;
        _gameplayReady = false;
        _inCombat = null;
        }
    }

    private enum StripState : byte { Normal, Escape, Csi }

    [GeneratedRegex(@"\[\s*(?:HP|H)\s*[=:]\s*\d+[^\]]*\]\s*:?")]
    private static partial Regex GamePrompt();
}
