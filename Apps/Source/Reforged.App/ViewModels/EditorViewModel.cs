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

public sealed class EditorViewModel : ViewModelBase
{
    private readonly ClassicAdministrationEditorService _service;
    private bool _unlimitedItemsEnabled = true;
    private bool _everyoneCanUseSysGoto;
    private string _status = "Loading Classic settings...";
    private string _projectRoot = string.Empty;
    private string _policyPath = string.Empty;

    public EditorViewModel(ClassicAdministrationEditorService service)
    {
        _service = service;
        SaveCommand = new RelayCommand(Save);
        ReloadCommand = new RelayCommand(Load);
    }

    public ObservableCollection<ClassicCharacterAccessRow> Characters { get; } = [];
    public bool UnlimitedItemsEnabled { get => _unlimitedItemsEnabled; set => Set(ref _unlimitedItemsEnabled, value); }
    public bool EveryoneCanUseSysGoto { get => _everyoneCanUseSysGoto; set => Set(ref _everyoneCanUseSysGoto, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string ProjectRoot { get => _projectRoot; private set => Set(ref _projectRoot, value); }
    public string PolicyPath { get => _policyPath; private set => Set(ref _policyPath, value); }
    public ICommand SaveCommand { get; }
    public ICommand ReloadCommand { get; }

    public void Load()
    {
        try
        {
            var snapshot = _service.Load();
            ProjectRoot = snapshot.ProjectRoot;
            PolicyPath = snapshot.PolicyPath;
            ApplyPolicy(snapshot.Policy);

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
            Status = $"Loaded and verified Classic settings. {Characters.Count} character(s) available.{warning}";
        }
        catch (Exception exception)
        {
            Status = "FAILED: " + exception.Message;
        }
    }

    private void Save()
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

            var result = _service.Save(policy);
            ApplyPolicy(result.Policy);
            PolicyPath = result.PolicyPath;
            Status = $"Saved and read-back verified at {DateTime.Now:g}. {result.Policy.SysopCharacterIds.Count} character SYSOP grant(s).";
        }
        catch (Exception exception)
        {
            Status = "FAILED: " + exception.Message;
        }
    }

    private void ApplyPolicy(RuntimeClassicAdministrationPolicy policy)
    {
        UnlimitedItemsEnabled = policy.UnlimitedItemsEnabled;
        EveryoneCanUseSysGoto = policy.EveryoneCanUseSysGoto;

        foreach (var character in Characters)
        {
            character.HasSysopAccess = policy.HasCharacterSysopAccess(character.CharacterId);
        }
    }
}
