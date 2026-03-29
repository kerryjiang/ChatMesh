using System.Net.WebSockets;
using System.Text;
using ChatMesh.Contract;
using Microsoft.Extensions.Logging;

namespace ChatMesh.Client;

public sealed class ChatClient : IDisposable
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private readonly ILogger<ChatClient> _logger;
    private long? _lastReceivedMessageId;
    private string? _conversationKey;

    public event Action<MessagePayload>? MessageReceived;
    public event Action<bool, string>? ConnectionStateChanged;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public string Username { get; private set; } = string.Empty;

    public string PeerUsername { get; private set; } = string.Empty;

    public ChatClient(ILogger<ChatClient> logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(string host, string username, string token, string peerUsername)
    {
        await DisconnectAsync();

        ResetConversationCursorIfNeeded(host, username, peerUsername);

        _webSocket = new ClientWebSocket();
        _receiveCts = new CancellationTokenSource();

        var scheme = host.StartsWith("wss://", StringComparison.OrdinalIgnoreCase) ||
                     host.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : "ws://";

        var uriBuilder = new UriBuilder($"{scheme}{host}");
        var queryParts = new List<string>
        {
            $"username={Uri.EscapeDataString(username)}",
            $"token={Uri.EscapeDataString(token)}",
            $"peerUsername={Uri.EscapeDataString(peerUsername)}"
        };

        if (_lastReceivedMessageId is long lastReceivedMessageId)
        {
            queryParts.Add($"lastMessageId={lastReceivedMessageId}");
        }

        uriBuilder.Query = string.Join("&", queryParts);
        var uri = uriBuilder.Uri;

        try
        {
            await _webSocket.ConnectAsync(uri, CancellationToken.None);
            Username = username;
            PeerUsername = peerUsername;
            ConnectionStateChanged?.Invoke(true, "Connected");
            _ = ReceiveLoopAsync(_receiveCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to {Uri}", uri);
            ConnectionStateChanged?.Invoke(false, $"Connection failed: {ex.Message}");
            throw;
        }
    }

    public async Task SendMessageAsync(string content)
    {
        if (_webSocket?.State != WebSocketState.Open)
            return;

        var payload = new ChatMessagePayload { Content = content };
        var json = MessageSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);
    }

    public async Task DisconnectAsync()
    {
        if (_receiveCts is not null)
        {
            await _receiveCts.CancelAsync();
            _receiveCts.Dispose();
            _receiveCts = null;
        }

        if (_webSocket is not null)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                }
                catch
                {
                }
            }

            _webSocket.Dispose();
            _webSocket = null;
        }

        ConnectionStateChanged?.Invoke(false, "Disconnected");
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var sb = new StringBuilder();
                WebSocketReceiveResult result;

                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        ConnectionStateChanged?.Invoke(false, "Server closed connection");
                        return;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                var json = sb.ToString();

                try
                {
                    var message = MessageSerializer.Deserialize(json);
                    if (message is not null)
                    {
                        MessageReceived?.Invoke(message);

                        if (message.Id > 0)
                        {
                            _lastReceivedMessageId = message.Id;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize message: {Json}", json);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket receive error");
            ConnectionStateChanged?.Invoke(false, $"Disconnected: {ex.Message}");
        }
    }

    private void ResetConversationCursorIfNeeded(string host, string username, string peerUsername)
    {
        var conversationKey = string.Join('|', host.Trim(), username.Trim(), peerUsername.Trim());

        if (!string.Equals(_conversationKey, conversationKey, StringComparison.OrdinalIgnoreCase))
        {
            _conversationKey = conversationKey;
            _lastReceivedMessageId = null;
        }
    }

    public void Dispose()
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _webSocket?.Dispose();
    }
}
