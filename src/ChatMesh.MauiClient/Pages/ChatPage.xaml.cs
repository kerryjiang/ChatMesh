using ChatMesh.MauiClient.ViewModels;
using System.Collections.Specialized;

namespace ChatMesh.MauiClient.Pages;

public partial class ChatPage : ContentPage
{
    private readonly ChatPageViewModel _viewModel;
    private bool _messagesSubscribed;

    public ChatPage(ChatPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_messagesSubscribed)
        {
            _viewModel.Messages.CollectionChanged += OnMessagesCollectionChanged;
            _messagesSubscribed = true;
        }

        await _viewModel.AutoConnectAsync();
    }

    protected override void OnDisappearing()
    {
        if (_messagesSubscribed)
        {
            _viewModel.Messages.CollectionChanged -= OnMessagesCollectionChanged;
            _messagesSubscribed = false;
        }

        base.OnDisappearing();
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel.Messages.Count == 0 || e.Action is not NotifyCollectionChangedAction.Add)
            return;

        Dispatcher.Dispatch(ScrollToLatestMessage);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), ScrollToLatestMessage);
    }

    private void ScrollToLatestMessage()
    {
        if (_viewModel.Messages.Count == 0)
            return;

        MessagesView.ScrollTo(_viewModel.Messages[^1], position: ScrollToPosition.End, animate: true);
    }
}
