using System.Net.WebSockets;
using System.Text;
using AIChatMesh.Client.Models;
using Microsoft.Extensions.Logging;

namespace AIChatMesh.Client.Services;

public sealed class ChatService : IDisposable
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private readonly ILogger<ChatService> _logger;

    public event Action<MessagePayload>? MessageReceived;
    public event Action<bool, string>? ConnectionStateChanged;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public string Username { get; private set; } = string.Empty;

    public string PeerUsername { get; private set; } = string.Empty;

    public ChatService(ILogger<ChatService> logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(string host, string username, string token, string peerUsername)
    {
        await DisconnectAsync();

        _webSocket = new ClientWebSocket();
        _receiveCts = new CancellationTokenSource();

        var scheme = host.StartsWith("wss://", StringComparison.OrdinalIgnoreCase) ||
                     host.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : "ws://";

        var uri = new Uri($"{scheme}{host}?username={Uri.EscapeDataString(username)}&token={Uri.EscapeDataString(token)}&peerUsername={Uri.EscapeDataString(peerUsername)}");

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
                    // Best-effort close
                }
            }

            _webSocket.Dispose();
            _webSocket = null;
        }

        ConnectionStateChanged?.Invoke(false,"Disconnected");
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
                        ConnectionStateChanged?.Invoke(false,"Server closed connection");
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
            // Normal disconnection
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket receive error");
            ConnectionStateChanged?.Invoke(false, $"Disconnected: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _webSocket?.Dispose();
    }
}
