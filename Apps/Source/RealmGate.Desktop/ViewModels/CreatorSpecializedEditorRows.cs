using System.ComponentModel;
using System.Runtime.CompilerServices;
using RealmGate.App.Services;

namespace RealmGate.Desktop.ViewModels;

public sealed class CreatorClassicAccessRow(ClassicAdministrationCharacter source, bool hasSysopAccess) : INotifyPropertyChanged
{
    private bool _hasSysopAccess = hasSysopAccess;

    public event PropertyChangedEventHandler? PropertyChanged;
    public string CharacterId { get; } = source.CharacterId;
    public string CharacterName { get; } = source.DisplayName;
    public string AccountId { get; } = source.AccountId;
    public int Level { get; } = source.Level;
    public string Source { get; } = source.Source;

    public bool HasSysopAccess
    {
        get => _hasSysopAccess;
        set
        {
            if (_hasSysopAccess == value) return;
            _hasSysopAccess = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSysopAccess)));
        }
    }
}

public sealed class CreatorManagedCharacterRow
{
    public CreatorManagedCharacterRow(EditorCharacterRecord character)
    {
        Character = character;
    }

    public EditorCharacterRecord Character { get; }
    public string CharacterId => Character.CharacterId;
    public string CharacterName => Character.DisplayName;
    public string AccountId => Character.AccountId;
    public string WorldId => Character.WorldId;
    public string RaceName => Character.RaceName;
    public string ClassName => Character.ClassName;
    public int Level => Character.Level;
}

public sealed class CreatorRoomLinkRow(CustomWorldRoomMapLink source) : INotifyPropertyChanged
{
    private string _destinationRoomId = source.DestinationRoomId;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Direction { get; } = source.Direction;
    public string ExitId { get; } = source.ExitId;
    public string DestinationName { get; } = source.DestinationName;

    public string DestinationRoomId
    {
        get => _destinationRoomId;
        set
        {
            value ??= string.Empty;
            if (_destinationRoomId == value) return;
            _destinationRoomId = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DestinationRoomId)));
        }
    }

    public CustomWorldRoomMapLink ToModel() =>
        new(Direction, ExitId, DestinationRoomId, DestinationName);
}

public sealed class CreatorShopInventoryRow : INotifyPropertyChanged
{
    private string _itemRef = string.Empty;
    private int _maxQty;
    private int _normalQty;
    private int _regenQty;
    private int _regenTime;
    private int _regenPercent;

    public CreatorShopInventoryRow()
    {
    }

