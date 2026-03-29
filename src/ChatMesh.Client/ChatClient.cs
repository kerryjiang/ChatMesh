using System.Net.WebSockets;
using System.Security.Cryptography;
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

    public string? MessageEncryptionKey { get; private set; }

    public ChatClient(ILogger<ChatClient> logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(string host, string username, string token, string peerUsername, string? messageEncryptionKey = null)
    {
        await DisconnectAsync();

        ResetConversationCursorIfNeeded(host, username, peerUsername);
        MessageEncryptionKey = NormalizeMessageEncryptionKey(messageEncryptionKey);

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

        var encrypted = !string.IsNullOrWhiteSpace(MessageEncryptionKey);
        var payload = new ChatMessagePayload
        {
            Content = encrypted ? EncryptContent(content, MessageEncryptionKey!) : content,
            Encypted = encrypted
        };
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
                        if (message is ChatMessagePayload chatMessage)
                        {
                            TryDecryptChatMessage(chatMessage);
                        }

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

    private void TryDecryptChatMessage(ChatMessagePayload message)
    {
        if (!message.Encypted || string.IsNullOrWhiteSpace(MessageEncryptionKey))
            return;

        try
        {
            message.Content = DecryptContent(message.Content, MessageEncryptionKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt chat message for conversation {ConversationKey}", _conversationKey);
        }
    }

    private static string? NormalizeMessageEncryptionKey(string? messageEncryptionKey)
    {
        if (string.IsNullOrWhiteSpace(messageEncryptionKey))
            return null;

        return messageEncryptionKey.Trim();
    }

    private static string EncryptContent(string content, string key)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(content);
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using var aesGcm = new AesGcm(keyBytes, tag.Length);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var encryptedBytes = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, encryptedBytes, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, encryptedBytes, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, encryptedBytes, nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(encryptedBytes);
    }

    private static string DecryptContent(string encryptedContent, string key)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedContent);
        var nonceLength = AesGcm.NonceByteSizes.MaxSize;
        var tagLength = AesGcm.TagByteSizes.MaxSize;

        if (encryptedBytes.Length <= nonceLength + tagLength)
            throw new CryptographicException("Encrypted content payload is invalid.");

        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var nonce = encryptedBytes.AsSpan(0, nonceLength);
        var tag = encryptedBytes.AsSpan(nonceLength, tagLength);
        var ciphertext = encryptedBytes.AsSpan(nonceLength + tagLength);
        var plaintextBytes = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(keyBytes, tag.Length);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    public void Dispose()
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _webSocket?.Dispose();
    }
}
