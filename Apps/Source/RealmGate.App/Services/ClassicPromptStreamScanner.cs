using System.Text;
using System.Text.RegularExpressions;

namespace RealmGate.App.Services;

/// <summary>
/// Finds MajorMUD status prompts directly in the decoded Telnet character
/// stream. Prompts are rewritten in place and may never become completed
/// lines, so line-based parsing alone can miss intermediate combat ticks.
/// </summary>
internal sealed partial class ClassicPromptStreamScanner
{
    private const int BufferCapacity = 1024;
    private readonly StringBuilder _buffer = new(BufferCapacity);
    private StripState _state;

    public IReadOnlyList<ClassicPromptObservation> Append(ReadOnlySpan<char> text)
    {
        foreach (var value in text)
        {
            switch (_state)
            {
                case StripState.Normal:
                    if (value == '\x1B')
                        _state = StripState.Escape;
                    else if (value == '\u009B')
                        _state = StripState.Csi;
                    else if (value is >= ' ' and <= '~')
                        _buffer.Append(value);
                    break;
                case StripState.Escape:
                    _state = value == '[' ? StripState.Csi : StripState.Normal;
                    break;
                case StripState.Csi:
                    if (value is >= '@' and <= '~')
                        _state = StripState.Normal;
                    break;
            }
        }

        var observations = new List<ClassicPromptObservation>();
        var lastEnd = 0;
        foreach (Match match in PromptPattern().Matches(_buffer.ToString()))
        {
            if (!int.TryParse(match.Groups["hp"].Value, out var health))
                continue;

            var resourceKind = match.Groups["kind"].Value.ToUpperInvariant();
            var resource = int.TryParse(match.Groups["resource"].Value, out var parsed)
                ? parsed
                : 0;
            var position = match.Groups["position"].Value;
            observations.Add(new ClassicPromptObservation(
                health,
                resourceKind,
                resource,
                position.Equals("Resting", StringComparison.OrdinalIgnoreCase),
                position.Equals("Meditating", StringComparison.OrdinalIgnoreCase)));
            lastEnd = match.Index + match.Length;
        }

        if (lastEnd > 0)
            _buffer.Remove(0, lastEnd);
        if (_buffer.Length > BufferCapacity)
            _buffer.Remove(0, _buffer.Length - BufferCapacity);
        return observations;
    }

    public void Reset()
    {
        _buffer.Clear();
        _state = StripState.Normal;
    }

    private enum StripState : byte { Normal, Escape, Csi }

    [GeneratedRegex(
        @"\[\s*(?:HP|H)\s*[=:]\s*(?<hp>\d+)(?:\s*/\s*(?<kind>MA|MP|MANA|KAI)\s*[=:]\s*(?<resource>\d+))?(?:\s*\(\s*(?<position>Resting|Meditating)\s*\))?\s*\]\s*:?(?:\s*\(\s*(?<position>Resting|Meditating)\s*\))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PromptPattern();
}

internal readonly record struct ClassicPromptObservation(
    int Health,
    string ResourceKind,
    int Resource,
    bool IsResting,
    bool IsMeditating);
