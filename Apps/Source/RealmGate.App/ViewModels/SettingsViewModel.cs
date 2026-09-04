using System.Windows.Input; using RealmGate.App.Commands; using RealmGate.App.Models; using RealmGate.App.Services;
namespace RealmGate.App.ViewModels;
public sealed class SettingsViewModel(IApplicationSettingsService service) : ViewModelBase { public ApplicationSettings Settings=>service.Current; public ICommand SaveCommand{get;}=new AsyncRelayCommand(service.SaveAsync); }