    public CreatorShopInventoryRow(CustomWorldShopInventoryEntry source)
    {
        _itemRef = source.ItemRef;
        _maxQty = source.MaxQty;
        _normalQty = source.NormalQty;
        _regenQty = source.RegenQty;
        _regenTime = source.RegenTime;
        _regenPercent = source.RegenPercent;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ItemRef { get => _itemRef; set => Set(ref _itemRef, value ?? string.Empty); }
    public int MaxQty { get => _maxQty; set => Set(ref _maxQty, value); }
    public int NormalQty { get => _normalQty; set => Set(ref _normalQty, value); }
    public int RegenQty { get => _regenQty; set => Set(ref _regenQty, value); }
    public int RegenTime { get => _regenTime; set => Set(ref _regenTime, value); }
    public int RegenPercent { get => _regenPercent; set => Set(ref _regenPercent, value); }

    public CustomWorldShopInventoryEntry ToModel() =>
        new(ItemRef, MaxQty, NormalQty, RegenQty, RegenTime, RegenPercent);

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class CreatorMapRoomRow : INotifyPropertyChanged
{
    private double _x;
    private double _y;

    public CreatorMapRoomRow(CustomWorldVisualMapRoom source)
    {
        RoomId = source.RoomId;
        Name = source.Name;
        MapNumber = source.MapNumber;
        RoomNumber = source.RoomNumber;
        Marker = source.Marker;
        _x = source.X;
        _y = source.Y;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string RoomId { get; }
    public string Name { get; }
    public int MapNumber { get; }
    public int RoomNumber { get; }
    public string Marker { get; }
    public double X { get => _x; set => Set(ref _x, Math.Max(0, value)); }
    public double Y { get => _y; set => Set(ref _y, Math.Max(0, value)); }

    public CustomWorldVisualMapPosition ToPosition() =>
        new(RoomId, MapNumber, X, Y);

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class CreatorMapEdgeRow(CustomWorldVisualMapEdge source)
{
    public string ExitId { get; } = source.ExitId;
    public string FromRoomId { get; } = source.FromRoomId;
    public string ToRoomId { get; } = source.ToRoomId;
    public string Direction { get; } = source.Direction;
}

public sealed class CreatorNpcAttackRow : INotifyPropertyChanged
{
    private int _slot;
    private int _type;
    private int _minimumDamage;
    private int _maximumDamage;
    private int _selectionPercent;
    private int _accuracy;
    private int _hitSpellLegacyId;
    private int _energyCost;
    private int _hitMessageLegacyId;
    private int _missMessageLegacyId;
    private int _dodgeMessageLegacyId;
    private string _attackVerb = string.Empty;
    private string _missVerb = string.Empty;
    private string _weaponName = string.Empty;
    private bool _nativeEmptyMissArgumentsVerified;

    public CreatorNpcAttackRow(int slot)
    {
        SourceIndex = -1;
        _slot = slot;
        _type = 1;
        _selectionPercent = 100;
        _energyCost = 1000;
    }

    public CreatorNpcAttackRow(CustomWorldNpcAttackEntry source)
    {
        SourceIndex = source.SourceIndex;
        _slot = source.Slot;
        _type = source.Type;
        _minimumDamage = source.MinimumDamage;
        _maximumDamage = source.MaximumDamage;
        _selectionPercent = source.SelectionPercent;
        _accuracy = source.Accuracy;
        _hitSpellLegacyId = source.HitSpellLegacyId;
        _energyCost = source.EnergyCost;
        _hitMessageLegacyId = source.HitMessageLegacyId;
        _missMessageLegacyId = source.MissMessageLegacyId;
        _dodgeMessageLegacyId = source.DodgeMessageLegacyId;
        _attackVerb = source.AttackVerb;
        _missVerb = source.MissVerb;
        _weaponName = source.WeaponName;
        _nativeEmptyMissArgumentsVerified = source.NativeEmptyMissArgumentsVerified;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public int SourceIndex { get; }
    public int Slot { get => _slot; set => Set(ref _slot, value); }
    public int Type { get => _type; set => Set(ref _type, value); }
    public int MinimumDamage { get => _minimumDamage; set => Set(ref _minimumDamage, value); }
    public int MaximumDamage { get => _maximumDamage; set => Set(ref _maximumDamage, value); }
    public int SelectionPercent { get => _selectionPercent; set => Set(ref _selectionPercent, value); }
    public int Accuracy { get => _accuracy; set => Set(ref _accuracy, value); }
    public int HitSpellLegacyId { get => _hitSpellLegacyId; set => Set(ref _hitSpellLegacyId, value); }
    public int EnergyCost { get => _energyCost; set => Set(ref _energyCost, value); }
    public int HitMessageLegacyId { get => _hitMessageLegacyId; set => Set(ref _hitMessageLegacyId, value); }
    public int MissMessageLegacyId { get => _missMessageLegacyId; set => Set(ref _missMessageLegacyId, value); }
    public int DodgeMessageLegacyId { get => _dodgeMessageLegacyId; set => Set(ref _dodgeMessageLegacyId, value); }
    public string AttackVerb { get => _attackVerb; set => Set(ref _attackVerb, value ?? string.Empty); }
    public string MissVerb { get => _missVerb; set => Set(ref _missVerb, value ?? string.Empty); }
    public string WeaponName { get => _weaponName; set => Set(ref _weaponName, value ?? string.Empty); }
    public bool NativeEmptyMissArgumentsVerified { get => _nativeEmptyMissArgumentsVerified; set => Set(ref _nativeEmptyMissArgumentsVerified, value); }

    public CustomWorldNpcAttackEntry ToModel() => new(
        SourceIndex, Slot, Type, MinimumDamage, MaximumDamage, SelectionPercent, Accuracy,
        HitSpellLegacyId, EnergyCost, HitMessageLegacyId, MissMessageLegacyId,
        DodgeMessageLegacyId, AttackVerb, MissVerb, WeaponName, NativeEmptyMissArgumentsVerified);

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class CreatorNpcAbilityRow : INotifyPropertyChanged
{
    private int _abilityId;
    private string _name = string.Empty;
    private int _value;

    public CreatorNpcAbilityRow()
    {
        SourceIndex = -1;
    }

    public CreatorNpcAbilityRow(CustomWorldNpcAbilityEntry source)
    {
        SourceIndex = source.SourceIndex;
        _abilityId = source.AbilityId;
        _name = source.Name;
        _value = source.Value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public int SourceIndex { get; }
    public int AbilityId { get => _abilityId; set => Set(ref _abilityId, value); }
    public string Name { get => _name; set => Set(ref _name, value ?? string.Empty); }
    public int Value { get => _value; set => Set(ref _value, value); }

    public CustomWorldNpcAbilityEntry ToModel() => new(SourceIndex, AbilityId, Name, Value);

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class CreatorQuestStageRow(CustomWorldQuestStageEntry source)
{
    public int StageIndex { get; } = source.StageIndex;
    public int TextblockId { get; } = source.TextblockId;
    public string TextblockRef { get; } = source.TextblockRef;
    public string ScriptId { get; } = source.ScriptId;
    public string SourceScript { get; } = source.SourceScript;
    public int OperationCount { get; } = source.OperationCount;
    public string OperationSummary { get; } = source.OperationSummary;
    public string StageLabel => $"Stage {StageIndex}";
    public string OperationCountLabel => $"{OperationCount:N0} operations";
}

public sealed class CreatorScriptOperationRow : INotifyPropertyChanged
{
    private int _line;
    private int _sequence;
    private string _opcode;
    private string _arguments;

    public CreatorScriptOperationRow(int line, int sequence, IReadOnlyList<string> availableOpcodes)
    {
        SourceIndex = -1;
        _line = line;
        _sequence = sequence;
        _opcode = "message";
        _arguments = string.Empty;
        OperationKind = "presentation";
        Raw = string.Empty;
        AvailableOpcodes = availableOpcodes;
    }

    public CreatorScriptOperationRow(CustomWorldScriptOperationEntry source, IReadOnlyList<string> availableOpcodes)
    {
        SourceIndex = source.SourceIndex;
        _line = source.Line;
        _sequence = source.Sequence;
        _opcode = source.Opcode;
        _arguments = source.Arguments;
        OperationKind = source.OperationKind;
        Raw = source.Raw;
        AvailableOpcodes = availableOpcodes;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SourceIndex { get; }
    public int Line { get => _line; set => Set(ref _line, value); }
    public int Sequence { get => _sequence; set => Set(ref _sequence, value); }
    public string Opcode { get => _opcode; set => Set(ref _opcode, value ?? string.Empty); }
    public string Arguments { get => _arguments; set => Set(ref _arguments, value ?? string.Empty); }
    public string OperationKind { get; }
    public string Raw { get; }
    public IReadOnlyList<string> AvailableOpcodes { get; }

    public CustomWorldScriptOperationEntry ToModel() =>
        new(SourceIndex, Line, Sequence, Opcode, Arguments, OperationKind, Raw);

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
