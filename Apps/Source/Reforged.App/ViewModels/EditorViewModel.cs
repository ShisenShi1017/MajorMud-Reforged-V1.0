using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;
using Reforged.App.Commands;
using Reforged.App.Services;
using Reforged.Server;

namespace Reforged.App.ViewModels;

public sealed class ClassicCharacterAccessRow : ViewModelBase
{
    private bool _hasSysopAccess;

    public ClassicCharacterAccessRow(ClassicAdministrationCharacter character, bool hasSysopAccess)
    {
        CharacterId = character.CharacterId;
        CharacterName = character.DisplayName;
        AccountId = character.AccountId;
        Level = character.Level;
        Source = character.Source;
        _hasSysopAccess = hasSysopAccess;
    }

    public string CharacterId { get; }
    public string CharacterName { get; }
    public string AccountId { get; }
    public int Level { get; }
    public string Source { get; }
    public bool HasSysopAccess { get => _hasSysopAccess; set => Set(ref _hasSysopAccess, value); }
}

public sealed class CustomWorldOverrideRow : ViewModelBase
{
    public CustomWorldOverrideRow(CustomWorldOverrideSummary summary)
    {
        Family = summary.Family;
        TargetId = summary.TargetId;
        Operation = summary.Operation;
        FieldCount = summary.FieldCount;
        FieldsJson = summary.FieldsJson;
    }

    public string Family { get; }
    public string TargetId { get; }
    public string Operation { get; }
    public int FieldCount { get; }
    public string FieldsJson { get; }
}

public sealed class CustomWorldObjectRow : ViewModelBase
{
    public CustomWorldObjectRow(CustomWorldObjectSummary summary)
    {
        Family = summary.Family;
        Id = summary.Id;
        Name = summary.Name;
    }

    public string Family { get; }
    public string Id { get; }
    public string Name { get; }
}

public sealed class CustomWorldSimpleFieldRow : ViewModelBase
{
    private string _textValue;
    private bool _boolValue;
    private bool _isDirty;

    public CustomWorldSimpleFieldRow(CustomWorldSimpleFieldDefinition definition, string? textValue = null, bool? boolValue = null)
    {
        Definition = definition;
        _textValue = textValue ?? definition.DefaultValue;
        _boolValue = boolValue ?? (bool.TryParse(definition.DefaultValue, out var parsed) && parsed);
    }

    public CustomWorldSimpleFieldDefinition Definition { get; }
    public string Path => Definition.Path;
    public string Label => Definition.Label;
    public string LabelWithRequired => Definition.Required ? Definition.Label + " *" : Definition.Label;
    public string HelpText => Definition.HelpText;
    public CustomWorldSimpleFieldKind Kind => Definition.Kind;
    public IReadOnlyList<CustomWorldSimpleOption> Options => Definition.Options ?? [];
    public bool IsText => Kind is CustomWorldSimpleFieldKind.Text or CustomWorldSimpleFieldKind.Multiline or CustomWorldSimpleFieldKind.Integer or
        CustomWorldSimpleFieldKind.Lines or CustomWorldSimpleFieldKind.IntegerLines or CustomWorldSimpleFieldKind.Decimal or
        CustomWorldSimpleFieldKind.DecimalLines or CustomWorldSimpleFieldKind.BooleanLines or CustomWorldSimpleFieldKind.ObjectLines;
    public bool IsChoice => Kind == CustomWorldSimpleFieldKind.Choice;
    public bool IsBoolean => Kind == CustomWorldSimpleFieldKind.Boolean;
    public bool IsRequired => Definition.Required;
    public string ReferenceFamily => InferReferenceFamily(Path, Kind);
    public bool HasReferenceLookup => ReferenceFamily.Length > 0;
    public string ReferenceLookupLabel => ReferenceFamily.Length == 0 ? string.Empty : "Find " + CustomWorldSimpleFormCatalog.LabelForFamily(ReferenceFamily);
    public double InputHeight => Path.Equals("payload", StringComparison.OrdinalIgnoreCase) ? 340 :
        Path.EndsWith("payload", StringComparison.OrdinalIgnoreCase) ? 220 :
        Kind is CustomWorldSimpleFieldKind.Multiline or CustomWorldSimpleFieldKind.Lines or
        CustomWorldSimpleFieldKind.IntegerLines or CustomWorldSimpleFieldKind.DecimalLines or
        CustomWorldSimpleFieldKind.BooleanLines or CustomWorldSimpleFieldKind.ObjectLines ? 112 : 44;
    public bool IsDirty => _isDirty;
    public string TextValue
    {
        get => _textValue;
        set
        {
            if (!Set(ref _textValue, value)) return;
            _isDirty = true;
        }
    }
    public bool BoolValue
    {
        get => _boolValue;
        set
        {
            if (!Set(ref _boolValue, value)) return;
            _isDirty = true;
        }
    }

    private static string InferReferenceFamily(string path, CustomWorldSimpleFieldKind kind)
    {
        if (kind is CustomWorldSimpleFieldKind.Boolean or CustomWorldSimpleFieldKind.DecimalLines or
            CustomWorldSimpleFieldKind.BooleanLines or CustomWorldSimpleFieldKind.ObjectLines or CustomWorldSimpleFieldKind.Multiline)
            return string.Empty;

        var normalized = (path ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0 || normalized is "id" or "legacy_id" or "message_id" or "textblock_id" or "map_number" or "room_number")
            return string.Empty;
        var leaf = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? normalized;
        var looksLikeReference = normalized.StartsWith("relationships.", StringComparison.Ordinal) ||
            leaf.EndsWith("_id", StringComparison.Ordinal) || leaf.EndsWith("_ids", StringComparison.Ordinal) ||
            leaf.EndsWith("_ref", StringComparison.Ordinal) || leaf.EndsWith("_refs", StringComparison.Ordinal) ||
            leaf.EndsWith("_name", StringComparison.Ordinal) || leaf.EndsWith("_names", StringComparison.Ordinal) || leaf.EndsWith("_named", StringComparison.Ordinal) ||
            leaf.Contains("required_item", StringComparison.Ordinal) || leaf.Contains("reward_item", StringComparison.Ordinal) ||
            (leaf.Contains("restriction", StringComparison.Ordinal) && (normalized.Contains("class", StringComparison.Ordinal) || normalized.Contains("race", StringComparison.Ordinal))) ||
            leaf is "resist_ability" or "return_room" or "start_room";
        if (!looksLikeReference) return string.Empty;

        if (normalized.Contains("textblock", StringComparison.Ordinal)) return "native_textblock_payloads";
        if (normalized.Contains("message", StringComparison.Ordinal)) return "native_message_payloads";
        if (normalized.Contains("ability", StringComparison.Ordinal)) return "abilities";
        if (normalized.Contains("spell", StringComparison.Ordinal)) return "spells";
        if (normalized.Contains("item", StringComparison.Ordinal)) return "items";
        if (normalized.Contains("npc", StringComparison.Ordinal) || normalized.Contains("monster", StringComparison.Ordinal)) return "npcs";
        if (normalized.Contains("room", StringComparison.Ordinal)) return "rooms";
        if (normalized.Contains("shop", StringComparison.Ordinal) || normalized.Contains("service", StringComparison.Ordinal)) return "shops";
        if (normalized.Contains("class", StringComparison.Ordinal)) return "classes";
        if (normalized.Contains("race", StringComparison.Ordinal)) return "races";
        if (normalized.Contains("quest", StringComparison.Ordinal)) return "quests";
        if (normalized.Contains("trigger", StringComparison.Ordinal)) return "triggers";
        if (normalized.Contains("script", StringComparison.Ordinal)) return "scripts";
        if (normalized.Contains("skill", StringComparison.Ordinal)) return "skills";
        if (normalized.Contains("formula", StringComparison.Ordinal)) return "formulas";
        if (normalized.Contains("command", StringComparison.Ordinal)) return "commands";
        return string.Empty;
    }
}

public sealed class CustomWorldSimpleSectionRow : ViewModelBase
{
    public CustomWorldSimpleSectionRow(string title, string description, IEnumerable<CustomWorldSimpleFieldRow> fields)
    {
        Title = title;
        Description = description;
        foreach (var field in fields) Fields.Add(field);
    }

    public string Title { get; }
    public string Description { get; }
    public ObservableCollection<CustomWorldSimpleFieldRow> Fields { get; } = [];
    public int FieldCount => Fields.Count;
}

public sealed class CustomWorldDedicatedTabRow : ViewModelBase
{
    public CustomWorldDedicatedTabRow(
        string title,
        string description,
        IEnumerable<CustomWorldSimpleFieldRow> fields,
        bool showsNpcAttacks = false,
        bool showsNpcAbilities = false,
        bool showsRoomMap = false,
        bool showsShopInventory = false)
    {
        Title = title;
        Description = description;
        ShowsNpcAttacks = showsNpcAttacks;
        ShowsNpcAbilities = showsNpcAbilities;
        ShowsRoomMap = showsRoomMap;
        ShowsShopInventory = showsShopInventory;
        foreach (var field in fields) Fields.Add(field);
    }

    public string Title { get; }
    public string Description { get; }
    public bool ShowsNpcAttacks { get; }
    public bool ShowsNpcAbilities { get; }
    public bool ShowsRoomMap { get; }
    public bool ShowsShopInventory { get; }
    public ObservableCollection<CustomWorldSimpleFieldRow> Fields { get; } = [];
    public int FieldCount => Fields.Count;
}

public sealed class NpcAttackEditorRow : ViewModelBase
{
    private string _slot = "1";
    private string _type = "1";
    private string _minimumDamage = "0";
    private string _maximumDamage = "0";
    private string _selectionPercent = "100";
    private string _accuracy = "0";
    private string _hitSpellLegacyId = "0";
    private string _energyCost = "1000";
    private string _hitMessageLegacyId = "0";
    private string _missMessageLegacyId = "0";
    private string _dodgeMessageLegacyId = "0";
    private string _extraFields = string.Empty;

    public string Slot { get => _slot; set { if (Set(ref _slot, value)) { Raise(nameof(DisplayName)); Raise(nameof(Summary)); } } }
    public string Type { get => _type; set { if (Set(ref _type, value)) Raise(nameof(Summary)); } }
    public string MinimumDamage { get => _minimumDamage; set { if (Set(ref _minimumDamage, value)) Raise(nameof(Summary)); } }
    public string MaximumDamage { get => _maximumDamage; set { if (Set(ref _maximumDamage, value)) Raise(nameof(Summary)); } }
    public string SelectionPercent { get => _selectionPercent; set { if (Set(ref _selectionPercent, value)) Raise(nameof(Summary)); } }
    public string Accuracy { get => _accuracy; set => Set(ref _accuracy, value); }
    public string HitSpellLegacyId { get => _hitSpellLegacyId; set => Set(ref _hitSpellLegacyId, value); }
    public string EnergyCost { get => _energyCost; set => Set(ref _energyCost, value); }
    public string HitMessageLegacyId { get => _hitMessageLegacyId; set => Set(ref _hitMessageLegacyId, value); }
    public string MissMessageLegacyId { get => _missMessageLegacyId; set => Set(ref _missMessageLegacyId, value); }
    public string DodgeMessageLegacyId { get => _dodgeMessageLegacyId; set => Set(ref _dodgeMessageLegacyId, value); }
    public string ExtraFields { get => _extraFields; set => Set(ref _extraFields, value); }
    public string DisplayName => $"Attack {Slot}";
    public string Summary => $"Type {Type}  •  {MinimumDamage}-{MaximumDamage}  •  {SelectionPercent}%";
}

public sealed class NpcAbilityEditorRow : ViewModelBase
{
    private string _ability = "New Ability";
    private string _value = "0";
    private string _abilityId = "0";
    private string _extraFields = string.Empty;

    public string Ability { get => _ability; set { if (Set(ref _ability, value)) Raise(nameof(DisplayName)); } }
    public string Value { get => _value; set => Set(ref _value, value); }
    public string AbilityId { get => _abilityId; set => Set(ref _abilityId, value); }
    public string ExtraFields { get => _extraFields; set => Set(ref _extraFields, value); }
    public string DisplayName => string.IsNullOrWhiteSpace(Ability) ? "Ability" : Ability;
}

public sealed class RoomMapTileRow : ViewModelBase
{
    private string _destinationRoomId;

    public RoomMapTileRow(string direction, string exitId, string destinationRoomId, string destinationName, bool isCenter = false)
    {
        Direction = direction;
        ExitId = exitId;
        _destinationRoomId = destinationRoomId;
        DestinationName = destinationName;
        IsCenter = isCenter;
    }

    public string Direction { get; }
    public string ExitId { get; }
    public string DestinationName { get; }
    public bool IsCenter { get; }
    public string DirectionLabel => IsCenter ? "CURRENT ROOM" : Direction switch
    {
        "N" => "NORTH", "NE" => "NORTHEAST", "E" => "EAST", "SE" => "SOUTHEAST",
        "S" => "SOUTH", "SW" => "SOUTHWEST", "W" => "WEST", "NW" => "NORTHWEST",
        "U" => "UP", "D" => "DOWN", _ => Direction
    };
    public string DestinationRoomId
    {
        get => _destinationRoomId;
        set
        {
            if (!Set(ref _destinationRoomId, value)) return;
            Raise(nameof(DisplayText));
        }
    }
    public string DisplayText => IsCenter
        ? (string.IsNullOrWhiteSpace(DestinationName) ? DestinationRoomId : DestinationName)
        : string.IsNullOrWhiteSpace(DestinationRoomId)
            ? "No exit"
            : string.IsNullOrWhiteSpace(DestinationName) ? DestinationRoomId : DestinationName;
}

public sealed class RoomVisualMapNodeRow : ViewModelBase
{
    private double _x;
    private double _y;
    private bool _isCurrent;
    private bool _isSelected;
    private bool _isTrail;

    public RoomVisualMapNodeRow(CustomWorldVisualMapRoom room)
    {
        RoomId = room.RoomId;
        Name = room.Name;
        MapNumber = room.MapNumber;
        RoomNumber = room.RoomNumber;
        Marker = room.Marker;
        _x = room.X;
        _y = room.Y;
    }

    public string RoomId { get; }
    public string Name { get; }
    public int MapNumber { get; }
    public int RoomNumber { get; }
    public string Marker { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? RoomId : Name;
    public string RoomNumberLabel => RoomNumber > 0 ? $"Room {RoomNumber}" : RoomId;
    public bool HasMarker => !string.IsNullOrWhiteSpace(Marker);
    public double X { get => _x; set => Set(ref _x, Math.Max(0, value)); }
    public double Y { get => _y; set => Set(ref _y, Math.Max(0, value)); }
    public bool IsCurrent { get => _isCurrent; set => Set(ref _isCurrent, value); }
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public bool IsTrail { get => _isTrail; set => Set(ref _isTrail, value); }
}

public sealed class RoomVisualMapEdgeRow : ViewModelBase
{
    public const double NodeWidth = 176;
    public const double NodeHeight = 82;
    private bool _isTrail;

    public RoomVisualMapEdgeRow(CustomWorldVisualMapEdge edge, RoomVisualMapNodeRow from, RoomVisualMapNodeRow to)
    {
        ExitId = edge.ExitId;
        Direction = edge.Direction;
        From = from;
        To = to;
        From.PropertyChanged += NodeMoved;
        To.PropertyChanged += NodeMoved;
    }

    public string ExitId { get; }
    public string Direction { get; }
    public RoomVisualMapNodeRow From { get; }
    public RoomVisualMapNodeRow To { get; }
    public string FromRoomId => From.RoomId;
    public string ToRoomId => To.RoomId;
    public double X1 => From.X + NodeWidth / 2;
    public double Y1 => From.Y + NodeHeight / 2;
    public double X2 => To.X + NodeWidth / 2;
    public double Y2 => To.Y + NodeHeight / 2;
    public double LabelX => (X1 + X2) / 2 + (Direction is "W" or "SW" or "NW" ? -30 : 8);
    public double LabelY => (Y1 + Y2) / 2 + (Direction is "N" or "NE" or "NW" ? -28 : 8);
    public bool IsTrail { get => _isTrail; set => Set(ref _isTrail, value); }

    private void NodeMoved(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(RoomVisualMapNodeRow.X) and not nameof(RoomVisualMapNodeRow.Y)) return;
        Raise(nameof(X1)); Raise(nameof(Y1)); Raise(nameof(X2)); Raise(nameof(Y2)); Raise(nameof(LabelX)); Raise(nameof(LabelY));
    }
}

public sealed class ShopInventoryEditorRow : ViewModelBase
{
    private string _itemRef;
    private string _maxQty;
    private string _normalQty;
    private string _regenQty;
    private string _regenTime;
    private string _regenPercent;

    public ShopInventoryEditorRow(string itemRef = "", string maxQty = "5", string normalQty = "5", string regenQty = "1", string regenTime = "48", string regenPercent = "25")
    {
        _itemRef = itemRef;
        _maxQty = maxQty;
        _normalQty = normalQty;
        _regenQty = regenQty;
        _regenTime = regenTime;
        _regenPercent = regenPercent;
    }

    public string ItemRef { get => _itemRef; set { if (Set(ref _itemRef, value)) Raise(nameof(DisplayName)); } }
    public string MaxQty { get => _maxQty; set => Set(ref _maxQty, value); }
    public string NormalQty { get => _normalQty; set => Set(ref _normalQty, value); }
    public string RegenQty { get => _regenQty; set => Set(ref _regenQty, value); }
    public string RegenTime { get => _regenTime; set => Set(ref _regenTime, value); }
    public string RegenPercent { get => _regenPercent; set => Set(ref _regenPercent, value); }
    public string DisplayName => string.IsNullOrWhiteSpace(ItemRef) ? "New stock item" : ItemRef;
}

public sealed class EditorViewModel : ViewModelBase
{
    private readonly ClassicAdministrationEditorService _classicService;
    private readonly CustomWorldEditorService _customService;
    private readonly NightmareMdbBridgeService _nightmareMdbBridge;
    private bool _unlimitedItemsEnabled = true;
    private bool _everyoneCanUseSysGoto;
    private string _classicStatus = "Loading Classic settings...";
    private string _projectRoot = string.Empty;
    private string _policyPath = string.Empty;

