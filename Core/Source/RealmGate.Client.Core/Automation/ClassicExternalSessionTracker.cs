using System.Text;
using RealmGate.Client.Gameplay;
using RealmGate.Client.Navigation;
using RealmGate.Client.Terminal;

namespace RealmGate.Client.Automation;

public sealed record ClassicExternalSessionSnapshot(
    string CharacterName,
    string RaceName,
    string ClassName,
    int? HealthCurrent,
    int? HealthMaximum,
    int? ResourceCurrent,
    int? ResourceMaximum,
    long? Experience,
    long? ExperienceNeeded,
    long? ExperienceRemaining,
    int? Level,
    int? Lives,
    int? CharacterPoints,
    IReadOnlyDictionary<string, int> Attributes,
    IReadOnlyDictionary<string, int> Skills,
    IReadOnlyDictionary<string, int> DerivedStats,
    IReadOnlyDictionary<string, int> Resistances,
    IReadOnlyList<string> ActiveEffects,
    IReadOnlyList<ClassicLearnedSpellObservation> Spells,
    IReadOnlyList<ClassicPartyMemberObservation> Party,
    ClassicRoomDisplayObservation? Room,
    bool? InCombat,
    DateTimeOffset CapturedAtUtc);

/// <summary>
/// Builds a read-only snapshot from source-bounded Classic terminal screens.
/// It never supplies qualified RealmGate identity or automation authority.
/// </summary>
public sealed class ClassicExternalSessionTracker
{
    private readonly object _sync = new();
    private readonly StringBuilder _buffer = new();
    private readonly ClassicStatScreenParser _stats = new();
    private readonly ClassicSpellbookParser _spells = new();
    private readonly ClassicPartyScreenParser _party = new();
    private readonly ClassicRoomDisplayStreamParser _room = new();
    private readonly ClassicCombatStreamParser _combat = new();
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Dictionary<string, int> _attributes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _skills = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _derivedStats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _resistances = new(StringComparer.OrdinalIgnoreCase);
    private string _characterName = string.Empty;
    private string _raceName = string.Empty;
    private string _className = string.Empty;
    private int? _healthCurrent;
    private int? _healthMaximum;
    private int? _resourceCurrent;
    private int? _resourceMaximum;
    private long? _experience;
    private long? _experienceNeeded;
    private long? _experienceRemaining;
    private int? _level;
    private int? _lives;
    private int? _characterPoints;
    private IReadOnlyList<string> _activeEffects = [];
    private IReadOnlyList<ClassicLearnedSpellObservation> _learnedSpells = [];
    private IReadOnlyList<ClassicPartyMemberObservation> _partyMembers = [];
    private ClassicRoomDisplayObservation? _currentRoom;
    private bool? _inCombat;
    private ClassicExternalSessionSnapshot? _snapshot;

    public ClassicExternalSessionTracker(Func<DateTimeOffset>? utcNow = null) =>
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

    public ClassicExternalSessionSnapshot? Snapshot
    {
        get { lock (_sync) return _snapshot; }
    }

    public void NoteOutboundCommand(string? command)
    {
        lock (_sync)
        {
            _stats.NoteOutboundCommand(command);
            _spells.NoteOutboundCommand(command);
            _party.NoteOutboundCommand(command);
            _room.NoteOutboundCommand(command);
        }
    }

