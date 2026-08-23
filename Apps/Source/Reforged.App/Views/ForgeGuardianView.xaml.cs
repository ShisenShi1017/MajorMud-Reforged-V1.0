using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

public partial class ForgeGuardianView : UserControl
{
    private INotifyCollectionChanged? _messages;

    public ForgeGuardianView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => DetachMessages();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachMessages();
        if (e.NewValue is ForgeGuardianViewModel viewModel)
        {
            _messages = viewModel.Messages;
            _messages.CollectionChanged += OnMessagesChanged;
            ScrollToLatest();
        }
    }

    private void DetachMessages()
    {
        if (_messages is not null)
            _messages.CollectionChanged -= OnMessagesChanged;
        _messages = null;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToLatest();

    private void ScrollToLatest()
    {
        Dispatcher.BeginInvoke(() => ChatScrollViewer.ScrollToEnd());
    }

    private async void SaveBorinPasswordClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ForgeGuardianViewModel viewModel)
            return;

        await viewModel.SavePlayerPasswordFromUiAsync(BorinPlayerPasswordBox.Password);
        BorinPlayerPasswordBox.Clear();
    }

    private async void ClearBorinPasswordClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ForgeGuardianViewModel viewModel)
            return;

        await viewModel.ClearPlayerPasswordFromUiAsync();
        BorinPlayerPasswordBox.Clear();
    }

    private void OpenBorinAssignmentClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ForgeGuardianViewModel viewModel)
            return;

        var assignmentWindow = new BorinPlayerAssignmentWindow(viewModel)
        {
            Owner = Window.GetWindow(this),
        };
        assignmentWindow.Show();
    }

    private void OpenBorinTerminalClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ForgeGuardianViewModel viewModel)
            return;

        try
        {
            viewModel.PreparePlayerTerminal();
            var terminalViewModel = viewModel.PlayerTerminalViewModel;
            var session = terminalViewModel.SelectedLiveSession
                ?? throw new InvalidOperationException(
                    "Borin's saved client session was not selected. Press Save Profile once, then open the terminal again.");

            ClientTerminalWindow.ShowForSession(
                terminalViewModel,
                session,
                Window.GetWindow(this));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Open Borin Terminal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