    private string _customWorldName = "Custom World 1";
    private string _customDescription = string.Empty;
    private string _customLoginBanner = "Custom World 1";
    private string _customMotd = string.Empty;
    private bool _customRuntimeEnabled = true;
    private bool _customTransfersAllowed;
    private string _customStatus = "Loading Custom World 1...";
    private string _customConfigPath = string.Empty;
    private string _customOverridePath = string.Empty;
    private string _customGeneratedDataPath = string.Empty;
    private string _customGeneratedState = "Not built";
    private string _overrideFamily = "rooms";
    private string _overrideTargetId = string.Empty;
    private string _overrideOperation = "patch";
    private string _overrideFieldsJson = "{\n  \"name\": \"\"\n}";
    private CustomWorldOverrideRow? _selectedOverride;
    private string _objectSearch = string.Empty;
    private string _baseRecordJson = "Choose a family and click Browse Objects.";
    private CustomWorldObjectRow? _selectedBaseObject;

    private string _simpleFamily = "rooms";
    private string _simpleObjectId = string.Empty;
    private string _simpleFormTitle = "New Room";
    private string _simpleFormHelp = string.Empty;
    private bool _simpleIsNew = true;
    private CustomWorldObjectRow? _selectedSimpleObject;
    private CustomWorldSimpleSectionRow? _selectedSimpleSection;
    private CustomWorldDedicatedTabRow? _selectedDedicatedTab;
    private NpcAttackEditorRow? _selectedNpcAttack;
    private NpcAbilityEditorRow? _selectedNpcAbility;
    private bool _npcAttacksDirty;
    private bool _npcAbilitiesDirty;
    private bool _roomMapLinksDirty;
    private bool _shopInventoryDirty;
    private ShopInventoryEditorRow? _selectedShopInventory;
    private RoomVisualMapNodeRow? _selectedVisualMapNode;
    private int _selectedVisualMapNumber = 1;
    private bool _loadingVisualMap;
    private bool _visualMapLayoutDirty;
    private double _visualMapZoom = 0.85;
    private string _visualMapStatus = "Open a saved room to load its map.";
    private string _visualMapTrailText = "Select a room to preview a route. Double-click a room to edit it.";
    private bool _referenceLookupOpen;
    private string _referenceLookupFamily = string.Empty;
    private string _referenceLookupSearch = string.Empty;
    private string _referenceLookupTitle = "Select linked content";
    private CustomWorldSimpleFieldRow? _referenceLookupTarget;
    private CustomWorldObjectRow? _selectedReferenceLookupResult;
    private int _editorWorkspaceTabIndex;
    private string _toolReferenceFamily = "items";
    private string _toolReferenceSearch = string.Empty;
    private CustomWorldObjectRow? _selectedToolReferenceObject;
    private CustomWorldReferenceHit? _selectedCrossReferenceHit;
    private string _crossReferenceStatus = "Choose a record, then click Find Where Used.";

    private string _npcToolSearch = string.Empty;
    private CustomWorldObjectRow? _selectedNpcToolObject;
    private string _npcTargetDefense = "0";
    private string _npcTargetArmorClass = "50";
    private string _npcTargetDamageResistance = "0";
    private string _npcTargetDodge = "0";
    private string _npcSimulationRounds = "1000";
    private string _npcSimulationSummary = "Choose a monster and run the simulator.";

    private string _weaponToolSearch = string.Empty;
    private CustomWorldObjectRow? _selectedWeaponToolObject;
    private string _weaponBaseAttackValue = "100";
    private string _weaponTargetDefense = "0";
    private string _weaponTargetArmorClass = "50";
    private string _weaponTargetDamageResistance = "0";
    private string _weaponTargetDodge = "0";
    private string _weaponCriticalChance = "0";
    private string _weaponSimulationSwings = "1000";
    private string _weaponSimulationSummary = "Choose a weapon and run the calculator.";

    private CustomWorldValidationIssue? _selectedValidationIssue;
    private string _validationStatus = "Run validation before testing Custom1.";

    private string _globalSearchText = string.Empty;
    private CustomWorldGlobalSearchHit? _selectedGlobalSearchHit;
    private string _globalSearchStatus = "Search every creator database by name, ID, text, flags, effects, or references.";
    private string _importExportStatus = "Export Custom1 changes or preview a package before importing it.";
    private string _pendingImportPath = string.Empty;
    private CustomWorldImportPreview? _pendingImportPreview;

    private string _nightmareMdbPath = string.Empty;
    private string _nightmareMdbStatus = "Choose a Nightmare Redux .mdb, analyze it, then import only after reviewing the table mapping.";
    private NightmareMdbAnalysis? _pendingNightmareMdbAnalysis;
    private NightmareMdbTableSummary? _selectedNightmareMdbTable;
    private string _wccEvidenceFolder = string.Empty;
    private string _wccEvidenceStatus = "Optional: choose any WCC/WGS DAT file from the original custom Worldgroup install. Files are hashed and retained read-only as evidence.";
    private NightmareWccEvidenceFile? _selectedWccEvidenceFile;
    private NightmareWccGapAnalysis? _pendingWccGapAnalysis;
    private NightmareWccGapRow? _selectedWccGapRow;
    private string _wccGapStatus = "Choose the matching MDB and original WCC/WGS folder, then run gap analysis. Source files remain read-only.";

    private string _bulkFamily = "items";
    private string _bulkTargetIds = string.Empty;
    private string _bulkFieldPath = string.Empty;
    private string _bulkValue = string.Empty;
    private string _bulkValueKind = "Text";
    private string _bulkSourceId = string.Empty;
    private string _bulkStatus = "Enter one object ID per line. Bulk operations write Custom1 overrides only.";

    private string _keyDoorSearch = string.Empty;
    private CustomWorldKeyDoorRow? _selectedKeyDoor;
    private string _keyDoorStatus = "Search all keyed doors/exits and jump directly to the exit or key item.";

    private string _coinRoomSearch = string.Empty;
    private CustomWorldObjectRow? _selectedCoinRoom;
    private string _coinRunic = "0";
    private string _coinPlatinum = "0";
    private string _coinGold = "0";
    private string _coinSilver = "0";
    private string _coinCopper = "0";
    private string _coinStatus = "This edits current Custom1 room currency state for testing.";

    private string _monsterIndexSearch = string.Empty;
    private string _monsterIndexMinimumLevel = "0";
    private string _monsterIndexMaximumLevel = string.Empty;
    private CustomWorldMonsterIndexRow? _selectedMonsterIndexRow;
    private string _monsterIndexStatus = "Build a grouped NPC/room index by level, group, and spawn location.";

    private string _controlRoomSearch = string.Empty;
    private CustomWorldControlRoomRow? _selectedControlRoom;
    private string _controlRoomStatus = "List rooms referenced by interactions, triggers, and scripts.";

    private string _shopResetSearch = string.Empty;
    private CustomWorldObjectRow? _selectedShopResetObject;
    private string _shopResetStatus = "Select a generated Custom1 shop to reset its live stock to authored normal quantities.";

    private string _roomCleanupSearch = string.Empty;
    private CustomWorldObjectRow? _selectedRoomCleanupObject;
    private string _roomCleanupStatus = "Select a room to combine duplicate dynamic ground-item stacks. Stop the host before running this cleanup.";

    private string _textblockStripFind = string.Empty;
    private string _textblockStripReplacement = string.Empty;
    private bool _textblockStripSuffixOnly = true;
    private CustomWorldTextblockStripPreviewRow? _selectedTextblockStripPreview;
    private string _textblockStripStatus = "Enter exact text, preview every change, then apply the Custom1-only batch operation.";

    public EditorViewModel(
        ClassicAdministrationEditorService classicService,
        CustomWorldEditorService customService)
    {
        _classicService = classicService;
        _customService = customService;
        _nightmareMdbBridge = new NightmareMdbBridgeService(customService);
        SimpleFamilies = _customService.GetEditableFamilies();
        SaveClassicCommand = new RelayCommand(SaveClassic);
        ReloadCommand = new RelayCommand(Load);
        SaveCustomSettingsCommand = new RelayCommand(SaveCustomSettings);
        BuildCustomWorldCommand = new AsyncRelayCommand(BuildCustomWorldAsync);
        SaveOverrideCommand = new RelayCommand(SaveOverride);
        RemoveOverrideCommand = new RelayCommand(RemoveSelectedOverride, () => SelectedOverride is not null);
        NewOverrideCommand = new RelayCommand(NewOverride);
        BrowseObjectsCommand = new RelayCommand(BrowseObjects);
        BrowseSimpleObjectsCommand = new RelayCommand(BrowseSimpleObjects);
        NewSimpleObjectCommand = new RelayCommand(StartNewSimpleObject);
        CloneSimpleObjectCommand = new RelayCommand(CloneSimpleObject, () => !SimpleIsNew && !string.IsNullOrWhiteSpace(SimpleObjectId));
        SaveSimpleObjectCommand = new AsyncRelayCommand(SaveSimpleObjectAsync);
        DeleteSimpleObjectCommand = new RelayCommand(DeleteSimpleObject, () => !string.IsNullOrWhiteSpace(SimpleObjectId));
        DiscardSimpleObjectCommand = new RelayCommand(DiscardSimpleObject);
        AddNpcAttackCommand = new RelayCommand(AddNpcAttack);
        RemoveNpcAttackCommand = new RelayCommand(RemoveNpcAttack, () => SelectedNpcAttack is not null);
        AddNpcAbilityCommand = new RelayCommand(AddNpcAbility);
        RemoveNpcAbilityCommand = new RelayCommand(RemoveNpcAbility, () => SelectedNpcAbility is not null);
        AddShopInventoryCommand = new RelayCommand(AddShopInventory);
        RemoveShopInventoryCommand = new RelayCommand(RemoveShopInventory, () => SelectedShopInventory is not null);
        RefreshVisualMapCommand = new RelayCommand(() => LoadVisualMapEditor());
        SaveVisualMapLayoutCommand = new RelayCommand(SaveVisualMapLayout, () => IsRoomEditor && RoomVisualMapNodes.Count > 0);
        AutoArrangeVisualMapCommand = new RelayCommand(AutoArrangeVisualMap, () => IsRoomEditor && RoomVisualMapNodes.Count > 0);
        NewRoomFromMapCommand = new RelayCommand(NewRoomFromMap, () => IsRoomEditor);
        OpenExitEditorCommand = new RelayCommand(() => SwitchEditorFamily("exits"));
        OpenSpawnEditorCommand = new RelayCommand(() => SwitchEditorFamily("spawns"));
        OpenShopEditorCommand = new RelayCommand(() => SwitchEditorFamily("shops"));
        OpenTriggerEditorCommand = new RelayCommand(() => SwitchEditorFamily("triggers"));
        OpenReferenceLookupCommand = new ParameterCommand(parameter =>
        {
            if (parameter is CustomWorldSimpleFieldRow field) OpenReferenceLookup(field);
            return Task.CompletedTask;
        });
        RefreshReferenceLookupCommand = new RelayCommand(RefreshReferenceLookup);
        ApplyReferenceLookupCommand = new RelayCommand(ApplyReferenceLookup, () => _referenceLookupTarget is not null && SelectedReferenceLookupResult is not null);
        CloseReferenceLookupCommand = new RelayCommand(CloseReferenceLookup);
        BrowseToolReferenceObjectsCommand = new RelayCommand(BrowseToolReferenceObjects);
        FindWhereUsedCommand = new RelayCommand(FindWhereUsed, () => SelectedToolReferenceObject is not null);
        OpenCrossReferenceHitCommand = new RelayCommand(OpenSelectedCrossReferenceHit, () => SelectedCrossReferenceHit is not null);
        BrowseNpcToolObjectsCommand = new RelayCommand(BrowseNpcToolObjects);
        RunNpcSimulationCommand = new RelayCommand(RunNpcSimulation, () => SelectedNpcToolObject is not null);
        OpenNpcToolObjectCommand = new RelayCommand(() => OpenToolObject("npcs", SelectedNpcToolObject?.Id), () => SelectedNpcToolObject is not null);
        BrowseWeaponToolObjectsCommand = new RelayCommand(BrowseWeaponToolObjects);
        RunWeaponSimulationCommand = new RelayCommand(RunWeaponSimulation, () => SelectedWeaponToolObject is not null);
        OpenWeaponToolObjectCommand = new RelayCommand(() => OpenToolObject("items", SelectedWeaponToolObject?.Id), () => SelectedWeaponToolObject is not null);
        RunCustomValidationCommand = new RelayCommand(RunCustomValidation);
        OpenValidationIssueCommand = new RelayCommand(OpenSelectedValidationIssue, () => SelectedValidationIssue is not null);
        RunGlobalSearchCommand = new RelayCommand(RunGlobalSearch);
        OpenGlobalSearchHitCommand = new RelayCommand(OpenSelectedGlobalSearchHit, () => SelectedGlobalSearchHit is not null);
        ExportAllCustomChangesCommand = new RelayCommand(() => ExportCustomChanges("all"));
        ExportCurrentFamilyCommand = new RelayCommand(() => ExportCustomChanges("family"));
        ExportCurrentRecordCommand = new RelayCommand(() => ExportCustomChanges("record"), () => !string.IsNullOrWhiteSpace(SimpleObjectId));
        PreviewImportPackageCommand = new RelayCommand(PreviewImportPackage);
        ApplyImportPackageCommand = new RelayCommand(ApplyImportPackage, () => PendingImportPreview is not null && PendingImportPreview.InvalidRecords == 0);
        AnalyzeNightmareMdbCommand = new RelayCommand(AnalyzeNightmareMdb);
        ImportNightmareMdbCommand = new RelayCommand(ImportNightmareMdb, () => PendingNightmareMdbAnalysis is not null && PendingNightmareMdbAnalysis.MappableRows > 0);
        ExportNightmareMdbChangedCommand = new RelayCommand(() => ExportNightmareMdb(changedOnly: true));
        ExportNightmareMdbFullCommand = new RelayCommand(() => ExportNightmareMdb(changedOnly: false));
        ChooseWccEvidenceCommand = new RelayCommand(ChooseWccEvidence);
        AnalyzeWccGapsCommand = new RelayCommand(AnalyzeWccGaps, () => PendingNightmareMdbAnalysis is not null && !string.IsNullOrWhiteSpace(WccEvidenceFolder));
        RunBulkSetCommand = new RelayCommand(RunBulkSet);
        RunBulkCopyCommand = new RelayCommand(RunBulkCopy);
        BrowseKeyDoorsCommand = new RelayCommand(BrowseKeyDoors);
        OpenSelectedKeyDoorCommand = new RelayCommand(() => OpenToolObject("exits", SelectedKeyDoor?.ExitId), () => SelectedKeyDoor is not null);
        OpenSelectedKeyItemCommand = new RelayCommand(() => OpenToolObject("items", SelectedKeyDoor?.KeyItemId), () => SelectedKeyDoor is not null && !string.IsNullOrWhiteSpace(SelectedKeyDoor.KeyItemId));
        BrowseCoinRoomsCommand = new RelayCommand(BrowseCoinRooms);
        SetCoinRoomCurrencyCommand = new RelayCommand(SetCoinRoomCurrency, () => SelectedCoinRoom is not null);
        BrowseMonsterIndexCommand = new RelayCommand(BrowseMonsterIndex);
        OpenSelectedMonsterIndexCommand = new RelayCommand(() => OpenToolObject("npcs", SelectedMonsterIndexRow?.NpcId), () => SelectedMonsterIndexRow is not null);
        BrowseControlRoomsCommand = new RelayCommand(BrowseControlRooms);
        OpenSelectedControlRoomCommand = new RelayCommand(() => OpenToolObject("rooms", SelectedControlRoom?.RoomId), () => SelectedControlRoom is not null);
        BrowseShopResetObjectsCommand = new RelayCommand(BrowseShopResetObjects);
        ResetSelectedShopStockCommand = new RelayCommand(ResetSelectedShopStock, () => SelectedShopResetObject is not null);
        BrowseRoomCleanupObjectsCommand = new RelayCommand(BrowseRoomCleanupObjects);
        CombineSelectedRoomItemsCommand = new RelayCommand(CombineSelectedRoomItems, () => SelectedRoomCleanupObject is not null);
        PreviewTextblockStripCommand = new RelayCommand(PreviewTextblockStrip);
        ApplyTextblockStripCommand = new RelayCommand(ApplyTextblockStrip, () => TextblockStripPreviewRows.Count > 0);
    }

