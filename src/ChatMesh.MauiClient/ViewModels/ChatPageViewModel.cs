using System.Collections.ObjectModel;
using ChatMesh.Client;
using ChatMesh.Contract;
using ChatMesh.MauiClient.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChatMesh.MauiClient.ViewModels;

public partial class ChatPageViewModel : ObservableObject, IDisposable
{
    private readonly ChatClient _chatService;
    private string _currentUsername = string.Empty;

    public ObservableCollection<ChatEntry> Messages { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isNotConnected = true;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _messageText = string.Empty;

    [ObservableProperty]
    private bool _canConnect = true;

    public ChatPageViewModel(ChatClient chatService)
    {
        _chatService = chatService;
        _chatService.MessageReceived += OnMessageReceived;
        _chatService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var host = Preferences.Default.Get("ServerHost", string.Empty);
        var username = Preferences.Default.Get("Username", string.Empty);
        var token = Preferences.Default.Get("AuthToken", string.Empty);
        var peerUsername = Preferences.Default.Get("PeerUsername", string.Empty);
        var messageEncryptionKey = Preferences.Default.Get("MessageEncryptionKey", string.Empty);

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(peerUsername))
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
                await page.DisplayAlertAsync("Settings Required", "Please configure your username, token, and peer username in Settings.", "OK");
            return;
        }

        _currentUsername = username;

        try
        {
            CanConnect = false;
            await _chatService.ConnectAsync(host, username, token, peerUsername, messageEncryptionKey);
        }
        catch
        {
            CanConnect = true;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _chatService.DisconnectAsync();
    }

    [RelayCommand]
    private static async Task OpenSettingsAsync()
    {
        await Shell.Current.GoToAsync(nameof(Pages.SettingsPage));
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = MessageText.Trim();

        if (string.IsNullOrEmpty(text))
            return;

        MessageText = string.Empty;
        await _chatService.SendMessageAsync(text);
    }

    private bool CanSend() => IsConnected && !string.IsNullOrWhiteSpace(MessageText);

    public async Task AutoConnectAsync()
    {
        if (_chatService.IsConnected)
            return;

        await ConnectAsync();
    }

    private void OnMessageReceived(MessagePayload message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var entry = message switch
            {
                ChatMessagePayload chat => new ChatEntry
                {
                    DisplayText = $"{chat.Sender}: {chat.Content}",
                    IsSystem = false,
                    IsOwnMessage = string.Equals(chat.Sender, _currentUsername, StringComparison.OrdinalIgnoreCase),
                    Timestamp = chat.Timestamp
                },
                UserJoinedPayload joined => new ChatEntry
                {
                    DisplayText = $"*** {joined.Username} joined the chat ***",
                    IsSystem = true,
                    Timestamp = joined.Timestamp
                },
                UserLeftPayload left => new ChatEntry
                {
                    DisplayText = $"*** {left.Username} left the chat ***",
                    IsSystem = true,
                    Timestamp = left.Timestamp
                },
                SystemMessagePayload sys => new ChatEntry
                {
                    DisplayText = $"[System] {sys.Content}",
                    IsSystem = true,
                    Timestamp = sys.Timestamp
                },
                _ => null
            };

            if (entry is not null)
            {
                Messages.Add(entry);
            }
        });
    }

    private void OnConnectionStateChanged(bool connected, string stateMessage)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = connected;
            IsNotConnected = !connected;

            if (connected)
            {
                _currentUsername = _chatService.Username;
                StatusText = $"{_chatService.Username}/{_chatService.PeerUsername}";
            }
            else
            {
                _currentUsername = string.Empty;
                StatusText = string.Empty;
                CanConnect = true;
            }
        });
    }

    public void Dispose()
    {
        _chatService.MessageReceived -= OnMessageReceived;
        _chatService.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
