using System.Collections.ObjectModel;
using System.Windows.Input;
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
    public bool IsText => Kind is CustomWorldSimpleFieldKind.Text or CustomWorldSimpleFieldKind.Multiline or CustomWorldSimpleFieldKind.Integer or CustomWorldSimpleFieldKind.Lines;
    public bool IsChoice => Kind == CustomWorldSimpleFieldKind.Choice;
    public bool IsBoolean => Kind == CustomWorldSimpleFieldKind.Boolean;
    public bool IsRequired => Definition.Required;
    public double InputHeight => Kind is CustomWorldSimpleFieldKind.Multiline or CustomWorldSimpleFieldKind.Lines ? 72 : 30;
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
}

public sealed class EditorViewModel : ViewModelBase
{
    private readonly ClassicAdministrationEditorService _classicService;
    private readonly CustomWorldEditorService _customService;
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

    public EditorViewModel(
        ClassicAdministrationEditorService classicService,
        CustomWorldEditorService customService)
    {
        _classicService = classicService;
        _customService = customService;
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
        SaveSimpleObjectCommand = new AsyncRelayCommand(SaveSimpleObjectAsync);
        DeleteSimpleObjectCommand = new RelayCommand(DeleteSimpleObject, () => !string.IsNullOrWhiteSpace(SimpleObjectId));
        StartNewSimpleObject();
    }

    public ObservableCollection<ClassicCharacterAccessRow> Characters { get; } = [];
    public ObservableCollection<CustomWorldOverrideRow> CustomOverrides { get; } = [];
    public ObservableCollection<CustomWorldObjectRow> BaseObjects { get; } = [];
    public ObservableCollection<CustomWorldObjectRow> SimpleObjects { get; } = [];
    public ObservableCollection<CustomWorldSimpleFieldRow> SimpleFields { get; } = [];
    public ObservableCollection<CustomWorldSimpleFieldRow> SimpleRequiredFields { get; } = [];
    public ObservableCollection<CustomWorldSimpleFieldRow> SimpleOptionalFields { get; } = [];
    public IReadOnlyList<CustomWorldSimpleFamilyOption> SimpleFamilies { get; } = CustomWorldSimpleFormCatalog.Families;
    public IReadOnlyList<string> OverrideFamilies { get; } =
    [
        "rooms", "exits", "npcs", "spawns", "items", "spells", "abilities", "classes", "races",
        "shops", "quests", "triggers", "scripts", "skills", "formulas", "commands"
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
    public bool SimpleIsNew { get => _simpleIsNew; private set => Set(ref _simpleIsNew, value); }
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
    public ICommand SaveSimpleObjectCommand { get; }
    public ICommand DeleteSimpleObjectCommand { get; }

    public void Load()
    {
        LoadClassic();
        LoadCustom();
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
            SimpleFormHelp = form.HelpText + " Fields marked * are required. Object ID is optional and will be generated for you.";
            SimpleFields.Clear();
            SimpleRequiredFields.Clear();
            SimpleOptionalFields.Clear();
            foreach (var field in form.Fields)
            {
                var row = new CustomWorldSimpleFieldRow(field);
                SimpleFields.Add(row);
                if (field.Required) SimpleRequiredFields.Add(row);
                else SimpleOptionalFields.Add(row);
            }
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
            var form = _customService.GetSimpleForm(item.Family);
            var loaded = _customService.LoadSimpleRecord(item.Family, item.Id);
            SimpleObjectId = item.Id;
            SimpleIsNew = false;
            SimpleFormTitle = $"Edit {form.SingularName}: {(string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name)}";
            SimpleFormHelp = form.HelpText + " Change the fields you want and click Save.";
            SimpleFields.Clear();
            SimpleRequiredFields.Clear();
            SimpleOptionalFields.Clear();
            foreach (var field in form.Fields)
            {
                loaded.TextValues.TryGetValue(field.Path, out var text);
                loaded.BoolValues.TryGetValue(field.Path, out var boolean);
                var row = new CustomWorldSimpleFieldRow(field, text, field.Kind == CustomWorldSimpleFieldKind.Boolean ? boolean : null);
                SimpleFields.Add(row);
                if (field.Required) SimpleRequiredFields.Add(row);
                else SimpleOptionalFields.Add(row);
            }
            CustomStatus = $"Loaded {item.Family}/{item.Id} into the simple editor.";
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
    }

    private async Task SaveSimpleObjectAsync()
    {
        try
        {
            var textValues = SimpleFields.Where(field => field.Kind != CustomWorldSimpleFieldKind.Boolean)
                .ToDictionary(field => field.Path, field => field.TextValue, StringComparer.OrdinalIgnoreCase);
            var boolValues = SimpleFields.Where(field => field.Kind == CustomWorldSimpleFieldKind.Boolean)
                .ToDictionary(field => field.Path, field => field.BoolValue, StringComparer.OrdinalIgnoreCase);
            var changedPaths = SimpleFields.Where(field => field.IsDirty).Select(field => field.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!SimpleIsNew && changedPaths.Count == 0)
            {
                CustomStatus = "No fields were changed.";
                return;
            }
            CustomStatus = "Saving and rebuilding Custom1...";
            var saved = _customService.SaveSimpleRecord(SimpleFamily, SimpleObjectId, SimpleIsNew, textValues, boolValues, changedPaths);
            var build = await Task.Run(_customService.Build);
            SimpleObjectId = saved.TargetId;
            SimpleIsNew = false;
            CustomGeneratedState = "Runtime data ready";
            CustomGeneratedDataPath = build.OutputRoot;
            LoadCustom();
            BrowseSimpleObjects();
            var selected = SimpleObjects.FirstOrDefault(item => item.Id.Equals(saved.TargetId, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                _selectedSimpleObject = selected;
                Raise(nameof(SelectedSimpleObject));
                LoadSimpleObject(selected);
            }
            var warnings = build.Diagnostics.Count(line => line.StartsWith("Warning", StringComparison.OrdinalIgnoreCase));
            CustomStatus = $"Saved {SimpleFamily}/{saved.TargetId} and rebuilt Custom1. No JSON entry was required. Warnings: {warnings}.";
        }
        catch (Exception exception)
        {
            CustomStatus = "FAILED: " + exception.Message;
        }
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