    public ObservableCollection<ClassicCharacterAccessRow> Characters { get; } = [];
    public ObservableCollection<CustomWorldOverrideRow> CustomOverrides { get; } = [];
    public ObservableCollection<CustomWorldObjectRow> BaseObjects { get; } = [];
    public ObservableCollection<CustomWorldObjectRow> SimpleObjects { get; } = [];
    public ObservableCollection<CustomWorldSimpleFieldRow> SimpleFields { get; } = [];
    public ObservableCollection<CustomWorldSimpleFieldRow> SimpleRequiredFields { get; } = [];
    public ObservableCollection<CustomWorldSimpleFieldRow> SimpleOptionalFields { get; } = [];
    public ObservableCollection<CustomWorldSimpleSectionRow> SimpleSections { get; } = [];
    public ObservableCollection<CustomWorldDedicatedTabRow> DedicatedTabs { get; } = [];
    public ObservableCollection<NpcAttackEditorRow> NpcAttacks { get; } = [];
    public ObservableCollection<NpcAbilityEditorRow> NpcAbilities { get; } = [];
    public ObservableCollection<RoomMapTileRow> RoomMapGridTiles { get; } = [];
    public ObservableCollection<RoomMapTileRow> RoomMapVerticalLinks { get; } = [];
    public ObservableCollection<RoomVisualMapNodeRow> RoomVisualMapNodes { get; } = [];
    public ObservableCollection<RoomVisualMapEdgeRow> RoomVisualMapEdges { get; } = [];
    public ObservableCollection<int> VisualMapNumbers { get; } = [];
    public ObservableCollection<ShopInventoryEditorRow> ShopInventory { get; } = [];
    public ObservableCollection<CustomWorldObjectRow> ReferenceLookupResults { get; } = [];
    public ObservableCollection<CustomWorldObjectRow> ToolReferenceObjects { get; } = [];
    public ObservableCollection<CustomWorldReferenceHit> CrossReferenceHits { get; } = [];
    public ObservableCollection<CustomWorldObjectRow> NpcToolObjects { get; } = [];
    public ObservableCollection<CustomWorldNpcAttackSimulationRow> NpcSimulationRows { get; } = [];
    public ObservableCollection<CustomWorldObjectRow> WeaponToolObjects { get; } = [];
    public ObservableCollection<CustomWorldValidationIssue> ValidationIssues { get; } = [];
    public ObservableCollection<CustomWorldGlobalSearchHit> GlobalSearchHits { get; } = [];
    public ObservableCollection<NightmareMdbTableSummary> NightmareMdbTables { get; } = [];
    public ObservableCollection<NightmareWccEvidenceFile> WccEvidenceFiles { get; } = [];
    public ObservableCollection<NightmareWccGapRow> WccGapRows { get; } = [];
    public ObservableCollection<CustomWorldKeyDoorRow> KeyDoorRows { get; } = [];
    public ObservableCollection<CustomWorldObjectRow> CoinRoomObjects { get; } = [];
    public ObservableCollection<CustomWorldNightmareParityRow> NightmareParityRows { get; } = [];
    public ObservableCollection<CustomWorldMonsterIndexRow> MonsterIndexRows { get; } = [];
    public ObservableCollection<CustomWorldControlRoomRow> ControlRoomRows { get; } = [];
    public ObservableCollection<CustomWorldObjectRow> ShopResetObjects { get; } = [];
    public ObservableCollection<CustomWorldObjectRow> RoomCleanupObjects { get; } = [];
    public ObservableCollection<CustomWorldTextblockStripPreviewRow> TextblockStripPreviewRows { get; } = [];
    public IReadOnlyList<string> BulkValueKinds { get; } = ["Text", "Integer", "Decimal", "Boolean", "Null"];
    public IReadOnlyList<CustomWorldSimpleFamilyOption> SimpleFamilies { get; }
    public IReadOnlyList<string> OverrideFamilies { get; } =
    [
        "rooms", "exits", "npcs", "spawns", "items", "spells", "abilities", "native_message_payloads",
        "native_textblock_payloads", "room_interactions", "classes", "races", "shops", "quests", "triggers",
        "scripts", "skills", "formulas", "commands"
    ];
    public IReadOnlyList<string> OverrideOperations { get; } = ["patch", "upsert", "delete"];

    public bool UnlimitedItemsEnabled { get => _unlimitedItemsEnabled; set => Set(ref _unlimitedItemsEnabled, value); }
    public bool EveryoneCanUseSysGoto { get => _everyoneCanUseSysGoto; set => Set(ref _everyoneCanUseSysGoto, value); }
    public string ClassicStatus { get => _classicStatus; private set => Set(ref _classicStatus, value); }
    public string ProjectRoot { get => _projectRoot; private set => Set(ref _projectRoot, value); }
    public string PolicyPath { get => _policyPath; private set => Set(ref _policyPath, value); }

    public string CustomWorldName { get => _customWorldName; set => Set(ref _customWorldName, value); }
    public string CustomDescription { get => _customDescription; set => Set(ref _customDescription, value); }
    public string CustomLoginBanner { get => _customLoginBanner; set => Set(ref _customLoginBanner, value); }
    public string CustomMotd { get => _customMotd; set => Set(ref _customMotd, value); }
    public bool CustomRuntimeEnabled { get => _customRuntimeEnabled; set => Set(ref _customRuntimeEnabled, value); }
    public bool CustomTransfersAllowed { get => _customTransfersAllowed; set => Set(ref _customTransfersAllowed, value); }
    public string CustomStatus { get => _customStatus; private set => Set(ref _customStatus, value); }
    public string CustomConfigPath { get => _customConfigPath; private set => Set(ref _customConfigPath, value); }
    public string CustomOverridePath { get => _customOverridePath; private set => Set(ref _customOverridePath, value); }
    public string CustomGeneratedDataPath { get => _customGeneratedDataPath; private set => Set(ref _customGeneratedDataPath, value); }
    public string CustomGeneratedState { get => _customGeneratedState; private set => Set(ref _customGeneratedState, value); }

    public string SimpleFamily
    {
        get => _simpleFamily;
        set
        {
            if (!Set(ref _simpleFamily, value)) return;
            RaiseEditorModeProperties();
            CloseReferenceLookup();
            StartNewSimpleObject();
            BrowseSimpleObjects();
        }
    }
    public string SimpleObjectId
    {
        get => _simpleObjectId;
        set
        {
            if (!Set(ref _simpleObjectId, value)) return;
            (DeleteSimpleObjectCommand as RelayCommand)?.Raise();
        }
    }
    public string SimpleFormTitle { get => _simpleFormTitle; private set => Set(ref _simpleFormTitle, value); }
    public string SimpleFormHelp { get => _simpleFormHelp; private set => Set(ref _simpleFormHelp, value); }
    public bool SimpleIsNew
    {
        get => _simpleIsNew;
        private set
        {
            if (!Set(ref _simpleIsNew, value)) return;
            (CloneSimpleObjectCommand as RelayCommand)?.Raise();
        }
    }
    public CustomWorldSimpleSectionRow? SelectedSimpleSection
    {
        get => _selectedSimpleSection;
        set => Set(ref _selectedSimpleSection, value);
    }
    public int SimpleFieldCount => SimpleFields.Count;
    public string SimpleControlSummary => IsDedicatedEditor
        ? $"{SimpleFieldCount} gameplay/content fields organized into {DedicatedTabs.Count} dedicated tab(s)."
        : $"{SimpleFieldCount} editable field(s) across {SimpleSections.Count} section(s).";
    public bool IsRoomEditor => SimpleFamily.Equals("rooms", StringComparison.OrdinalIgnoreCase);
    public bool IsItemEditor => SimpleFamily.Equals("items", StringComparison.OrdinalIgnoreCase);
    public bool IsNpcEditor => SimpleFamily.Equals("npcs", StringComparison.OrdinalIgnoreCase);
    public bool IsSpellEditor => SimpleFamily.Equals("spells", StringComparison.OrdinalIgnoreCase);
    public bool IsAbilityEditor => SimpleFamily.Equals("abilities", StringComparison.OrdinalIgnoreCase);
    public bool IsMessageEditor => SimpleFamily.Equals("native_message_payloads", StringComparison.OrdinalIgnoreCase);
    public bool IsTextblockEditor => SimpleFamily.Equals("native_textblock_payloads", StringComparison.OrdinalIgnoreCase);
    public bool IsActionEditor => SimpleFamily.Equals("room_interactions", StringComparison.OrdinalIgnoreCase);
    public bool IsClassEditor => SimpleFamily.Equals("classes", StringComparison.OrdinalIgnoreCase);
    public bool IsRaceEditor => SimpleFamily.Equals("races", StringComparison.OrdinalIgnoreCase);
    public bool IsShopEditor => SimpleFamily.Equals("shops", StringComparison.OrdinalIgnoreCase);
    public bool IsDedicatedEditor => IsRoomEditor || IsItemEditor || IsNpcEditor || IsSpellEditor || IsAbilityEditor ||
        IsMessageEditor || IsTextblockEditor || IsActionEditor || IsClassEditor || IsRaceEditor || IsShopEditor;
    public bool IsGenericEditor => !IsDedicatedEditor;
    public string DedicatedEditorName => IsRoomEditor ? "ROOM / MAP EDITOR" :
        IsItemEditor ? "ITEM EDITOR" : IsNpcEditor ? "MONSTER / NPC EDITOR" :
        IsSpellEditor ? "SPELL EDITOR" : IsAbilityEditor ? "ABILITY EDITOR" :
        IsMessageEditor ? "MESSAGE EDITOR" : IsTextblockEditor ? "TEXTBLOCK EDITOR" :
        IsActionEditor ? "ACTION / ROOM INTERACTION EDITOR" : IsClassEditor ? "CLASS EDITOR" :
        IsRaceEditor ? "RACE EDITOR" : IsShopEditor ? "SHOP / SERVICE EDITOR" : "EDITOR";
    public bool ReferenceLookupOpen { get => _referenceLookupOpen; private set => Set(ref _referenceLookupOpen, value); }
    public string ReferenceLookupFamily { get => _referenceLookupFamily; private set => Set(ref _referenceLookupFamily, value); }
    public string ReferenceLookupTitle { get => _referenceLookupTitle; private set => Set(ref _referenceLookupTitle, value); }
    public string ReferenceLookupSearch { get => _referenceLookupSearch; set => Set(ref _referenceLookupSearch, value); }
    public CustomWorldObjectRow? SelectedReferenceLookupResult
    {
        get => _selectedReferenceLookupResult;
        set
        {
            if (!Set(ref _selectedReferenceLookupResult, value)) return;
            (ApplyReferenceLookupCommand as RelayCommand)?.Raise();
        }
    }

    public int EditorWorkspaceTabIndex { get => _editorWorkspaceTabIndex; set => Set(ref _editorWorkspaceTabIndex, value); }
    public string ToolReferenceFamily
    {
        get => _toolReferenceFamily;
        set
        {
            if (!Set(ref _toolReferenceFamily, value)) return;
            BrowseToolReferenceObjects();
        }
    }
    public string ToolReferenceSearch { get => _toolReferenceSearch; set => Set(ref _toolReferenceSearch, value); }
    public CustomWorldObjectRow? SelectedToolReferenceObject
    {
        get => _selectedToolReferenceObject;
        set
        {
            if (!Set(ref _selectedToolReferenceObject, value)) return;
            (FindWhereUsedCommand as RelayCommand)?.Raise();
        }
    }
    public CustomWorldReferenceHit? SelectedCrossReferenceHit
    {
        get => _selectedCrossReferenceHit;
        set
        {
            if (!Set(ref _selectedCrossReferenceHit, value)) return;
            (OpenCrossReferenceHitCommand as RelayCommand)?.Raise();
        }
    }
    public string CrossReferenceStatus { get => _crossReferenceStatus; private set => Set(ref _crossReferenceStatus, value); }

    public string NpcToolSearch { get => _npcToolSearch; set => Set(ref _npcToolSearch, value); }
    public CustomWorldObjectRow? SelectedNpcToolObject
    {
        get => _selectedNpcToolObject;
        set
        {
            if (!Set(ref _selectedNpcToolObject, value)) return;
            (RunNpcSimulationCommand as RelayCommand)?.Raise();
            (OpenNpcToolObjectCommand as RelayCommand)?.Raise();
        }
    }
    public string NpcTargetDefense { get => _npcTargetDefense; set => Set(ref _npcTargetDefense, value); }
    public string NpcTargetArmorClass { get => _npcTargetArmorClass; set => Set(ref _npcTargetArmorClass, value); }
    public string NpcTargetDamageResistance { get => _npcTargetDamageResistance; set => Set(ref _npcTargetDamageResistance, value); }
    public string NpcTargetDodge { get => _npcTargetDodge; set => Set(ref _npcTargetDodge, value); }
    public string NpcSimulationRounds { get => _npcSimulationRounds; set => Set(ref _npcSimulationRounds, value); }
    public string NpcSimulationSummary { get => _npcSimulationSummary; private set => Set(ref _npcSimulationSummary, value); }

    public string WeaponToolSearch { get => _weaponToolSearch; set => Set(ref _weaponToolSearch, value); }
    public CustomWorldObjectRow? SelectedWeaponToolObject
    {
        get => _selectedWeaponToolObject;
        set
        {
            if (!Set(ref _selectedWeaponToolObject, value)) return;
            (RunWeaponSimulationCommand as RelayCommand)?.Raise();
            (OpenWeaponToolObjectCommand as RelayCommand)?.Raise();
        }
    }
    public string WeaponBaseAttackValue { get => _weaponBaseAttackValue; set => Set(ref _weaponBaseAttackValue, value); }
    public string WeaponTargetDefense { get => _weaponTargetDefense; set => Set(ref _weaponTargetDefense, value); }
    public string WeaponTargetArmorClass { get => _weaponTargetArmorClass; set => Set(ref _weaponTargetArmorClass, value); }
    public string WeaponTargetDamageResistance { get => _weaponTargetDamageResistance; set => Set(ref _weaponTargetDamageResistance, value); }
    public string WeaponTargetDodge { get => _weaponTargetDodge; set => Set(ref _weaponTargetDodge, value); }
    public string WeaponCriticalChance { get => _weaponCriticalChance; set => Set(ref _weaponCriticalChance, value); }
    public string WeaponSimulationSwings { get => _weaponSimulationSwings; set => Set(ref _weaponSimulationSwings, value); }
    public string WeaponSimulationSummary { get => _weaponSimulationSummary; private set => Set(ref _weaponSimulationSummary, value); }

    public CustomWorldValidationIssue? SelectedValidationIssue
    {
        get => _selectedValidationIssue;
        set
        {
            if (!Set(ref _selectedValidationIssue, value)) return;
            (OpenValidationIssueCommand as RelayCommand)?.Raise();
        }
    }
    public string ValidationStatus { get => _validationStatus; private set => Set(ref _validationStatus, value); }

    public string GlobalSearchText { get => _globalSearchText; set => Set(ref _globalSearchText, value); }
    public CustomWorldGlobalSearchHit? SelectedGlobalSearchHit
    {
        get => _selectedGlobalSearchHit;
        set { if (Set(ref _selectedGlobalSearchHit, value)) (OpenGlobalSearchHitCommand as RelayCommand)?.Raise(); }
    }
    public string GlobalSearchStatus { get => _globalSearchStatus; private set => Set(ref _globalSearchStatus, value); }
    public string ImportExportStatus { get => _importExportStatus; private set => Set(ref _importExportStatus, value); }
    public string PendingImportPath { get => _pendingImportPath; private set => Set(ref _pendingImportPath, value); }
    public CustomWorldImportPreview? PendingImportPreview
    {
        get => _pendingImportPreview;
        private set { if (Set(ref _pendingImportPreview, value)) (ApplyImportPackageCommand as RelayCommand)?.Raise(); }
    }

    public string NightmareMdbPath { get => _nightmareMdbPath; private set => Set(ref _nightmareMdbPath, value); }
    public string NightmareMdbStatus { get => _nightmareMdbStatus; private set => Set(ref _nightmareMdbStatus, value); }
    public NightmareMdbAnalysis? PendingNightmareMdbAnalysis
    {
        get => _pendingNightmareMdbAnalysis;
        private set
        {
            if (!Set(ref _pendingNightmareMdbAnalysis, value)) return;
            (ImportNightmareMdbCommand as RelayCommand)?.Raise();
            (AnalyzeWccGapsCommand as RelayCommand)?.Raise();
        }
    }
    public NightmareMdbTableSummary? SelectedNightmareMdbTable { get => _selectedNightmareMdbTable; set => Set(ref _selectedNightmareMdbTable, value); }
    public string WccEvidenceFolder
    {
        get => _wccEvidenceFolder;
        private set
        {
            if (!Set(ref _wccEvidenceFolder, value)) return;
            (AnalyzeWccGapsCommand as RelayCommand)?.Raise();
        }
    }
    public string WccEvidenceStatus { get => _wccEvidenceStatus; private set => Set(ref _wccEvidenceStatus, value); }
    public NightmareWccEvidenceFile? SelectedWccEvidenceFile { get => _selectedWccEvidenceFile; set => Set(ref _selectedWccEvidenceFile, value); }
    public NightmareWccGapAnalysis? PendingWccGapAnalysis { get => _pendingWccGapAnalysis; private set => Set(ref _pendingWccGapAnalysis, value); }
    public NightmareWccGapRow? SelectedWccGapRow { get => _selectedWccGapRow; set => Set(ref _selectedWccGapRow, value); }
    public string WccGapStatus { get => _wccGapStatus; private set => Set(ref _wccGapStatus, value); }

    public string BulkFamily { get => _bulkFamily; set => Set(ref _bulkFamily, value); }
    public string BulkTargetIds { get => _bulkTargetIds; set => Set(ref _bulkTargetIds, value); }
    public string BulkFieldPath { get => _bulkFieldPath; set => Set(ref _bulkFieldPath, value); }
    public string BulkValue { get => _bulkValue; set => Set(ref _bulkValue, value); }
    public string BulkValueKind { get => _bulkValueKind; set => Set(ref _bulkValueKind, value); }
    public string BulkSourceId { get => _bulkSourceId; set => Set(ref _bulkSourceId, value); }
    public string BulkStatus { get => _bulkStatus; private set => Set(ref _bulkStatus, value); }

    public string KeyDoorSearch { get => _keyDoorSearch; set => Set(ref _keyDoorSearch, value); }
    public CustomWorldKeyDoorRow? SelectedKeyDoor
    {
        get => _selectedKeyDoor;
        set
        {
            if (!Set(ref _selectedKeyDoor, value)) return;
            (OpenSelectedKeyDoorCommand as RelayCommand)?.Raise();
            (OpenSelectedKeyItemCommand as RelayCommand)?.Raise();
        }
    }
    public string KeyDoorStatus { get => _keyDoorStatus; private set => Set(ref _keyDoorStatus, value); }

    public string CoinRoomSearch { get => _coinRoomSearch; set => Set(ref _coinRoomSearch, value); }
    public CustomWorldObjectRow? SelectedCoinRoom
    {
        get => _selectedCoinRoom;
        set
        {
            if (!Set(ref _selectedCoinRoom, value)) return;
            LoadCoinRoomCurrency();
            (SetCoinRoomCurrencyCommand as RelayCommand)?.Raise();
        }
    }
    public string CoinRunic { get => _coinRunic; set => Set(ref _coinRunic, value); }
    public string CoinPlatinum { get => _coinPlatinum; set => Set(ref _coinPlatinum, value); }
    public string CoinGold { get => _coinGold; set => Set(ref _coinGold, value); }
    public string CoinSilver { get => _coinSilver; set => Set(ref _coinSilver, value); }
    public string CoinCopper { get => _coinCopper; set => Set(ref _coinCopper, value); }
    public string CoinStatus { get => _coinStatus; private set => Set(ref _coinStatus, value); }

    public string MonsterIndexSearch { get => _monsterIndexSearch; set => Set(ref _monsterIndexSearch, value); }
    public string MonsterIndexMinimumLevel { get => _monsterIndexMinimumLevel; set => Set(ref _monsterIndexMinimumLevel, value); }
    public string MonsterIndexMaximumLevel { get => _monsterIndexMaximumLevel; set => Set(ref _monsterIndexMaximumLevel, value); }
    public CustomWorldMonsterIndexRow? SelectedMonsterIndexRow
    {
        get => _selectedMonsterIndexRow;
        set { if (Set(ref _selectedMonsterIndexRow, value)) (OpenSelectedMonsterIndexCommand as RelayCommand)?.Raise(); }
    }
    public string MonsterIndexStatus { get => _monsterIndexStatus; private set => Set(ref _monsterIndexStatus, value); }