    public ClassicExternalSessionSnapshot? Append(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        lock (_sync)
        {
            _buffer.Append(text);
            var buffered = _buffer.ToString();
            var start = 0;
            var changed = false;
            for (var index = 0; index < buffered.Length; index++)
            {
                if (buffered[index] is not ('\r' or '\n')) continue;
                changed |= ObserveLine(buffered[start..index]);
                if (buffered[index] == '\r' && index + 1 < buffered.Length && buffered[index + 1] == '\n') index++;
                start = index + 1;
            }

            _buffer.Clear();
            if (start < buffered.Length) _buffer.Append(buffered[start..]);
            var tail = AnsiTextParser.StripFormatting(_buffer.ToString()).Trim();
            if (tail == ">" || tail.StartsWith("[HP=", StringComparison.OrdinalIgnoreCase) ||
                tail.StartsWith("[H=", StringComparison.OrdinalIgnoreCase))
            {
                changed |= ObserveLine(tail);
                _buffer.Clear();
            }
            if (_buffer.Length > 8192) _buffer.Remove(0, _buffer.Length - 8192);
            return changed ? Capture() : null;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _buffer.Clear();
            _stats.Reset();
            _spells.Reset();
            _party.Reset();
            _room.Reset();
            _combat.Reset();
            _attributes.Clear();
            _skills.Clear();
            _derivedStats.Clear();
            _resistances.Clear();
            _characterName = _raceName = _className = string.Empty;
            _healthCurrent = _healthMaximum = _resourceCurrent = _resourceMaximum = null;
            _experience = _experienceNeeded = _experienceRemaining = null;
            _level = _lives = _characterPoints = null;
            _activeEffects = [];
            _learnedSpells = [];
            _partyMembers = [];
            _currentRoom = null;
            _inCombat = null;
            _snapshot = null;
        }
    }

    private bool ObserveLine(string line)
    {
        var text = AnsiTextParser.StripFormatting(line);
        var changed = false;
        if (_stats.Observe(text) is { } stat)
        {
            if (stat.CharacterName.Length > 0) _characterName = stat.CharacterName;
            if (stat.RaceName.Length > 0) _raceName = stat.RaceName;
            if (stat.ClassName.Length > 0) _className = stat.ClassName;
            _healthCurrent = stat.HealthCurrent ?? _healthCurrent;
            _healthMaximum = stat.HealthMaximum ?? _healthMaximum;
            _resourceCurrent = stat.ManaCurrent ?? _resourceCurrent;
            _resourceMaximum = stat.ManaMaximum ?? _resourceMaximum;
            _experience = stat.Experience ?? _experience;
            _experienceNeeded = stat.ExperienceNeeded ?? _experienceNeeded;
            _experienceRemaining = stat.ExperienceRemaining ?? _experienceRemaining;
            _level = stat.Level ?? _level;
            _lives = stat.Lives ?? _lives;
            _characterPoints = stat.CharacterPoints ?? _characterPoints;
            Merge(_attributes, stat.Attributes);
            Merge(_skills, stat.Skills);
            Merge(_derivedStats, stat.DerivedStats);
            Merge(_resistances, stat.Resistances);
            if (stat.ScreenComplete) _activeEffects = stat.ActiveEffects?.ToArray() ?? [];
            changed = true;
        }
        if (_spells.Observe(text) is { } spells)
        {
            _learnedSpells = spells.ToArray();
            changed = true;
        }
        if (_party.Observe(text) is { } party)
        {
            _partyMembers = party.ToArray();
            changed = true;
        }
        if (_room.Observe(text) is { } room)
        {
            _currentRoom = room;
            changed = true;
        }
        var combat = _combat.Observe(text);
        if (combat.Kind != ClassicCombatEventKind.None)
        {
            _inCombat = combat.InCombat;
            changed = true;
        }
        return changed;
    }

    private ClassicExternalSessionSnapshot Capture()
    {
        _snapshot = new ClassicExternalSessionSnapshot(
            _characterName, _raceName, _className, _healthCurrent, _healthMaximum,
            _resourceCurrent, _resourceMaximum, _experience, _experienceNeeded,
            _experienceRemaining, _level, _lives, _characterPoints,
            new Dictionary<string, int>(_attributes, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(_skills, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(_derivedStats, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(_resistances, StringComparer.OrdinalIgnoreCase),
            _activeEffects.ToArray(), _learnedSpells.ToArray(), _partyMembers.ToArray(),
            _currentRoom, _inCombat, _utcNow());
        return _snapshot;
    }

    private static void Merge(IDictionary<string, int> target, IReadOnlyDictionary<string, int>? source)
    {
        if (source is null) return;
        foreach (var pair in source) target[pair.Key] = pair.Value;
    }
}
