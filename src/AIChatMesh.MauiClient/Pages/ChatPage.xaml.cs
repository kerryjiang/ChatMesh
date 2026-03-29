using AIChatMesh.MauiClient.ViewModels;

namespace AIChatMesh.MauiClient.Pages;

public partial class ChatPage : ContentPage
{
    private readonly ChatPageViewModel _viewModel;

    public ChatPage(ChatPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.Messages.CollectionChanged += (_, _) =>
        {
            if (_viewModel.Messages.Count > 0)
                MessagesView.ScrollTo(_viewModel.Messages.Count - 1);
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.AutoConnectAsync();
    }
}
