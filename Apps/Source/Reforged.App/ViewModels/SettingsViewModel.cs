using System.Windows.Input; using Reforged.App.Commands; using Reforged.App.Models; using Reforged.App.Services;
namespace Reforged.App.ViewModels;
public sealed class SettingsViewModel(IApplicationSettingsService service) : ViewModelBase { public ApplicationSettings Settings=>service.Current; public ICommand SaveCommand{get;}=new AsyncRelayCommand(service.SaveAsync); }
