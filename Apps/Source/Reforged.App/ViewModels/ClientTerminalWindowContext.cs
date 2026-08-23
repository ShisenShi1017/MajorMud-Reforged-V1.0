using System.ComponentModel;
using Reforged.App.Models;

namespace Reforged.App.ViewModels;

/// <summary>
/// Pins one terminal window to one live session. The shared PlayViewModel remains
/// available for client services, while command input and session selection stay
/// local to this window.
/// </summary>
public sealed class ClientTerminalWindowContext : ViewModelBase, IDisposable
{
    private string _terminalCommand = string.Empty;

    public ClientTerminalWindowContext(
        PlayViewModel host,
        ClientSessionConnection session)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Session.PropertyChanged += SessionPropertyChanged;
    }

    public PlayViewModel Host { get; }
    public ClientSessionConnection Session { get; }
    public IReadOnlyList<ClientTerminalViewMode> TerminalViewModes => Host.TerminalViewModes;

    public string TerminalCommand
    {
        get => _terminalCommand;
        set => Set(ref _terminalCommand, value ?? string.Empty);
    }

    public string WindowTitle
    {
        get
        {
            var character = string.IsNullOrWhiteSpace(Session.CharacterName)
                ? Session.DisplayName
                : Session.CharacterName;
            return $"MajorMUD Reforged Client — {character} — {Session.ServerProfileName} / {Session.WorldName}";
        }
    }

    public void ClearCommand() => TerminalCommand = string.Empty;

    public void Dispose() => Session.PropertyChanged -= SessionPropertyChanged;

    private void SessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClientSessionConnection.DisplayName)
            or nameof(ClientSessionConnection.CharacterName)
            or nameof(ClientSessionConnection.ServerProfileName)
            or nameof(ClientSessionConnection.WorldName))
        {
            Raise(nameof(WindowTitle));
        }
    }
}