    public string ControlRoomSearch { get => _controlRoomSearch; set => Set(ref _controlRoomSearch, value); }
    public CustomWorldControlRoomRow? SelectedControlRoom
    {
        get => _selectedControlRoom;
        set { if (Set(ref _selectedControlRoom, value)) (OpenSelectedControlRoomCommand as RelayCommand)?.Raise(); }
    }
    public string ControlRoomStatus { get => _controlRoomStatus; private set => Set(ref _controlRoomStatus, value); }

    public string ShopResetSearch { get => _shopResetSearch; set => Set(ref _shopResetSearch, value); }
    public CustomWorldObjectRow? SelectedShopResetObject
    {
        get => _selectedShopResetObject;
        set { if (Set(ref _selectedShopResetObject, value)) (ResetSelectedShopStockCommand as RelayCommand)?.Raise(); }
    }
    public string ShopResetStatus { get => _shopResetStatus; private set => Set(ref _shopResetStatus, value); }

    public string RoomCleanupSearch { get => _roomCleanupSearch; set => Set(ref _roomCleanupSearch, value); }
    public CustomWorldObjectRow? SelectedRoomCleanupObject
    {
        get => _selectedRoomCleanupObject;
        set { if (Set(ref _selectedRoomCleanupObject, value)) (CombineSelectedRoomItemsCommand as RelayCommand)?.Raise(); }
    }
    public string RoomCleanupStatus { get => _roomCleanupStatus; private set => Set(ref _roomCleanupStatus, value); }

    public string TextblockStripFind { get => _textblockStripFind; set => Set(ref _textblockStripFind, value); }
    public string TextblockStripReplacement { get => _textblockStripReplacement; set => Set(ref _textblockStripReplacement, value); }
    public bool TextblockStripSuffixOnly { get => _textblockStripSuffixOnly; set => Set(ref _textblockStripSuffixOnly, value); }
    public CustomWorldTextblockStripPreviewRow? SelectedTextblockStripPreview
    {
        get => _selectedTextblockStripPreview;
        set => Set(ref _selectedTextblockStripPreview, value);
    }
    public string TextblockStripStatus { get => _textblockStripStatus; private set => Set(ref _textblockStripStatus, value); }

    public CustomWorldDedicatedTabRow? SelectedDedicatedTab
    {
        get => _selectedDedicatedTab;
        set => Set(ref _selectedDedicatedTab, value);
    }
    public NpcAttackEditorRow? SelectedNpcAttack
    {
        get => _selectedNpcAttack;
        set
        {
            if (!Set(ref _selectedNpcAttack, value)) return;
            (RemoveNpcAttackCommand as RelayCommand)?.Raise();
        }
    }
    public NpcAbilityEditorRow? SelectedNpcAbility
    {
        get => _selectedNpcAbility;
        set
        {
            if (!Set(ref _selectedNpcAbility, value)) return;
            (RemoveNpcAbilityCommand as RelayCommand)?.Raise();
        }
    }
    public ShopInventoryEditorRow? SelectedShopInventory
    {
        get => _selectedShopInventory;
        set
        {
            if (!Set(ref _selectedShopInventory, value)) return;
            (RemoveShopInventoryCommand as RelayCommand)?.Raise();
        }
    }
    public RoomVisualMapNodeRow? SelectedVisualMapNode
    {
        get => _selectedVisualMapNode;
        private set => Set(ref _selectedVisualMapNode, value);
    }
    public int SelectedVisualMapNumber
    {
        get => _selectedVisualMapNumber;
        set
        {
            if (!Set(ref _selectedVisualMapNumber, value) || _loadingVisualMap) return;
            LoadVisualMapEditor(value);
        }
    }
    public double VisualMapZoom
    {
        get => _visualMapZoom;
        set => Set(ref _visualMapZoom, Math.Clamp(value, 0.45, 1.75));
    }
    public string VisualMapStatus { get => _visualMapStatus; private set => Set(ref _visualMapStatus, value); }
    public string VisualMapTrailText { get => _visualMapTrailText; private set => Set(ref _visualMapTrailText, value); }
    public int VisualMapRoomCount => RoomVisualMapNodes.Count;
    public int VisualMapEdgeCount => RoomVisualMapEdges.Count;
    public double VisualMapCanvasWidth => Math.Max(3000, RoomVisualMapNodes.Count == 0 ? 3000 : RoomVisualMapNodes.Max(node => node.X) + 520);
    public double VisualMapCanvasHeight => Math.Max(1900, RoomVisualMapNodes.Count == 0 ? 1900 : RoomVisualMapNodes.Max(node => node.Y) + 420);
    public bool VisualMapLayoutDirty
    {
        get => _visualMapLayoutDirty;
        private set => Set(ref _visualMapLayoutDirty, value);
    }
    public CustomWorldObjectRow? SelectedSimpleObject
    {
        get => _selectedSimpleObject;
        set
        {
            if (!Set(ref _selectedSimpleObject, value) || value is null) return;
            LoadSimpleObject(value);
        }
    }

