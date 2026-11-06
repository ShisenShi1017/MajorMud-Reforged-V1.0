using System.ComponentModel;
using System.Runtime.CompilerServices;
using RealmGate.App.Services;

namespace RealmGate.Desktop.ViewModels;

public sealed class CreatorFieldEditorRow : INotifyPropertyChanged
{
    private string _textValue;
    private bool _boolValue;
    private CustomWorldSimpleOption? _selectedOption;
    private bool _isDirty;

    public CreatorFieldEditorRow(
        CustomWorldSimpleFieldDefinition definition,
        string? textValue = null,
        bool? boolValue = null)
    {
        Definition = definition;
        _textValue = textValue ?? definition.DefaultValue;
        _boolValue = boolValue ?? (bool.TryParse(definition.DefaultValue, out var selected) && selected);
        _selectedOption = definition.Options?.FirstOrDefault(option =>
            option.Value.Equals(_textValue, StringComparison.OrdinalIgnoreCase));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CustomWorldSimpleFieldDefinition Definition { get; }
    public string Path => Definition.Path;
    public string Label => Definition.Required ? Definition.Label + " *" : Definition.Label;
    public string HelpText => Definition.HelpText;
    public CustomWorldSimpleFieldKind Kind => Definition.Kind;
    public IReadOnlyList<CustomWorldSimpleOption> Options => Definition.Options ?? [];
    public bool IsBoolean => Kind == CustomWorldSimpleFieldKind.Boolean;
    public bool IsChoice => Kind == CustomWorldSimpleFieldKind.Choice;
    public bool IsText => !IsBoolean && !IsChoice;
    public bool IsMultiline => Kind is CustomWorldSimpleFieldKind.Multiline or
        CustomWorldSimpleFieldKind.Lines or
        CustomWorldSimpleFieldKind.IntegerLines or
        CustomWorldSimpleFieldKind.DecimalLines or
        CustomWorldSimpleFieldKind.BooleanLines or
        CustomWorldSimpleFieldKind.ObjectLines;
    public double EditorHeight => IsMultiline ? 100 : 34;

    public string TextValue
    {
        get => _textValue;
        set
        {
            if (!Set(ref _textValue, value ?? string.Empty)) return;
            IsDirty = true;
        }
    }

    public bool BoolValue
    {
        get => _boolValue;
        set
        {
            if (!Set(ref _boolValue, value)) return;
            IsDirty = true;
        }
    }

    public CustomWorldSimpleOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (!Set(ref _selectedOption, value)) return;
            IsDirty = true;
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => Set(ref _isDirty, value);
    }

    public string ValueForSave() => IsChoice ? SelectedOption?.Value ?? string.Empty : TextValue;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
