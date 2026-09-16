namespace RealmGate.Client.Terminal;

public sealed record ClassicTerminalSubmission(string Text, bool Sensitive);

/// <summary>
/// Framework-neutral command-line editing state for a classic terminal.
/// ANSI interpretation remains in <see cref="ClassicTerminalEmulator"/>;
/// presentation layers translate platform key events into these operations.
/// </summary>
public sealed class ClassicTerminalInputBuffer
{
    private const int MaximumHistoryEntries = 200;

    private readonly List<string> _history = [];
    private string _text = string.Empty;
    private int _caretIndex;
    private int _historyIndex = -1;
    private bool _isPasswordMode;

    public string Text => _text;
    public int CaretIndex => _caretIndex;
    public bool IsPasswordMode => _isPasswordMode;
    public string DisplayText => _isPasswordMode ? new string('\u2022', _text.Length) : _text;
    public string? CopyableText => _isPasswordMode || _text.Length == 0 ? null : _text;

    public void SetPasswordMode(bool enabled)
    {
        if (_isPasswordMode == enabled)
            return;

        _isPasswordMode = enabled;
        Clear();
    }

    public bool InsertText(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var filtered = string.Concat(value.Where(character => !char.IsControl(character)));
        return InsertCore(filtered);
    }

    public bool PasteText(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return InsertCore(value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal));
    }

    public bool Backspace()
    {
        if (_caretIndex <= 0)
            return false;

        _text = _text.Remove(_caretIndex - 1, 1);
        _caretIndex--;
        return true;
    }

    public bool Delete()
    {
        if (_caretIndex >= _text.Length)
            return false;

        _text = _text.Remove(_caretIndex, 1);
        return true;
    }

    public bool MoveCaret(int delta)
    {
        var next = Math.Clamp(_caretIndex + delta, 0, _text.Length);
        if (next == _caretIndex)
            return false;

        _caretIndex = next;
        return true;
    }

    public bool MoveHome()
    {
        if (_caretIndex == 0)
            return false;

        _caretIndex = 0;
        return true;
    }

    public bool MoveEnd()
    {
        if (_caretIndex == _text.Length)
            return false;

        _caretIndex = _text.Length;
        return true;
    }

    public bool RecallHistory(int direction)
    {
        if (_isPasswordMode || _history.Count == 0 || direction == 0)
            return false;

        if (_historyIndex < 0)
            _historyIndex = _history.Count;

        var next = Math.Clamp(_historyIndex + Math.Sign(direction), 0, _history.Count);
        if (next == _historyIndex)
            return false;

        _historyIndex = next;
        _text = _historyIndex == _history.Count
            ? string.Empty
            : _history[_historyIndex];
        _caretIndex = _text.Length;
        return true;
    }

    public ClassicTerminalSubmission Submit()
    {
        var submission = new ClassicTerminalSubmission(_text, _isPasswordMode);

        if (!_isPasswordMode && _text.Length > 0)
        {
            if (_history.Count == 0 ||
                !string.Equals(_history[^1], _text, StringComparison.Ordinal))
            {
                _history.Add(_text);
            }

            if (_history.Count > MaximumHistoryEntries)
                _history.RemoveAt(0);
        }

        Clear();
        return submission;
    }

    public void Clear()
    {
        _text = string.Empty;
        _caretIndex = 0;
        _historyIndex = -1;
    }

    private bool InsertCore(string value)
    {
        if (value.Length == 0)
            return false;

        _text = _text.Insert(_caretIndex, value);
        _caretIndex += value.Length;
        return true;
    }
}