    public string OverrideFamily { get => _overrideFamily; set => Set(ref _overrideFamily, value); }
    public string OverrideTargetId { get => _overrideTargetId; set => Set(ref _overrideTargetId, value); }
    public string OverrideOperation { get => _overrideOperation; set => Set(ref _overrideOperation, value); }
    public string OverrideFieldsJson { get => _overrideFieldsJson; set => Set(ref _overrideFieldsJson, value); }
    public string ObjectSearch { get => _objectSearch; set => Set(ref _objectSearch, value); }
    public string BaseRecordJson { get => _baseRecordJson; private set => Set(ref _baseRecordJson, value); }
    public CustomWorldObjectRow? SelectedBaseObject
    {
        get => _selectedBaseObject;
        set
        {
            if (!Set(ref _selectedBaseObject, value) || value is null) return;
            OverrideFamily = value.Family;
            OverrideTargetId = value.Id;
            try
            {
                BaseRecordJson = _customService.ReadBaseRecordJson(value.Family, value.Id);
            }
            catch (Exception exception)
            {
                BaseRecordJson = "FAILED: " + exception.Message;
            }

            var existing = CustomOverrides.FirstOrDefault(item =>
                item.Family.Equals(value.Family, StringComparison.OrdinalIgnoreCase) &&
                item.TargetId.Equals(value.Id, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) SelectedOverride = existing;
            else
            {
                SelectedOverride = null;
                OverrideOperation = "patch";
                OverrideFieldsJson = "{\n  \"name\": \"\"\n}";
            }
        }
    }

    public CustomWorldOverrideRow? SelectedOverride
    {
        get => _selectedOverride;
        set
        {
            if (!Set(ref _selectedOverride, value)) return;
            if (value is not null)
            {
                OverrideFamily = value.Family;
                OverrideTargetId = value.TargetId;
                OverrideOperation = value.Operation;
                OverrideFieldsJson = value.FieldsJson;
            }
            (RemoveOverrideCommand as RelayCommand)?.Raise();
        }
    }

    public ICommand SaveClassicCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand SaveCustomSettingsCommand { get; }
    public ICommand BuildCustomWorldCommand { get; }
    public ICommand SaveOverrideCommand { get; }
    public ICommand RemoveOverrideCommand { get; }
    public ICommand NewOverrideCommand { get; }
    public ICommand BrowseObjectsCommand { get; }
    public ICommand BrowseSimpleObjectsCommand { get; }
    public ICommand NewSimpleObjectCommand { get; }
    public ICommand CloneSimpleObjectCommand { get; }
    public ICommand SaveSimpleObjectCommand { get; }
    public ICommand DeleteSimpleObjectCommand { get; }
    public ICommand DiscardSimpleObjectCommand { get; }
    public ICommand AddNpcAttackCommand { get; }
    public ICommand RemoveNpcAttackCommand { get; }
    public ICommand AddNpcAbilityCommand { get; }
    public ICommand RemoveNpcAbilityCommand { get; }
    public ICommand AddShopInventoryCommand { get; }
    public ICommand RemoveShopInventoryCommand { get; }
    public ICommand RefreshVisualMapCommand { get; }
    public ICommand SaveVisualMapLayoutCommand { get; }
    public ICommand AutoArrangeVisualMapCommand { get; }
    public ICommand NewRoomFromMapCommand { get; }
    public ICommand OpenExitEditorCommand { get; }
    public ICommand OpenSpawnEditorCommand { get; }
    public ICommand OpenShopEditorCommand { get; }
    public ICommand OpenTriggerEditorCommand { get; }
    public ICommand OpenReferenceLookupCommand { get; }
    public ICommand RefreshReferenceLookupCommand { get; }
    public ICommand ApplyReferenceLookupCommand { get; }
    public ICommand CloseReferenceLookupCommand { get; }
    public ICommand BrowseToolReferenceObjectsCommand { get; }
    public ICommand FindWhereUsedCommand { get; }
    public ICommand OpenCrossReferenceHitCommand { get; }
    public ICommand BrowseNpcToolObjectsCommand { get; }
    public ICommand RunNpcSimulationCommand { get; }
    public ICommand OpenNpcToolObjectCommand { get; }
    public ICommand BrowseWeaponToolObjectsCommand { get; }
    public ICommand RunWeaponSimulationCommand { get; }
    public ICommand OpenWeaponToolObjectCommand { get; }
    public ICommand RunCustomValidationCommand { get; }
    public ICommand OpenValidationIssueCommand { get; }
    public ICommand RunGlobalSearchCommand { get; }
    public ICommand OpenGlobalSearchHitCommand { get; }
    public ICommand ExportAllCustomChangesCommand { get; }
    public ICommand ExportCurrentFamilyCommand { get; }
    public ICommand ExportCurrentRecordCommand { get; }
    public ICommand PreviewImportPackageCommand { get; }
    public ICommand ApplyImportPackageCommand { get; }
    public ICommand AnalyzeNightmareMdbCommand { get; }
    public ICommand ImportNightmareMdbCommand { get; }
    public ICommand ExportNightmareMdbChangedCommand { get; }
    public ICommand ExportNightmareMdbFullCommand { get; }
    public ICommand ChooseWccEvidenceCommand { get; }
    public ICommand AnalyzeWccGapsCommand { get; }
    public ICommand RunBulkSetCommand { get; }
    public ICommand RunBulkCopyCommand { get; }
    public ICommand BrowseKeyDoorsCommand { get; }
    public ICommand OpenSelectedKeyDoorCommand { get; }
    public ICommand OpenSelectedKeyItemCommand { get; }
    public ICommand BrowseCoinRoomsCommand { get; }
    public ICommand SetCoinRoomCurrencyCommand { get; }
    public ICommand BrowseMonsterIndexCommand { get; }
    public ICommand OpenSelectedMonsterIndexCommand { get; }
    public ICommand BrowseControlRoomsCommand { get; }
    public ICommand OpenSelectedControlRoomCommand { get; }
    public ICommand BrowseShopResetObjectsCommand { get; }
    public ICommand ResetSelectedShopStockCommand { get; }
    public ICommand BrowseRoomCleanupObjectsCommand { get; }
    public ICommand CombineSelectedRoomItemsCommand { get; }
    public ICommand PreviewTextblockStripCommand { get; }
    public ICommand ApplyTextblockStripCommand { get; }

    public void Load()
    {
        LoadClassic();
        LoadCustom();
        BrowseToolReferenceObjects();
        BrowseNpcToolObjects();
        BrowseWeaponToolObjects();
        BrowseKeyDoors();
        BrowseCoinRooms();
        BrowseMonsterIndex();
        BrowseControlRooms();
        BrowseShopResetObjects();
        BrowseRoomCleanupObjects();
        NightmareParityRows.Clear();
        foreach (var row in _customService.BuildNightmareParityAudit()) NightmareParityRows.Add(row);
        StartNewSimpleObject();
    }

    private void LoadClassic()
    {
        try
        {
            var snapshot = _classicService.Load();
            ProjectRoot = snapshot.ProjectRoot;
            PolicyPath = snapshot.PolicyPath;
            UnlimitedItemsEnabled = snapshot.Policy.UnlimitedItemsEnabled;
            EveryoneCanUseSysGoto = snapshot.Policy.EveryoneCanUseSysGoto;

            Characters.Clear();
            foreach (var character in snapshot.Characters)
            {
                Characters.Add(new ClassicCharacterAccessRow(
                    character,
                    snapshot.Policy.HasCharacterSysopAccess(character.CharacterId)));
            }

            var warning = snapshot.Warnings.Count == 0
                ? string.Empty
                : $" Warning: {string.Join(" | ", snapshot.Warnings.Take(3))}";
            ClassicStatus = $"Loaded and verified Classic settings. {Characters.Count} character(s) available.{warning}";
        }
        catch (Exception exception)
        {
            ClassicStatus = "FAILED: " + exception.Message;
        }
    }

    private void SaveClassic()
    {
        try
        {
            var policy = new RuntimeClassicAdministrationPolicy
            {
                WorldId = "classic",
                UnlimitedItemsEnabled = UnlimitedItemsEnabled,
                EveryoneCanUseSysGoto = EveryoneCanUseSysGoto,
                SysopCharacterIds = Characters
                    .Where(character => character.HasSysopAccess)
                    .Select(character => character.CharacterId)
                    .ToList()
            };

            var result = _classicService.Save(policy);
            UnlimitedItemsEnabled = result.Policy.UnlimitedItemsEnabled;
            EveryoneCanUseSysGoto = result.Policy.EveryoneCanUseSysGoto;
            foreach (var character in Characters)
                character.HasSysopAccess = result.Policy.HasCharacterSysopAccess(character.CharacterId);
            PolicyPath = result.PolicyPath;
            ClassicStatus = $"Saved and read-back verified at {DateTime.Now:g}. {result.Policy.SysopCharacterIds.Count} character SYSOP grant(s).";
        }
        catch (Exception exception)
        {
            ClassicStatus = "FAILED: " + exception.Message;
        }
    }

    private void LoadCustom()
    {
        try
        {
            var snapshot = _customService.Load();
            ProjectRoot = snapshot.ProjectRoot;
            CustomWorldName = snapshot.Settings.Name;
            CustomDescription = snapshot.Settings.Description;
            CustomLoginBanner = snapshot.Settings.LoginBanner;
            CustomMotd = snapshot.Settings.Motd;
            CustomRuntimeEnabled = snapshot.Settings.RuntimeEnabled;
            CustomTransfersAllowed = snapshot.Settings.TransfersAllowed;
            CustomConfigPath = snapshot.ConfigPath;
            CustomOverridePath = snapshot.OverridePath;
            CustomGeneratedDataPath = snapshot.GeneratedDataPath;
            CustomGeneratedState = snapshot.GeneratedDataReady ? "Runtime data ready" : "Needs build";

            CustomOverrides.Clear();
            foreach (var item in snapshot.Overrides) CustomOverrides.Add(new CustomWorldOverrideRow(item));
            CustomStatus = $"Custom1 enabled as a separate ReforgedCustom world. {CustomOverrides.Count} authored override(s).";
            BrowseSimpleObjects();
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
    }

    private void SaveCustomSettings()
    {
        try
        {
            var saved = _customService.SaveSettings(new CustomWorldEditorSettings(
                CustomWorldEditorService.CustomWorldId,
                CustomWorldName,
                CustomDescription,
                CustomLoginBanner,
                CustomMotd,
                CustomRuntimeEnabled,
                CustomTransfersAllowed,
                $"Data/{CustomWorldEditorService.CustomWorldId}"));
            CustomWorldName = saved.Name;
            CustomDescription = saved.Description;
            CustomLoginBanner = saved.LoginBanner;
            CustomMotd = saved.Motd;
            CustomRuntimeEnabled = saved.RuntimeEnabled;
            CustomTransfersAllowed = saved.TransfersAllowed;
            CustomStatus = $"Saved Custom1 world settings at {DateTime.Now:g}.";
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
    }

    private void BrowseSimpleObjects()
    {
        try
        {
            var results = _customService.BrowseObjects(SimpleFamily, ObjectSearch);
            SimpleObjects.Clear();
            foreach (var item in results) SimpleObjects.Add(new CustomWorldObjectRow(item));
            CustomStatus = $"{results.Count} {SimpleFamily} object(s) available. Select one to edit or click New.";
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
    }

    private void StartNewSimpleObject()
    {
        try
        {
            var form = _customService.GetSimpleForm(SimpleFamily);
            _selectedSimpleObject = null;
            Raise(nameof(SelectedSimpleObject));
            SimpleObjectId = string.Empty;
            SimpleIsNew = true;
            SimpleFormTitle = $"New {form.SingularName}";
            SimpleFormHelp = IsDedicatedEditor
                ? form.HelpText + " Use the large tabs below; every normal gameplay/content field stays available without JSON."
                : form.HelpText + " Fill the required fields first, then open any More settings section you want. Every discovered gameplay/content field stays available without JSON.";
            PopulateSimpleForm(form, null);
            CustomStatus = $"Ready to create {form.SingularName}. {SimpleControlSummary}";
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
    }

    private void LoadSimpleObject(CustomWorldObjectRow item)
    {
        try
        {
            var form = _customService.GetSimpleForm(item.Family, item.Id);
            var loaded = _customService.LoadSimpleRecord(item.Family, item.Id);
            SimpleObjectId = item.Id;
            SimpleIsNew = false;
            SimpleFormTitle = $"Edit {form.SingularName}: {(string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name)}";
            SimpleFormHelp = IsDedicatedEditor
                ? form.HelpText + " Use the dedicated tabs below. Reforged keeps runtime/provenance bookkeeping out of the normal creator view."
                : form.HelpText + " Required fields stay at the top. Open any More settings section below to edit the discovered Librarium fields, including nested settings and structured collections.";
            PopulateSimpleForm(form, loaded);
            CustomStatus = $"Loaded {item.Family}/{item.Id}. {SimpleControlSummary}";
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
    }

    private void PopulateSimpleForm(
        CustomWorldSimpleFormDefinition form,
        (bool ExistsInBase, IReadOnlyDictionary<string, string> TextValues, IReadOnlyDictionary<string, bool> BoolValues)? loaded)
    {
        SimpleFields.Clear();
        SimpleRequiredFields.Clear();
        SimpleOptionalFields.Clear();
        SimpleSections.Clear();
        DedicatedTabs.Clear();
        NpcAttacks.Clear();
        NpcAbilities.Clear();
        RoomMapGridTiles.Clear();
        RoomMapVerticalLinks.Clear();
        RoomVisualMapNodes.Clear();
        RoomVisualMapEdges.Clear();
        VisualMapNumbers.Clear();
        ShopInventory.Clear();
        SelectedShopInventory = null;
        _npcAttacksDirty = false;
        _npcAbilitiesDirty = false;
        _roomMapLinksDirty = false;
        _shopInventoryDirty = false;

        foreach (var field in form.Fields)
        {
            string? text = null;
            var boolean = false;
            if (loaded is not null)
            {
                loaded.Value.TextValues.TryGetValue(field.Path, out text);
                loaded.Value.BoolValues.TryGetValue(field.Path, out boolean);
            }
            var row = new CustomWorldSimpleFieldRow(field, text,
                field.Kind == CustomWorldSimpleFieldKind.Boolean && loaded is not null ? boolean : null);
            SimpleFields.Add(row);
            if (field.Required) SimpleRequiredFields.Add(row);
            else SimpleOptionalFields.Add(row);
        }

        foreach (var group in SimpleOptionalFields
                     .GroupBy(field => SectionForPath(field.Path))
                     .OrderBy(group => SectionOrder(group.Key))
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            SimpleSections.Add(new CustomWorldSimpleSectionRow(
                group.Key,
                SectionDescription(group.Key),
                group.OrderBy(field => field.Label, StringComparer.OrdinalIgnoreCase)));
        }

        SelectedSimpleSection = SimpleSections.FirstOrDefault();
        BuildDedicatedTabs();
        LoadNpcCollectionEditors();
        LoadRoomMapEditor();
        LoadVisualMapEditor();
        LoadShopInventoryEditor();
        Raise(nameof(SimpleFieldCount));
        Raise(nameof(SimpleControlSummary));
    }

    private void CloneSimpleObject()
    {
        if (SimpleIsNew || string.IsNullOrWhiteSpace(SimpleObjectId)) return;
        var sourceId = SimpleObjectId;
        _selectedSimpleObject = null;
        Raise(nameof(SelectedSimpleObject));
        SimpleObjectId = sourceId + ".copy." + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SimpleIsNew = true;
        SimpleFormTitle = "Clone: " + SimpleFormTitle.Replace("Edit ", string.Empty, StringComparison.OrdinalIgnoreCase);
        SimpleFormHelp = "This is a complete editable copy. Change the name, room number, stats, effects, collections, or any other fields you want, then Save & Build.";
        CustomStatus = "Clone prepared with a new Custom1-only ID. Change anything you want and save.";
    }

    private void RaiseEditorModeProperties()
    {
        Raise(nameof(IsRoomEditor));
        Raise(nameof(IsItemEditor));
        Raise(nameof(IsNpcEditor));
        Raise(nameof(IsSpellEditor));
        Raise(nameof(IsAbilityEditor));
        Raise(nameof(IsMessageEditor));
        Raise(nameof(IsTextblockEditor));
        Raise(nameof(IsActionEditor));
        Raise(nameof(IsClassEditor));
        Raise(nameof(IsRaceEditor));
        Raise(nameof(IsShopEditor));
        Raise(nameof(IsDedicatedEditor));
        Raise(nameof(IsGenericEditor));
        Raise(nameof(DedicatedEditorName));
    }

    private void OpenReferenceLookup(CustomWorldSimpleFieldRow field)
    {
        if (!field.HasReferenceLookup) return;
        _referenceLookupTarget = field;
        ReferenceLookupFamily = field.ReferenceFamily;
        ReferenceLookupTitle = $"Choose {field.Label}";
        ReferenceLookupSearch = string.Empty;
        ReferenceLookupOpen = true;
        RefreshReferenceLookup();
    }

    private void RefreshReferenceLookup()
    {
        ReferenceLookupResults.Clear();
        SelectedReferenceLookupResult = null;
        if (!ReferenceLookupOpen || string.IsNullOrWhiteSpace(ReferenceLookupFamily)) return;
        try
        {
            foreach (var item in _customService.BrowseObjects(ReferenceLookupFamily, ReferenceLookupSearch, 350))
                ReferenceLookupResults.Add(new CustomWorldObjectRow(item));
            CustomStatus = $"{ReferenceLookupResults.Count} matching {CustomWorldSimpleFormCatalog.LabelForFamily(ReferenceLookupFamily).ToLowerInvariant()} record(s).";
        }
        catch (Exception exception)
        {
            CustomStatus = "Lookup failed: " + exception.Message;
        }
    }

    private void ApplyReferenceLookup()
    {
        if (_referenceLookupTarget is null || SelectedReferenceLookupResult is null) return;
        try
        {
            var value = _customService.ResolveReferenceValue(ReferenceLookupFamily, SelectedReferenceLookupResult.Id,
                _referenceLookupTarget.Path, _referenceLookupTarget.Kind);
            if (_referenceLookupTarget.Kind is CustomWorldSimpleFieldKind.Lines or CustomWorldSimpleFieldKind.IntegerLines)
            {
                var existing = _referenceLookupTarget.TextValue.Trim();
                _referenceLookupTarget.TextValue = existing.Length == 0 ? value : existing + Environment.NewLine + value;
            }
            else
            {
                _referenceLookupTarget.TextValue = value;
            }
            CustomStatus = $"Linked {_referenceLookupTarget.Label} to {SelectedReferenceLookupResult.Name} ({value}).";
            CloseReferenceLookup();
        }
        catch (Exception exception)
        {
            CustomStatus = "Lookup failed: " + exception.Message;
        }
    }

    private void CloseReferenceLookup()
    {
        ReferenceLookupOpen = false;
        _referenceLookupTarget = null;
        ReferenceLookupResults.Clear();
        SelectedReferenceLookupResult = null;
        ReferenceLookupFamily = string.Empty;
    }

    private void BrowseToolReferenceObjects()
    {
        ToolReferenceObjects.Clear();
        SelectedToolReferenceObject = null;
        CrossReferenceHits.Clear();
        SelectedCrossReferenceHit = null;
        try
        {
            foreach (var item in _customService.BrowseObjects(ToolReferenceFamily, ToolReferenceSearch, 500))
                ToolReferenceObjects.Add(new CustomWorldObjectRow(item));
            CrossReferenceStatus = $"{ToolReferenceObjects.Count} record(s) available. Select one and click Find Where Used.";
        }
        catch (Exception exception)
        {
            CrossReferenceStatus = "Browse failed: " + exception.Message;
        }
    }

    private void FindWhereUsed()
    {
        CrossReferenceHits.Clear();
        SelectedCrossReferenceHit = null;
        if (SelectedToolReferenceObject is null) return;
        try
        {
            foreach (var hit in _customService.FindReferences(SelectedToolReferenceObject.Family, SelectedToolReferenceObject.Id, 2500))
                CrossReferenceHits.Add(hit);
            CrossReferenceStatus = CrossReferenceHits.Count == 0
                ? $"No creator-facing references to {SelectedToolReferenceObject.Name} were found."
                : $"Found {CrossReferenceHits.Count} place(s) that reference {SelectedToolReferenceObject.Name}. Double-click/open a result to edit it.";
        }
        catch (Exception exception)
        {
            CrossReferenceStatus = "Cross-reference failed: " + exception.Message;
        }
    }

    private void OpenSelectedCrossReferenceHit()
    {
        if (SelectedCrossReferenceHit is null) return;
        OpenToolObject(SelectedCrossReferenceHit.Family, SelectedCrossReferenceHit.Id);
    }

    private void BrowseNpcToolObjects()
    {
        NpcToolObjects.Clear();
        SelectedNpcToolObject = null;
        try
        {
            foreach (var item in _customService.BrowseObjects("npcs", NpcToolSearch, 600))
                NpcToolObjects.Add(new CustomWorldObjectRow(item));
            NpcSimulationSummary = $"{NpcToolObjects.Count} monster/NPC record(s). Choose one and run the simulator.";
        }
        catch (Exception exception)
        {
            NpcSimulationSummary = "Monster browse failed: " + exception.Message;
        }
    }

    private void RunNpcSimulation()
    {
        NpcSimulationRows.Clear();
        if (SelectedNpcToolObject is null) return;
        try
        {
            var result = _customService.SimulateNpcCombat(
                SelectedNpcToolObject.Id,
                ParseToolInt(NpcTargetDefense, 0, 0, 9999),
                ParseToolInt(NpcTargetArmorClass, 50, 0, 9999),
                ParseToolInt(NpcTargetDamageResistance, 0, 0, 9999),
                ParseToolInt(NpcTargetDodge, 0, 0, 9999),
                ParseToolInt(NpcSimulationRounds, 1000, 1, 100000));
            foreach (var row in result.Attacks) NpcSimulationRows.Add(row);
            NpcSimulationSummary =
                $"{result.NpcName}: {result.Rounds:N0} rounds • {result.TotalSelections:N0} selected attacks • " +
                $"physical hit/miss/dodge {result.PhysicalHits:N0}/{result.PhysicalMisses:N0}/{result.PhysicalDodges:N0} • " +
                $"spell casts/fails {result.SpellCasts:N0}/{result.SpellFailures:N0} • " +
                $"physical damage {result.TotalPhysicalDamage:N0} ({result.AveragePhysicalDamagePerRound:0.00}/round).  {result.Notes}";
        }
        catch (Exception exception)
        {
            NpcSimulationSummary = "Simulation failed: " + exception.Message;
        }
    }

    private void BrowseWeaponToolObjects()
    {
        WeaponToolObjects.Clear();
        SelectedWeaponToolObject = null;
        try
        {
            foreach (var item in _customService.BrowseObjects("items", WeaponToolSearch, 600))
                WeaponToolObjects.Add(new CustomWorldObjectRow(item));
            WeaponSimulationSummary = $"{WeaponToolObjects.Count} item record(s). Choose a weapon and run the calculator.";
        }
        catch (Exception exception)
        {
            WeaponSimulationSummary = "Item browse failed: " + exception.Message;
        }
    }

    private void RunWeaponSimulation()
    {
        if (SelectedWeaponToolObject is null) return;
        try
        {
            var result = _customService.SimulateWeapon(
                SelectedWeaponToolObject.Id,
                ParseToolInt(WeaponBaseAttackValue, 100, 1, 9999),
                ParseToolInt(WeaponTargetDefense, 0, 0, 9999),
                ParseToolInt(WeaponTargetArmorClass, 50, 0, 9999),
                ParseToolInt(WeaponTargetDamageResistance, 0, 0, 9999),
                ParseToolInt(WeaponTargetDodge, 0, 0, 9999),
                ParseToolInt(WeaponCriticalChance, 0, 0, 100),
                ParseToolInt(WeaponSimulationSwings, 1000, 1, 100000));
            WeaponSimulationSummary =
                $"{result.ItemName}: damage {result.MinimumDamage}-{result.MaximumDamage}, accuracy {result.Accuracy:+#;-#;0}, speed {result.Speed}. " +
                $"Effective attack {result.EffectiveAttackValue}; runtime hit chance {result.HitChance}%. " +
                $"Across {result.Swings:N0} swings: {result.Hits:N0} hits, {result.Misses:N0} misses, {result.Dodges:N0} dodges, {result.Criticals:N0} criticals; " +
                $"average {result.AverageDamagePerSwing:0.00} damage/swing and {result.AverageDamageOnHit:0.00} damage/landed hit.  {result.Notes}";
        }
        catch (Exception exception)
        {
            WeaponSimulationSummary = "Calculator failed: " + exception.Message;
        }
    }

    private void RunCustomValidation()
    {
        ValidationIssues.Clear();
        SelectedValidationIssue = null;
        ValidationStatus = "Validating Custom1...";
        try
        {
            foreach (var issue in _customService.ValidateCustomWorld(3000)) ValidationIssues.Add(issue);
            var errors = ValidationIssues.Count(issue => issue.Severity.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
            var warnings = ValidationIssues.Count - errors;
            ValidationStatus = errors == 0 && warnings == 0
                ? "PASS — no broken creator-facing references or structural content problems were found."
                : $"Validation finished: {errors} error(s), {warnings} warning(s). Select an issue and click Open Record to fix it.";
        }
        catch (Exception exception)
        {
            ValidationStatus = "Validation failed: " + exception.Message;
        }
    }

    private void OpenSelectedValidationIssue()
    {
        if (SelectedValidationIssue is null) return;
        OpenToolObject(SelectedValidationIssue.Family, SelectedValidationIssue.Id);
    }

    private void OpenToolObject(string family, string? objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId)) return;
        var option = SimpleFamilies.FirstOrDefault(item => item.Value.Equals(family, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            CustomStatus = $"The {family} family is not available in the creator editor.";
            return;
        }

        EditorWorkspaceTabIndex = 0;
        SimpleFamily = option.Value;
        ObjectSearch = objectId;
        BrowseSimpleObjects();
        var match = SimpleObjects.FirstOrDefault(item => item.Id.Equals(objectId, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            SelectedSimpleObject = match;
            CustomStatus = $"Opened {match.Name} from Creator Tools.";
        }
        else
        {
            CustomStatus = $"Could not locate '{objectId}' after switching to {option.Label}.";
        }
    }

    private void RunGlobalSearch()
    {
        GlobalSearchHits.Clear();
        SelectedGlobalSearchHit = null;
        try
        {
            foreach (var hit in _customService.GlobalSearch(GlobalSearchText, 2000)) GlobalSearchHits.Add(hit);
            GlobalSearchStatus = GlobalSearchHits.Count == 0
                ? "No matching records found."
                : $"Found {GlobalSearchHits.Count} matching record(s). Double-click/open a result to edit it.";
        }
        catch (Exception exception) { GlobalSearchStatus = "Global search failed: " + exception.Message; }
    }

    private void OpenSelectedGlobalSearchHit()
    {
        if (SelectedGlobalSearchHit is null) return;
        OpenToolObject(SelectedGlobalSearchHit.Family, SelectedGlobalSearchHit.Id);
    }

    private void ExportCustomChanges(string scope)
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export Reforged Custom1 Creator Package",
                Filter = "Reforged Custom1 package (*.json)|*.json|All files (*.*)|*.*",
                FileName = scope switch
                {
                    "record" => $"Custom1-{SimpleFamily}-{SanitizeFileName(SimpleObjectId)}.json",
                    "family" => $"Custom1-{SimpleFamily}.json",
                    _ => "Custom1-All-Changes.json"
                }
            };
            if (dialog.ShowDialog() != true) return;
            var count = scope switch
            {
                "record" => _customService.ExportCustomChanges(dialog.FileName, SimpleFamily, [SimpleObjectId]),
                "family" => _customService.ExportCustomChanges(dialog.FileName, SimpleFamily),
                _ => _customService.ExportCustomChanges(dialog.FileName)
            };
            ImportExportStatus = $"Exported {count} Custom1 change(s) to {dialog.FileName}";
        }
        catch (Exception exception) { ImportExportStatus = "Export failed: " + exception.Message; }
    }

    private void PreviewImportPackage()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Preview Reforged Custom1 Import Package",
                Filter = "Reforged Custom1 package (*.json)|*.json|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;
            PendingImportPath = dialog.FileName;
            PendingImportPreview = _customService.PreviewImportPackage(dialog.FileName);
            ImportExportStatus = PendingImportPreview.Summary;
        }
        catch (Exception exception)
        {
            PendingImportPreview = null;
            ImportExportStatus = "Import preview failed: " + exception.Message;
        }
    }

    private void ApplyImportPackage()
    {
        if (PendingImportPreview is null || string.IsNullOrWhiteSpace(PendingImportPath)) return;
        try
        {
            var applied = _customService.ApplyImportPackage(PendingImportPath);
            PendingImportPreview = null;
            ImportExportStatus = applied.Summary;
            LoadCustom();
            BrowseSimpleObjects();
        }
        catch (Exception exception) { ImportExportStatus = "Import failed: " + exception.Message; }
    }

    private void AnalyzeNightmareMdb()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Analyze Nightmare Redux / Access MDB",
                Filter = "Nightmare Redux / Access MDB (*.mdb)|*.mdb|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            NightmareMdbPath = dialog.FileName;
            PendingNightmareMdbAnalysis = _nightmareMdbBridge.AnalyzeMdb(dialog.FileName);
            NightmareMdbTables.Clear();
            foreach (var table in PendingNightmareMdbAnalysis.Tables) NightmareMdbTables.Add(table);
            SelectedNightmareMdbTable = NightmareMdbTables.FirstOrDefault();
            NightmareMdbStatus = PendingNightmareMdbAnalysis.Summary + " Review the table list before importing.";
        }
        catch (Exception exception)
        {
            PendingNightmareMdbAnalysis = null;
            NightmareMdbTables.Clear();
            NightmareMdbStatus = "MDB analysis failed: " + exception.Message;
        }
    }

    private void ImportNightmareMdb()
    {
        if (PendingNightmareMdbAnalysis is null || string.IsNullOrWhiteSpace(NightmareMdbPath)) return;
        try
        {
            var result = _nightmareMdbBridge.ImportMdb(NightmareMdbPath, string.IsNullOrWhiteSpace(WccEvidenceFolder) ? null : WccEvidenceFolder);
            NightmareMdbStatus = result.Summary + " Build Custom1 when you are ready to test the imported world.";
            LoadCustom();
            BrowseSimpleObjects();
            PendingNightmareMdbAnalysis = _nightmareMdbBridge.AnalyzeMdb(NightmareMdbPath);
            NightmareMdbTables.Clear();
            foreach (var table in PendingNightmareMdbAnalysis.Tables) NightmareMdbTables.Add(table);
        }
        catch (Exception exception)
        {
            NightmareMdbStatus = "Nightmare MDB import failed: " + exception.Message;
        }
    }

