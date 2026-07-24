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
        Dispatcher.BeginInvoke(() =>
        {
            if (ChatList.Items.Count > 0)
                ChatList.ScrollIntoView(ChatList.Items[ChatList.Items.Count - 1]);
        });
    }
}