    private void ExportNightmareMdb(bool changedOnly)
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = changedOnly ? "Export Custom1 Changes to Nightmare MDB" : "Export Effective Custom1 World to Nightmare MDB",
                Filter = "Nightmare Redux / Access MDB (*.mdb)|*.mdb|All files (*.*)|*.*",
                FileName = changedOnly ? "Custom1-Nightmare-Changed.mdb" : "Custom1-Nightmare-Full.mdb",
                AddExtension = true,
                DefaultExt = ".mdb"
            };
            if (dialog.ShowDialog() != true) return;
            var result = _nightmareMdbBridge.ExportMdb(dialog.FileName, changedOnly);
            NightmareMdbStatus = result.Summary;
        }
        catch (Exception exception)
        {
            NightmareMdbStatus = "Nightmare MDB export failed: " + exception.Message;
        }
    }

    private void ChooseWccEvidence()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choose Any Original WCC / WGS DAT File",
                Filter = "Worldgroup / MajorMUD data (*.dat;*.bin)|*.dat;*.bin|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;
            var folder = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            var analysis = _nightmareMdbBridge.AnalyzeWccEvidenceFolder(folder);
            WccEvidenceFolder = analysis.Folder;
            WccEvidenceFiles.Clear();
            foreach (var file in analysis.Files) WccEvidenceFiles.Add(file);
            SelectedWccEvidenceFile = WccEvidenceFiles.FirstOrDefault();
            WccEvidenceStatus = analysis.Summary;
        }
        catch (Exception exception)
        {
            WccEvidenceStatus = "WCC evidence scan failed: " + exception.Message;
        }
    }

    private void AnalyzeWccGaps()
    {
        if (PendingNightmareMdbAnalysis is null || string.IsNullOrWhiteSpace(NightmareMdbPath) || string.IsNullOrWhiteSpace(WccEvidenceFolder)) return;
        try
        {
            PendingWccGapAnalysis = _nightmareMdbBridge.AnalyzeMdbWccGaps(NightmareMdbPath, WccEvidenceFolder);
            WccGapRows.Clear();
            foreach (var row in PendingWccGapAnalysis.Rows) WccGapRows.Add(row);
            SelectedWccGapRow = WccGapRows.FirstOrDefault();
            WccGapStatus = PendingWccGapAnalysis.Summary;
        }
        catch (Exception exception)
        {
            PendingWccGapAnalysis = null;
            WccGapRows.Clear();
            WccGapStatus = "MDB/WCC gap analysis failed: " + exception.Message;
        }
    }

    private void RunBulkSet()
    {
        try
        {
            var result = _customService.BulkSetField(BulkFamily, SplitToolLines(BulkTargetIds), BulkFieldPath, BulkValue, BulkValueKind);
            BulkStatus = result.Summary;
            if (BulkFamily.Equals(SimpleFamily, StringComparison.OrdinalIgnoreCase)) BrowseSimpleObjects();
        }
        catch (Exception exception) { BulkStatus = "Bulk set failed: " + exception.Message; }
    }

    private void RunBulkCopy()
    {
        try
        {
            var result = _customService.BulkCopyField(BulkFamily, BulkSourceId, SplitToolLines(BulkTargetIds), BulkFieldPath);
            BulkStatus = result.Summary;
            if (BulkFamily.Equals(SimpleFamily, StringComparison.OrdinalIgnoreCase)) BrowseSimpleObjects();
        }
        catch (Exception exception) { BulkStatus = "Bulk copy failed: " + exception.Message; }
    }

    private void BrowseKeyDoors()
    {
        KeyDoorRows.Clear();
        SelectedKeyDoor = null;
        try
        {
            foreach (var row in _customService.BrowseKeyDoors(KeyDoorSearch, 2000)) KeyDoorRows.Add(row);
            KeyDoorStatus = $"{KeyDoorRows.Count} keyed exit(s) found.";
        }
        catch (Exception exception) { KeyDoorStatus = "Key/door scan failed: " + exception.Message; }
    }

    private void BrowseCoinRooms()
    {
        CoinRoomObjects.Clear();
        SelectedCoinRoom = null;
        try
        {
            foreach (var room in _customService.BrowseObjects("rooms", CoinRoomSearch, 1000)) CoinRoomObjects.Add(new CustomWorldObjectRow(room));
            CoinStatus = $"{CoinRoomObjects.Count} room(s). Select one to edit its current Custom1 currency state.";
        }
        catch (Exception exception) { CoinStatus = "Room browse failed: " + exception.Message; }
    }

    private void LoadCoinRoomCurrency()
    {
        if (SelectedCoinRoom is null) return;
        try
        {
            var value = _customService.GetCustom1RoomCurrency(SelectedCoinRoom.Id);
            CoinRunic = value.RunicCoins.ToString();
            CoinPlatinum = value.PlatinumPieces.ToString();
            CoinGold = value.GoldCrowns.ToString();
            CoinSilver = value.SilverNobles.ToString();
            CoinCopper = value.CopperFarthings.ToString();
            CoinStatus = $"Loaded current Custom1 room currency for {SelectedCoinRoom.Name}.";
        }
        catch (Exception exception) { CoinStatus = "Coin load failed: " + exception.Message; }
    }

    private void SetCoinRoomCurrency()
    {
        if (SelectedCoinRoom is null) return;
        try
        {
            var value = _customService.SetCustom1RoomCurrency(
                SelectedCoinRoom.Id,
                ParseToolLong(CoinRunic), ParseToolLong(CoinPlatinum), ParseToolLong(CoinGold), ParseToolLong(CoinSilver), ParseToolLong(CoinCopper));
            CoinRunic = value.RunicCoins.ToString(); CoinPlatinum = value.PlatinumPieces.ToString(); CoinGold = value.GoldCrowns.ToString(); CoinSilver = value.SilverNobles.ToString(); CoinCopper = value.CopperFarthings.ToString();
            CoinStatus = $"Saved current Custom1 room currency: {RuntimeClassicRoomCurrencyStore.Format(value)}";
        }
        catch (Exception exception) { CoinStatus = "Coin save failed: " + exception.Message; }
    }

    private void BrowseMonsterIndex()
    {
        MonsterIndexRows.Clear();
        SelectedMonsterIndexRow = null;
        try
        {
            var minimum = ParseToolInt(MonsterIndexMinimumLevel, 0, 0, 100000);
            var maximum = string.IsNullOrWhiteSpace(MonsterIndexMaximumLevel)
                ? int.MaxValue
                : ParseToolInt(MonsterIndexMaximumLevel, int.MaxValue, minimum, int.MaxValue);
            foreach (var row in _customService.BrowseMonsterIndex(MonsterIndexSearch, minimum, maximum, 5000))
                MonsterIndexRows.Add(row);
            MonsterIndexStatus = $"{MonsterIndexRows.Count} NPC/monster record(s). Level range {minimum}-{(maximum == int.MaxValue ? "any" : maximum)}.";
        }
        catch (Exception exception) { MonsterIndexStatus = "Monster index failed: " + exception.Message; }
    }

    private void BrowseControlRooms()
    {
        ControlRoomRows.Clear();
        SelectedControlRoom = null;
        try
        {
            foreach (var row in _customService.BrowseControlRooms(ControlRoomSearch, 5000)) ControlRoomRows.Add(row);
            ControlRoomStatus = $"{ControlRoomRows.Count} control/action room(s) referenced by interactions, triggers, or scripts.";
        }
        catch (Exception exception) { ControlRoomStatus = "Control-room report failed: " + exception.Message; }
    }

    private void BrowseShopResetObjects()
    {
        ShopResetObjects.Clear();
        SelectedShopResetObject = null;
        try
        {
            foreach (var shop in _customService.BrowseObjects("shops", ShopResetSearch, 2000)) ShopResetObjects.Add(new CustomWorldObjectRow(shop));
            ShopResetStatus = $"{ShopResetObjects.Count} shop/service record(s). Build Custom1 before resetting live stock.";
        }
        catch (Exception exception) { ShopResetStatus = "Shop browse failed: " + exception.Message; }
    }

    private void ResetSelectedShopStock()
    {
        if (SelectedShopResetObject is null) return;
        try
        {
            var result = _customService.ResetCustom1ShopStock(SelectedShopResetObject.Id);
            ShopResetStatus = $"Reset {SelectedShopResetObject.Name}: {result.StockEntries} stock line(s), {result.TotalQuantity} total item(s). Regeneration restarts from now. Classic was not touched.";
        }
        catch (Exception exception) { ShopResetStatus = "Shop reset failed: " + exception.Message; }
    }

    private void BrowseRoomCleanupObjects()
    {
        RoomCleanupObjects.Clear();
        SelectedRoomCleanupObject = null;
        try
        {
            foreach (var room in _customService.BrowseObjects("rooms", RoomCleanupSearch, 2000)) RoomCleanupObjects.Add(new CustomWorldObjectRow(room));
            RoomCleanupStatus = $"{RoomCleanupObjects.Count} room(s). Stop the Custom1 host before combining saved room stacks.";
        }
        catch (Exception exception) { RoomCleanupStatus = "Room browse failed: " + exception.Message; }
    }

    private void CombineSelectedRoomItems()
    {
        if (SelectedRoomCleanupObject is null) return;
        try
        {
            var result = _customService.CombineCustom1RoomItems(SelectedRoomCleanupObject.Id);
            RoomCleanupStatus = !result.StateFileFound
                ? "No Custom1 living-world save exists yet. There are no saved dynamic room stacks to combine."
                : $"Room cleanup complete: {result.EligibleStacks} eligible dynamic stack(s) -> {result.FinalStacks}; removed {result.RemovedStacks} duplicate stack record(s), preserving {result.TotalQuantity} total item quantity. Generated static placements were not changed.";
        }
        catch (Exception exception) { RoomCleanupStatus = "Room item cleanup failed: " + exception.Message; }
    }

    private void PreviewTextblockStrip()
    {
        TextblockStripPreviewRows.Clear();
        SelectedTextblockStripPreview = null;
        try
        {
            foreach (var row in _customService.PreviewTextblockStrip(TextblockStripFind, TextblockStripReplacement, TextblockStripSuffixOnly, 5000))
                TextblockStripPreviewRows.Add(row);
            TextblockStripStatus = TextblockStripPreviewRows.Count == 0
                ? "No textblocks would change. Nothing has been written."
                : $"Preview only: {TextblockStripPreviewRows.Count} textblock(s) would change. Review the before/after columns, then apply.";
        }
        catch (Exception exception) { TextblockStripStatus = "Textblock preview failed: " + exception.Message; }
        (ApplyTextblockStripCommand as RelayCommand)?.Raise();
    }

    private void ApplyTextblockStrip()
    {
        if (TextblockStripPreviewRows.Count == 0) return;
        try
        {
            var ids = TextblockStripPreviewRows.Select(row => row.TextblockId).ToArray();
            var result = _customService.ApplyTextblockStrip(TextblockStripFind, TextblockStripReplacement, TextblockStripSuffixOnly, ids);
            TextblockStripStatus = result.Summary + " Rebuild Custom1 before runtime testing.";
            TextblockStripPreviewRows.Clear();
            SelectedTextblockStripPreview = null;
            (ApplyTextblockStripCommand as RelayCommand)?.Raise();
        }
        catch (Exception exception) { TextblockStripStatus = "Textblock batch apply failed: " + exception.Message; }
    }

    private static IEnumerable<string> SplitToolLines(string? value) =>
        (value ?? string.Empty).Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static long ParseToolLong(string? value)
    {
        if (!long.TryParse(value?.Trim(), out var parsed) || parsed < 0) throw new InvalidOperationException("Coin amounts must be whole numbers 0 or greater.");
        return parsed;
    }

    private static string SanitizeFileName(string? value)
    {
        value = string.IsNullOrWhiteSpace(value) ? "record" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return value;
    }

    private static int ParseToolInt(string value, int fallback, int minimum, int maximum)
    {
        if (!int.TryParse(value?.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)) parsed = fallback;
        return Math.Clamp(parsed, minimum, maximum);
    }

    private void SwitchEditorFamily(string family)
    {
        var option = SimpleFamilies.FirstOrDefault(item => item.Value.Equals(family, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            CustomStatus = $"Editor family '{family}' is not available in this Librarium.";
            return;
        }
        SimpleFamily = option.Value;
    }

    private void BuildDedicatedTabs()
    {
        DedicatedTabs.Clear();
        if (!IsDedicatedEditor)
        {
            SelectedDedicatedTab = null;
            return;
        }

        var remaining = SimpleFields.ToList();
        void AddTab(string title, string description, Func<CustomWorldSimpleFieldRow, bool> predicate,
            bool npcAttacks = false, bool npcAbilities = false, bool roomMap = false, bool shopInventory = false, params string[] suppressPaths)
        {
            var selected = remaining.Where(predicate).ToList();
            foreach (var item in selected) remaining.Remove(item);
            var visible = selected.Where(field => !suppressPaths.Contains(field.Path, StringComparer.OrdinalIgnoreCase)).ToList();
            if (visible.Count == 0 && !npcAttacks && !npcAbilities && !roomMap && !shopInventory) return;
            DedicatedTabs.Add(new CustomWorldDedicatedTabRow(title, description, visible, npcAttacks, npcAbilities, roomMap, shopInventory));
        }

        bool Root(CustomWorldSimpleFieldRow field, params string[] roots)
        {
            var root = field.Path.TrimStart('@').Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            return roots.Contains(root, StringComparer.OrdinalIgnoreCase);
        }
        bool Starts(CustomWorldSimpleFieldRow field, params string[] prefixes)
            => prefixes.Any(prefix => field.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        bool Basic(CustomWorldSimpleFieldRow field)
            => field.IsRequired || Root(field, "name", "title", "display_name", "short_name", "legacy_id", "description", "classification", "map_number", "room_number", "flags");

        if (IsRoomEditor)
        {
            AddTab("General", "Room identity, map/room number, name, description, and basic room flags.", Basic);
            AddTab("Map", "Full visual map editor with draggable room nodes, route highlighting, map switching, auto-layout, room creation, and exact directional exit controls.",
                field => false, roomMap: true);
            AddTab("Environment", "Lighting, ambient spells, respawn timing, area caps, terrain, and other room environment controls.",
                field => Root(field, "environment", "attributes", "terrain", "area", "region"));
            AddTab("Contents", "NPC spawns, room items, services, shops, and other contents linked to this room.",
                field => Root(field, "spawns", "services", "inventory", "items", "npcs", "loot"));
            AddTab("Exits & Links", "Room connections, exits, doors, prerequisites, traps, and relationship links.",
                field => Root(field, "relationships", "exits", "doors", "prerequisites", "traps", "effects"));
            AddTab("Messages & Scripts", "Player-facing text, commands, scripts, triggers, and room event behavior.",
                field => Root(field, "messages", "message", "commands", "scripts", "script", "triggers", "events", "text"));
        }
        else if (IsItemEditor)
        {
            AddTab("General", "Item identity, type, descriptions, pick-up behavior, and basic flags.",
                field => Basic(field) || Starts(field, "public_reference_0139.fields.descriptions"));
            AddTab("Equipment", "Wear slot, strength/stat requirements, wearable behavior, and equipment properties.",
                field => Root(field, "equipment", "requirements", "prerequisites"));
            AddTab("Combat", "Weapon damage, armor bonuses, accuracy, speed, weapon/armor type, and combat behavior.",
                field => Root(field, "combat", "damage"));
            AddTab("Use & Effects", "Uses, activation effects, spells, abilities, open rewards, and consumption behavior.",
                field => Root(field, "use_effect", "effects", "effect", "abilities", "spells") ||
                         Starts(field, "public_reference_0139.fields.max_uses", "public_reference_0139.fields.uses_", "public_reference_0139.fields.retain_", "public_reference_0139.fields.open_rewards", "public_reference_0139.fields.abilities_named"));
            AddTab("Economy & Limits", "Cost, weight, currency type, game limits, and current-instance controls.",
                field => Root(field, "economy") || Starts(field, "public_reference_0139.fields.cost_type", "public_reference_0139.fields.game_limit", "public_reference_0139.fields.current_instances"));
            AddTab("Restrictions", "Class/race restrictions, negate properties, death/drop rules, and other restrictions.",
                field => Starts(field, "public_reference_0139.fields.class_restrictions", "public_reference_0139.fields.race_restrictions", "public_reference_0139.fields.stat_requirements", "public_reference_0139.fields.properties.negate", "public_reference_0139.fields.not_droppable", "public_reference_0139.fields.destroy_on_death") || Root(field, "restrictions"));
        }
        else if (IsNpcEditor)
        {
            AddTab("General", "Monster/NPC identity, level, descriptions, type, gender, and game-limit information.",
                field => Basic(field) || Starts(field, "public_reference_0139.fields.descriptions", "public_reference_0139.fields.monster_type", "public_reference_0139.fields.gender", "public_reference_0139.fields.game_limit"));
            AddTab("Combat", "Hit points, energy, armor, resistances, regeneration, and general combat stats.",
                field => Root(field, "combat") || Starts(field, "public_reference_0139.fields.hp_regen", "public_reference_0139.fields.backstab_defense"));
            AddTab("Attacks & Spells", "Edit each attack slot using normal fields. Spell lists and recovered attack-related settings stay on this tab.",
                field => Root(field, "attacks", "spells") || Starts(field, "public_reference_0139.fields.spells"),
                npcAttacks: true, suppressPaths: ["attacks"]);
            AddTab("Behavior", "Alignment, group/faction, follow/charm behavior, undead state, respawn, and movement/AI controls.",
                field => Root(field, "behavior", "ai", "movement"));
            AddTab("Rewards & Loot", "Experience, currency drops, loot, inventory, and death rewards.",
                field => Root(field, "rewards", "loot", "inventory"));
            AddTab("Abilities", "Edit named monster abilities without JSON. Formula links and other ability controls remain visible below.",
                field => Root(field, "abilities", "formula_refs"), npcAbilities: true, suppressPaths: ["abilities"]);
        }
        else if (IsSpellEditor)
        {
            AddTab("General", "Spell identity, level, type, short name, and core classification.",
                field => Basic(field) || Starts(field, "classification.spell_level", "classification.spell_type"));
            AddTab("Targeting", "Target type, attack type, resistance type, difficulty, and targeting rules.",
                field => Starts(field, "classification.target_type", "classification.attack_type", "public_reference_0139.fields.resist_type", "public_reference_0139.fields.resist_ability", "public_reference_0139.fields.difficulty") || Root(field, "targeting"));
            AddTab("Costs", "Mana, energy, requirements, and casting costs.",
                field => Root(field, "costs", "requirements") || Starts(field, "public_reference_0139.fields.magery_requirement"));
            AddTab("Effects & Scaling", "Minimum/maximum effect, duration, level scaling, caps, and formula links.",
                field => Root(field, "effect", "formula_refs") || Starts(field, "public_reference_0139.fields.level_cap", "public_reference_0139.fields.levels_for_", "public_reference_0139.fields.max_increase", "public_reference_0139.fields.min_increase", "public_reference_0139.fields.duration_increase"));
            AddTab("Messages & Abilities", "Descriptions, cast messages, message style, and abilities applied by the spell.",
                field => Root(field, "messages") || Starts(field, "public_reference_0139.fields.descriptions", "public_reference_0139.fields.message_style", "public_reference_0139.fields.abilities_named"));
        }
        else if (IsAbilityEditor)
        {
            AddTab("General", "Ability identity, legacy number, category, and basic creator settings.", Basic);
            AddTab("Description & Display", "Player-facing description plus recovered display color and implementation notes.",
                field => Starts(field, "public_reference_0139.fields.description", "public_reference_0139.fields.color_", "public_reference_0139.fields.impl_status", "public_reference_0139.fields.dll_notes") || Root(field, "description", "display", "color"));
            AddTab("Gameplay", "Ability values, modifiers, parameters, flags, and decoded effect behavior.",
                field => Root(field, "effect", "effects", "value", "values", "modifier", "modifiers", "parameters", "flags", "behavior"));
            AddTab("Relationships & Use", "Links to spells, items, statuses, scripts, quests, and other records that use or are used by this ability.",
                field => Root(field, "relationships", "spells", "items", "status_effects", "scripts", "quests", "skills", "formulas"));
        }
        else if (IsMessageEditor)
        {
            AddTab("Message Text", "Edit the player, secondary, and tertiary message slots using large normal text boxes.",
                field => Root(field, "player_payload", "secondary_payload", "tertiary_payload"));
            AddTab("Identity", "Message number and text encoding. Native physical file offsets are deliberately hidden from the creator surface.",
                field => Root(field, "message_id", "encoding"));
        }
        else if (IsTextblockEditor)
        {
            AddTab("Text", "Edit the complete textblock in one large text area. No JSON or escaped text is required.",
                field => Root(field, "payload"));
            AddTab("Identity", "Textblock number and encoding. Classic native storage bookkeeping remains protected.",
                field => Root(field, "textblock_id", "encoding"));
        }
        else if (IsActionEditor)
        {
            AddTab("General", "Action identity, action type, runtime disposition, and basic settings.",
                field => Basic(field) || Root(field, "interaction_kind", "runtime_disposition"));
            AddTab("Location & Commands", "Choose source/destination rooms, exit direction, and the commands or phrases that activate this action.",
                field => Root(field, "relationships", "commands", "accepted_commands", "verb_phrase", "direction", "from_room_id", "from_map_number", "from_room_number", "to_room_id", "to_map_number", "to_room_number") ||
                    Starts(field, "from_", "to_", "explicit_source_"));
            AddTab("Requirements", "Required items, levels, abilities, quest state, gates, and other prerequisites.",
                field => Root(field, "prerequisites", "requirements") || Starts(field, "command_semantics.required_", "command_semantics.minimum_", "command_semantics.maximum_"));
            AddTab("Outcomes & Messages", "Textblocks, messages, return rooms, rewards, damage, spells, and other outcomes produced by the action.",
                field => Root(field, "effects", "rewards", "outcomes", "messages", "text") || Starts(field, "command_semantics.success_", "command_semantics.failure_", "command_semantics.native_textblock", "command_semantics.return_room", "command_semantics.random_textblock"));
            AddTab("Execution", "Decoded operation order, flags, and advanced executable action controls.",
                field => Root(field, "operations", "command_semantics", "execution_semantics", "flags"));
        }
        else if (IsClassEditor)
        {
            AddTab("General", "Class identity, description, category, and playable classification.", Basic);
            AddTab("Stats & Growth", "Stat requirements, growth, hit points, mana, energy, and level progression.",
                field => Root(field, "stats", "attributes", "growth", "progression", "levels", "requirements"));
            AddTab("Combat & Magic", "Combat modifiers, weapon/armor rules, magic, resistances, and spell progression.",
                field => Root(field, "combat", "magic", "spells", "resistances", "equipment"));
            AddTab("Skills & Abilities", "Skills, abilities, training, and class-specific powers.",
                field => Root(field, "skills", "abilities", "training"));
            AddTab("Restrictions", "Race, alignment, equipment, guild, and other class restrictions.",
                field => Root(field, "restrictions", "prerequisites", "relationships", "flags"));
        }
        else if (IsRaceEditor)
        {
            AddTab("General", "Race identity, description, classification, and playable settings.", Basic);
            AddTab("Attributes", "Base stats, bonuses, penalties, regeneration, size, and movement.",
                field => Root(field, "stats", "attributes", "bonuses", "penalties", "regeneration", "movement"));
            AddTab("Combat & Resistance", "Combat modifiers, resistances, immunities, vulnerabilities, and special defenses.",
                field => Root(field, "combat", "resistances", "immunities", "vulnerabilities", "defenses"));
            AddTab("Abilities & Restrictions", "Innate abilities, class limits, equipment rules, and other racial restrictions.",
                field => Root(field, "abilities", "skills", "restrictions", "requirements", "equipment", "flags"));
        }
        else if (IsShopEditor)
        {
            AddTab("General", "Shop/service identity, service type, and basic settings.", Basic);
            AddTab("Location & Access", "Room placement, level range, class limits, and access rules.",
                field => Root(field, "room_ref", "level_range", "class_limit", "requirements", "restrictions", "relationships"));
            AddTab("Pricing & Rules", "Markup, pricing, service behavior, restock rules, and service-specific controls.",
                field => Root(field, "markup_percent", "pricing", "economy", "service_kind", "service_type", "decoded_shop_type"));
            AddTab("Inventory", "Add shop stock using normal fields for quantities and regeneration. No field=value rows are required.",
                field => Root(field, "inventory") || field.Path.Equals("@inventory_items", StringComparison.OrdinalIgnoreCase) || Starts(field, "public_reference_0139.fields.inventory"),
                shopInventory: true, suppressPaths: ["inventory", "@inventory_items", "public_reference_0139.fields.inventory"]);
            AddTab("Descriptions", "Shop text and player-facing descriptions.",
                field => Starts(field, "public_reference_0139.fields.descriptions") || Root(field, "descriptions", "messages"));
        }

        if (remaining.Count > 0)
        {
            DedicatedTabs.Add(new CustomWorldDedicatedTabRow(
                "Advanced Gameplay",
                "Less-common decoded gameplay fields that do not fit the main tabs. Librarium provenance/runtime bookkeeping is intentionally hidden from the normal creator editor.",
                remaining.OrderBy(field => field.Label, StringComparer.OrdinalIgnoreCase)));
        }

        SelectedDedicatedTab = DedicatedTabs.FirstOrDefault();
    }

    private void LoadNpcCollectionEditors()
    {
        NpcAttacks.Clear();
        NpcAbilities.Clear();
        SelectedNpcAttack = null;
        SelectedNpcAbility = null;
        _npcAttacksDirty = false;
        _npcAbilitiesDirty = false;
        if (!IsNpcEditor) return;

        var attacksField = SimpleFields.FirstOrDefault(field => field.Path.Equals("attacks", StringComparison.OrdinalIgnoreCase));
        if (attacksField is not null)
        {
            foreach (var line in SplitCollectionLines(attacksField.TextValue))
                AddLoadedNpcAttack(ParseNpcAttack(line));
        }
        if (NpcAttacks.Count == 0 && SimpleIsNew)
        {
            AddLoadedNpcAttack(new NpcAttackEditorRow
            {
                Slot = "1", Type = "1", MinimumDamage = "1", MaximumDamage = "4",
                SelectionPercent = "100", Accuracy = "0", HitSpellLegacyId = "0", EnergyCost = "1000"
            });
        }
        SelectedNpcAttack = NpcAttacks.FirstOrDefault();

        var abilitiesField = SimpleFields.FirstOrDefault(field => field.Path.Equals("abilities", StringComparison.OrdinalIgnoreCase));
        if (abilitiesField is not null)
        {
            foreach (var line in SplitCollectionLines(abilitiesField.TextValue))
                AddLoadedNpcAbility(ParseNpcAbility(line));
        }
        SelectedNpcAbility = NpcAbilities.FirstOrDefault();
        _npcAttacksDirty = false;
        _npcAbilitiesDirty = false;
    }

    private void AddLoadedNpcAttack(NpcAttackEditorRow row)
    {
        row.PropertyChanged += NpcAttackChanged;
        NpcAttacks.Add(row);
    }

    private void AddLoadedNpcAbility(NpcAbilityEditorRow row)
    {
        row.PropertyChanged += NpcAbilityChanged;
        NpcAbilities.Add(row);
    }

    private void NpcAttackChanged(object? sender, PropertyChangedEventArgs e) => _npcAttacksDirty = true;
    private void NpcAbilityChanged(object? sender, PropertyChangedEventArgs e) => _npcAbilitiesDirty = true;

    private void AddNpcAttack()
    {
        var nextSlot = NpcAttacks.Select(row => int.TryParse(row.Slot, out var value) ? value : 0).DefaultIfEmpty(0).Max() + 1;
        var row = new NpcAttackEditorRow
        {
            Slot = nextSlot.ToString(), Type = "1", MinimumDamage = "0", MaximumDamage = "0",
            SelectionPercent = NpcAttacks.Count == 0 ? "100" : "0", Accuracy = "0", HitSpellLegacyId = "0", EnergyCost = "1000"
        };
        AddLoadedNpcAttack(row);
        _npcAttacksDirty = true;
        SelectedNpcAttack = row;
    }

    private void RemoveNpcAttack()
    {
        if (SelectedNpcAttack is null) return;
        var index = NpcAttacks.IndexOf(SelectedNpcAttack);
        NpcAttacks.Remove(SelectedNpcAttack);
        _npcAttacksDirty = true;
        SelectedNpcAttack = NpcAttacks.Count == 0 ? null : NpcAttacks[Math.Clamp(index, 0, NpcAttacks.Count - 1)];
    }

    private void AddNpcAbility()
    {
        var row = new NpcAbilityEditorRow();
        AddLoadedNpcAbility(row);
        _npcAbilitiesDirty = true;
        SelectedNpcAbility = row;
    }

    private void RemoveNpcAbility()
    {
        if (SelectedNpcAbility is null) return;
        var index = NpcAbilities.IndexOf(SelectedNpcAbility);
        NpcAbilities.Remove(SelectedNpcAbility);
        _npcAbilitiesDirty = true;
        SelectedNpcAbility = NpcAbilities.Count == 0 ? null : NpcAbilities[Math.Clamp(index, 0, NpcAbilities.Count - 1)];
    }

    private void SyncNpcCollectionEditorsToFields()
    {
        if (!IsNpcEditor) return;
        if (SimpleIsNew || _npcAttacksDirty)
        {
            var field = SimpleFields.FirstOrDefault(item => item.Path.Equals("attacks", StringComparison.OrdinalIgnoreCase));
            if (field is not null) field.TextValue = string.Join(Environment.NewLine, NpcAttacks.Select(SerializeNpcAttack));
            var count = SimpleFields.FirstOrDefault(item => item.Path.Equals("combat.attack_count", StringComparison.OrdinalIgnoreCase));
            if (count is not null) count.TextValue = NpcAttacks.Count.ToString();
        }
        if (SimpleIsNew || _npcAbilitiesDirty)
        {
            var field = SimpleFields.FirstOrDefault(item => item.Path.Equals("abilities", StringComparison.OrdinalIgnoreCase));
            if (field is not null) field.TextValue = string.Join(Environment.NewLine, NpcAbilities.Select(SerializeNpcAbility));
        }
    }

    private void LoadRoomMapEditor()
    {
        RoomMapGridTiles.Clear();
        RoomMapVerticalLinks.Clear();
        _roomMapLinksDirty = false;
        if (!IsRoomEditor) return;

        IReadOnlyList<CustomWorldRoomMapLink> loaded = [];
        if (!SimpleIsNew && !string.IsNullOrWhiteSpace(SimpleObjectId))
        {
            try { loaded = _customService.LoadRoomMapLinks(SimpleObjectId); }
            catch { loaded = []; }
        }
        var byDirection = loaded.ToDictionary(link => link.Direction, StringComparer.OrdinalIgnoreCase);
        RoomMapTileRow Link(string direction)
        {
            byDirection.TryGetValue(direction, out var link);
            var row = new RoomMapTileRow(direction, link?.ExitId ?? string.Empty, link?.DestinationRoomId ?? string.Empty, link?.DestinationName ?? string.Empty);
            row.PropertyChanged += RoomMapLinkChanged;
            return row;
        }

        RoomMapGridTiles.Add(Link("NW"));
        RoomMapGridTiles.Add(Link("N"));
        RoomMapGridTiles.Add(Link("NE"));
        RoomMapGridTiles.Add(Link("W"));
        var roomName = SimpleFields.FirstOrDefault(field => field.Path.Equals("name", StringComparison.OrdinalIgnoreCase))?.TextValue ?? SimpleFormTitle;
        RoomMapGridTiles.Add(new RoomMapTileRow(string.Empty, string.Empty, SimpleObjectId, roomName, isCenter: true));
        RoomMapGridTiles.Add(Link("E"));
        RoomMapGridTiles.Add(Link("SW"));
        RoomMapGridTiles.Add(Link("S"));
        RoomMapGridTiles.Add(Link("SE"));
        RoomMapVerticalLinks.Add(Link("U"));
        RoomMapVerticalLinks.Add(Link("D"));
    }

    private void RoomMapLinkChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomMapTileRow.DestinationRoomId)) _roomMapLinksDirty = true;
    }

    private void LoadVisualMapEditor(int? requestedMapNumber = null)
    {
        RoomVisualMapNodes.Clear();
        RoomVisualMapEdges.Clear();
        VisualMapNumbers.Clear();
        SelectedVisualMapNode = null;
        VisualMapLayoutDirty = false;
        Raise(nameof(VisualMapRoomCount));
        Raise(nameof(VisualMapEdgeCount));
        Raise(nameof(VisualMapCanvasWidth));
        Raise(nameof(VisualMapCanvasHeight));
        if (!IsRoomEditor)
        {
            VisualMapStatus = "The visual map is available from the Room / Map editor.";
            VisualMapTrailText = "Select Rooms from the creator navigation to use the map.";
            return;
        }

        _loadingVisualMap = true;
        try
        {
            var requested = requestedMapNumber.GetValueOrDefault();
            if (requested <= 0)
            {
                var mapField = SimpleFields.FirstOrDefault(field => field.Path.Equals("map_number", StringComparison.OrdinalIgnoreCase));
                if (mapField is not null && int.TryParse(mapField.TextValue, out var parsed) && parsed > 0) requested = parsed;
            }

            var snapshot = _customService.LoadVisualMap(SimpleIsNew ? null : SimpleObjectId, requested > 0 ? requested : null);
            foreach (var map in snapshot.AvailableMaps) VisualMapNumbers.Add(map);
            if (!VisualMapNumbers.Contains(snapshot.MapNumber)) VisualMapNumbers.Add(snapshot.MapNumber);
            _selectedVisualMapNumber = snapshot.MapNumber;
            Raise(nameof(SelectedVisualMapNumber));

            var nodes = new Dictionary<string, RoomVisualMapNodeRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var room in snapshot.Rooms)
            {
                var node = new RoomVisualMapNodeRow(room)
                {
                    IsCurrent = !SimpleIsNew && room.RoomId.Equals(SimpleObjectId, StringComparison.OrdinalIgnoreCase)
                };
                node.PropertyChanged += VisualMapNodeChanged;
                RoomVisualMapNodes.Add(node);
                nodes[node.RoomId] = node;
            }

            foreach (var edge in snapshot.Edges)
            {
                if (!nodes.TryGetValue(edge.FromRoomId, out var from) || !nodes.TryGetValue(edge.ToRoomId, out var to)) continue;
                RoomVisualMapEdges.Add(new RoomVisualMapEdgeRow(edge, from, to));
            }

            Raise(nameof(VisualMapRoomCount));
            Raise(nameof(VisualMapEdgeCount));
            Raise(nameof(VisualMapCanvasWidth));
            Raise(nameof(VisualMapCanvasHeight));
            var current = RoomVisualMapNodes.FirstOrDefault(node => node.IsCurrent) ?? RoomVisualMapNodes.FirstOrDefault();
            if (current is not null) SelectVisualMapRoom(current);
            else VisualMapTrailText = "This map has no rooms yet. Use + New Room on This Map to start it.";
            VisualMapStatus = $"Map {snapshot.MapNumber}: {RoomVisualMapNodes.Count} room(s), {RoomVisualMapEdges.Count} visible directional link(s). Drag rooms to arrange them; double-click one to edit it.";
        }
        catch (Exception exception)
        {
            VisualMapStatus = "MAP FAILED: " + exception.Message;
            VisualMapTrailText = "The normal room editor is still available.";
        }
        finally
        {
            _loadingVisualMap = false;
            (SaveVisualMapLayoutCommand as RelayCommand)?.Raise();
            (AutoArrangeVisualMapCommand as RelayCommand)?.Raise();
        }
    }

    private void VisualMapNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RoomVisualMapNodeRow.X) or nameof(RoomVisualMapNodeRow.Y))
        {
            VisualMapLayoutDirty = true;
            Raise(nameof(VisualMapCanvasWidth));
            Raise(nameof(VisualMapCanvasHeight));
        }
    }

    public void SelectVisualMapRoom(RoomVisualMapNodeRow node)
    {
        if (!RoomVisualMapNodes.Contains(node)) return;
        foreach (var room in RoomVisualMapNodes)
        {
            room.IsSelected = ReferenceEquals(room, node);
            room.IsTrail = false;
        }
        foreach (var edge in RoomVisualMapEdges) edge.IsTrail = false;
        SelectedVisualMapNode = node;

        var start = RoomVisualMapNodes.FirstOrDefault(room => room.IsCurrent);
        if (start is null || ReferenceEquals(start, node))
        {
            node.IsTrail = true;
            VisualMapTrailText = ReferenceEquals(start, node)
                ? $"Current room: {node.DisplayName}. Drag to position it or double-click another room to edit."
                : $"Selected {node.DisplayName}. Double-click to edit.";
            return;
        }

        var previous = new Dictionary<string, RoomVisualMapEdgeRow>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start.RoomId };
        var queue = new Queue<string>();
        queue.Enqueue(start.RoomId);
        while (queue.Count > 0 && !seen.Contains(node.RoomId))
        {
            var currentId = queue.Dequeue();
            foreach (var edge in RoomVisualMapEdges.Where(edge => edge.FromRoomId.Equals(currentId, StringComparison.OrdinalIgnoreCase)))
            {
                if (!seen.Add(edge.ToRoomId)) continue;
                previous[edge.ToRoomId] = edge;
                queue.Enqueue(edge.ToRoomId);
            }
        }

        if (!seen.Contains(node.RoomId))
        {
            node.IsTrail = true;
            VisualMapTrailText = $"Selected {node.DisplayName}. No directed route from the current room was found on this map.";
            return;
        }

        var pathEdges = new List<RoomVisualMapEdgeRow>();
        var cursor = node.RoomId;
        while (!cursor.Equals(start.RoomId, StringComparison.OrdinalIgnoreCase) && previous.TryGetValue(cursor, out var edge))
        {
            pathEdges.Add(edge);
            cursor = edge.FromRoomId;
        }
        pathEdges.Reverse();
        start.IsTrail = true;
        foreach (var edge in pathEdges)
        {
            edge.IsTrail = true;
            edge.To.IsTrail = true;
        }
        var directions = string.Join(" → ", pathEdges.Select(edge => string.IsNullOrWhiteSpace(edge.Direction) ? "?" : edge.Direction));
        VisualMapTrailText = $"Trail: {start.DisplayName} → {node.DisplayName}" + (directions.Length > 0 ? $"  ({directions})" : string.Empty);
    }

    public void OpenVisualMapRoom(RoomVisualMapNodeRow node)
    {
        if (!IsRoomEditor || node is null) return;
        var item = SimpleObjects.FirstOrDefault(row => row.Id.Equals(node.RoomId, StringComparison.OrdinalIgnoreCase))
                   ?? new CustomWorldObjectRow(new CustomWorldObjectSummary("rooms", node.RoomId, node.Name));
        _selectedSimpleObject = item;
        Raise(nameof(SelectedSimpleObject));
        LoadSimpleObject(item);
        CustomStatus = $"Opened room {node.RoomId} from the visual map.";
    }

    private void SaveVisualMapLayout()
    {
        try
        {
            _customService.SaveVisualMapLayout(SelectedVisualMapNumber,
                RoomVisualMapNodes.Select(node => new CustomWorldVisualMapPosition(node.RoomId, node.MapNumber, node.X, node.Y)));
            VisualMapLayoutDirty = false;
            VisualMapStatus = $"Saved the visual layout for map {SelectedVisualMapNumber}. Room gameplay data was not changed.";
        }
        catch (Exception exception)
        {
            VisualMapStatus = "MAP LAYOUT FAILED: " + exception.Message;
        }
    }

    private void AutoArrangeVisualMap()
    {
        try
        {
            var map = SelectedVisualMapNumber;
            _customService.ResetVisualMapLayout(map);
            LoadVisualMapEditor(map);
            VisualMapLayoutDirty = true;
            VisualMapStatus = $"Auto-arranged map {map}. Review the layout, then click Save Layout to keep it.";
        }
        catch (Exception exception)
        {
            VisualMapStatus = "AUTO ARRANGE FAILED: " + exception.Message;
        }
    }

    private void NewRoomFromMap()
    {
        var map = SelectedVisualMapNumber <= 0 ? 1 : SelectedVisualMapNumber;
        StartNewSimpleObject();
        var mapField = SimpleFields.FirstOrDefault(field => field.Path.Equals("map_number", StringComparison.OrdinalIgnoreCase));
        if (mapField is not null) mapField.TextValue = map.ToString();
        var roomNumberField = SimpleFields.FirstOrDefault(field => field.Path.Equals("room_number", StringComparison.OrdinalIgnoreCase));
        if (roomNumberField is not null) roomNumberField.TextValue = _customService.GetNextRoomNumber(map).ToString();
        LoadVisualMapEditor(map);
        SelectedDedicatedTab = DedicatedTabs.FirstOrDefault();
        CustomStatus = $"New room prepared on map {map}. Give it a name/description and click Save & Build; Reforged assigned the next room number automatically.";
    }

    private void LoadShopInventoryEditor()
    {
        ShopInventory.Clear();
        SelectedShopInventory = null;
        _shopInventoryDirty = false;
        if (!IsShopEditor || SimpleIsNew || string.IsNullOrWhiteSpace(SimpleObjectId)) return;
        try
        {
            foreach (var item in _customService.LoadShopInventory(SimpleObjectId))
                AddLoadedShopInventory(new ShopInventoryEditorRow(item.ItemRef, item.MaxQty.ToString(), item.NormalQty.ToString(), item.RegenQty.ToString(), item.RegenTime.ToString(), item.RegenPercent.ToString()));
        }
        catch { }
        SelectedShopInventory = ShopInventory.FirstOrDefault();
    }

    private void AddLoadedShopInventory(ShopInventoryEditorRow row)
    {
        row.PropertyChanged += ShopInventoryChanged;
        ShopInventory.Add(row);
    }

    private void ShopInventoryChanged(object? sender, PropertyChangedEventArgs e) => _shopInventoryDirty = true;

    private void AddShopInventory()
    {
        var row = new ShopInventoryEditorRow();
        AddLoadedShopInventory(row);
        _shopInventoryDirty = true;
        SelectedShopInventory = row;
    }

    private void RemoveShopInventory()
    {
        if (SelectedShopInventory is null) return;
        var index = ShopInventory.IndexOf(SelectedShopInventory);
        ShopInventory.Remove(SelectedShopInventory);
        _shopInventoryDirty = true;
        SelectedShopInventory = ShopInventory.Count == 0 ? null : ShopInventory[Math.Clamp(index, 0, ShopInventory.Count - 1)];
    }

    private IEnumerable<CustomWorldRoomMapLink> RoomMapLinksForSave()
        => RoomMapGridTiles.Concat(RoomMapVerticalLinks)
            .Where(row => !row.IsCenter)
            .Select(row => new CustomWorldRoomMapLink(row.Direction, row.ExitId, row.DestinationRoomId, row.DestinationName));

    private IEnumerable<CustomWorldShopInventoryEntry> ShopInventoryForSave()
    {
        static int Number(string value, int fallback = 0) => int.TryParse(value, out var parsed) ? parsed : fallback;
        return ShopInventory.Select(row => new CustomWorldShopInventoryEntry(
            row.ItemRef,
            Number(row.MaxQty, 5),
            Number(row.NormalQty, 5),
            Number(row.RegenQty, 1),
            Number(row.RegenTime, 48),
            Number(row.RegenPercent, 25)));
    }

    private static NpcAttackEditorRow ParseNpcAttack(string line)
    {
        var values = ParseCollectionLine(line);
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "slot", "type", "minimum_damage", "maximum_damage", "selection_percent", "accuracy",
            "hit_spell_legacy_id", "energy_cost", "hit_message_legacy_id", "miss_message_legacy_id", "dodge_message_legacy_id"
        };
        return new NpcAttackEditorRow
        {
            Slot = Read(values, "slot", "1"),
            Type = Read(values, "type", "1"),
            MinimumDamage = Read(values, "minimum_damage", "0"),
            MaximumDamage = Read(values, "maximum_damage", "0"),
            SelectionPercent = Read(values, "selection_percent", "0"),
            Accuracy = Read(values, "accuracy", "0"),
            HitSpellLegacyId = Read(values, "hit_spell_legacy_id", "0"),
            EnergyCost = Read(values, "energy_cost", "0"),
            HitMessageLegacyId = Read(values, "hit_message_legacy_id", "0"),
            MissMessageLegacyId = Read(values, "miss_message_legacy_id", "0"),
            DodgeMessageLegacyId = Read(values, "dodge_message_legacy_id", "0"),
            ExtraFields = string.Join(" | ", values.Where(pair => !known.Contains(pair.Key)).Select(pair => pair.Key + "=" + pair.Value))
        };
    }

    private static NpcAbilityEditorRow ParseNpcAbility(string line)
    {
        var values = ParseCollectionLine(line);
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ability", "value", "ability_id" };
        return new NpcAbilityEditorRow
        {
            Ability = Read(values, "ability", "Ability"),
            Value = Read(values, "value", "0"),
            AbilityId = Read(values, "ability_id", "0"),
            ExtraFields = string.Join(" | ", values.Where(pair => !known.Contains(pair.Key)).Select(pair => pair.Key + "=" + pair.Value))
        };
    }

    private static string SerializeNpcAttack(NpcAttackEditorRow row)
    {
        var parts = new List<string>
        {
            $"slot={row.Slot}", $"type={row.Type}", $"minimum_damage={row.MinimumDamage}", $"maximum_damage={row.MaximumDamage}",
            $"selection_percent={row.SelectionPercent}", $"accuracy={row.Accuracy}", $"hit_spell_legacy_id={row.HitSpellLegacyId}",
            $"energy_cost={row.EnergyCost}", $"hit_message_legacy_id={row.HitMessageLegacyId}",
            $"miss_message_legacy_id={row.MissMessageLegacyId}", $"dodge_message_legacy_id={row.DodgeMessageLegacyId}"
        };
        if (!string.IsNullOrWhiteSpace(row.ExtraFields)) parts.Add(row.ExtraFields.Trim());
        return string.Join(" | ", parts);
    }

    private static string SerializeNpcAbility(NpcAbilityEditorRow row)
    {
        var parts = new List<string> { $"ability={row.Ability}", $"value={row.Value}", $"ability_id={row.AbilityId}" };
        if (!string.IsNullOrWhiteSpace(row.ExtraFields)) parts.Add(row.ExtraFields.Trim());
        return string.Join(" | ", parts);
    }

    private static Dictionary<string, string> ParseCollectionLine(string line)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = token.IndexOf('=');
            if (split <= 0) continue;
            result[token[..split].Trim()] = token[(split + 1)..].Trim();
        }
        return result;
    }

    private static string Read(IReadOnlyDictionary<string, string> values, string key, string fallback)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static string[] SplitCollectionLines(string raw) => (raw ?? string.Empty)
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string SectionForPath(string path)
    {
        if (path.StartsWith('@')) return "Collections";
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "Basics";
        var key = parts[0].ToLowerInvariant();
        if (parts.Length == 1 && key is "name" or "title" or "display_name" or "short_name" or "legacy_id" or
            "description" or "definition" or "category" or "type" or "classification" or "status") return "Basics";
        var root = parts[0].Replace('_', ' ');
        return string.Join(" ", root.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static int SectionOrder(string section) => section.ToLowerInvariant() switch
    {
        "basics" => 0,
        "classification" => 5,
        "combat" => 10,
        "effect" or "effects" => 15,
        "equipment" or "requirements" or "prerequisites" => 20,
        "behavior" or "ai" or "movement" => 30,
        "environment" or "spawn model" => 40,
        "economy" or "rewards" or "inventory" or "loot" => 50,
        "relationships" or "commands" or "aliases" => 60,
        "flags" => 70,
        "collections" => 80,
        _ => 100
    };

    private static string SectionDescription(string section) => section switch
    {
        "Basics" => "Names, descriptions, identity, and other top-level creator settings.",
        "Combat" => "Damage, defenses, accuracy, speed, attacks, resistances, and combat behavior.",
        "Equipment" => "Wear slots, requirements, bonuses, and equipment behavior.",
        "Behavior" => "AI, alignment, movement, regeneration, and behavior controls.",
        "Environment" => "Room/world environment, lighting, delays, spells, and area settings.",
        "Economy" => "Cost, value, currency, weight, limits, and economy controls.",
        "Rewards" => "Experience, money, drops, and reward settings.",
        "Inventory" => "Detailed inventory rows and stock behavior.",
        "Relationships" => "Links to rooms, objects, scripts, quests, and other records.",
        "Collections" => "Detailed list rows. Add or remove lines to add or remove collection entries.",
        _ => $"Full creator control for {section.ToLowerInvariant()} fields."
    };

    private async Task SaveSimpleObjectAsync()
    {
        try
        {
            SyncNpcCollectionEditorsToFields();
            var textValues = SimpleFields.Where(field => field.Kind != CustomWorldSimpleFieldKind.Boolean)
                .ToDictionary(field => field.Path, field => field.TextValue, StringComparer.OrdinalIgnoreCase);
            var boolValues = SimpleFields.Where(field => field.Kind == CustomWorldSimpleFieldKind.Boolean)
                .ToDictionary(field => field.Path, field => field.BoolValue, StringComparer.OrdinalIgnoreCase);
            var changedPaths = SimpleFields.Where(field => field.IsDirty).Select(field => field.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!SimpleIsNew && changedPaths.Count == 0 && !_roomMapLinksDirty && !_shopInventoryDirty && !VisualMapLayoutDirty)
            {
                CustomStatus = "No fields were changed.";
                return;
            }
            CustomStatus = "Saving and rebuilding Custom1...";
            var wasNew = SimpleIsNew;
            string savedId;
            if (!wasNew && changedPaths.Count == 0)
            {
                savedId = SimpleObjectId;
            }
            else
            {
                var saved = _customService.SaveSimpleRecord(SimpleFamily, SimpleObjectId, SimpleIsNew, textValues, boolValues, changedPaths);
                savedId = saved.TargetId;
            }
            if (IsRoomEditor && (wasNew || _roomMapLinksDirty))
                _customService.SaveRoomMapLinks(savedId, RoomMapLinksForSave());
            if (IsRoomEditor && VisualMapLayoutDirty && RoomVisualMapNodes.Count > 0)
                _customService.SaveVisualMapLayout(SelectedVisualMapNumber,
                    RoomVisualMapNodes.Select(node => new CustomWorldVisualMapPosition(node.RoomId, node.MapNumber, node.X, node.Y)));
            if (IsShopEditor && (wasNew || _shopInventoryDirty))
                _customService.SaveShopInventory(savedId, ShopInventoryForSave());
            var build = await Task.Run(_customService.Build);
            SimpleObjectId = savedId;
            SimpleIsNew = false;
            CustomGeneratedState = "Runtime data ready";
            CustomGeneratedDataPath = build.OutputRoot;
            LoadCustom();
            BrowseSimpleObjects();
            var selected = SimpleObjects.FirstOrDefault(item => item.Id.Equals(savedId, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                _selectedSimpleObject = selected;
                Raise(nameof(SelectedSimpleObject));
                LoadSimpleObject(selected);
            }
            var warnings = build.Diagnostics.Count(line => line.StartsWith("Warning", StringComparison.OrdinalIgnoreCase));
            CustomStatus = $"Saved {SimpleFamily}/{savedId} and rebuilt Custom1. No JSON entry was required. Warnings: {warnings}.";
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
    }

    private void DiscardSimpleObject()
    {
        if (!SimpleIsNew && _selectedSimpleObject is not null)
        {
            LoadSimpleObject(_selectedSimpleObject);
            CustomStatus = $"Discarded unsaved changes to {SimpleFamily}/{SimpleObjectId}.";
            return;
        }

        StartNewSimpleObject();
        CustomStatus = "Discarded the unsaved new object and opened a fresh form.";
    }

    private void DeleteSimpleObject()
    {
        if (string.IsNullOrWhiteSpace(SimpleObjectId)) return;
        try
        {
            var id = SimpleObjectId;
            _customService.DeleteSimpleRecord(SimpleFamily, id);
            LoadCustom();
            BrowseSimpleObjects();
            StartNewSimpleObject();
            CustomStatus = $"Removed {SimpleFamily}/{id} from Custom1. Classic was not changed.";
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
    }

    private void BrowseObjects()
    {
        try
        {
            var results = _customService.BrowseObjects(OverrideFamily, ObjectSearch);
            BaseObjects.Clear();
            foreach (var item in results) BaseObjects.Add(new CustomWorldObjectRow(item));
            SelectedBaseObject = null;
            BaseRecordJson = results.Count == 0
                ? "No matching objects found."
                : $"{results.Count} object(s) loaded. Select one to inspect its effective Custom1 record.";
            CustomStatus = $"Browse loaded {results.Count} {OverrideFamily} object(s). Search is capped at 500 results.";
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
    }

    private void SaveOverride()
    {
        try
        {
            _customService.SaveOverride(OverrideFamily, OverrideTargetId, OverrideOperation, OverrideFieldsJson);
            LoadCustom();
            SelectedOverride = CustomOverrides.FirstOrDefault(item =>
                item.Family.Equals(OverrideFamily, StringComparison.OrdinalIgnoreCase) &&
                item.TargetId.Equals(OverrideTargetId, StringComparison.OrdinalIgnoreCase));
            CustomStatus = $"Saved {OverrideFamily}/{OverrideTargetId}. Rebuild Custom1 to test the change.";
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
    }

    private void RemoveSelectedOverride()
    {
        if (SelectedOverride is null) return;
        try
        {
            var family = SelectedOverride.Family;
            var targetId = SelectedOverride.TargetId;
            _customService.RemoveOverride(family, targetId);
            LoadCustom();
            NewOverride();
            CustomStatus = $"Removed override {family}/{targetId}. Rebuild Custom1 to test the base Classic value again.";
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
    }

    private void NewOverride()
    {
        SelectedOverride = null;
        OverrideFamily = "rooms";
        OverrideTargetId = string.Empty;
        OverrideOperation = "patch";
        OverrideFieldsJson = "{\n  \"name\": \"\"\n}";
    }

    private async Task BuildCustomWorldAsync()
    {
        try
        {
            CustomStatus = "Building Custom1 from Librarium + world overrides...";
            var result = await Task.Run(_customService.Build);
            CustomGeneratedState = "Runtime data ready";
            CustomGeneratedDataPath = result.OutputRoot;
            var warnings = result.Diagnostics.Count(line => line.StartsWith("Warning", StringComparison.OrdinalIgnoreCase));
            CustomStatus = $"Built Custom1: {result.Families:N0} families, {result.Objects:N0} objects, {result.Files:N0} files. Warnings: {warnings}. Host can now load/test this world.";
        }
        catch (Exception exception)
        {
            CustomGeneratedState = "Build failed";
            CustomStatus = "FAILED: " + exception.Message;
        }
    }
}
