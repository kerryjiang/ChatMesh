using AIChatMesh.Contract;
using AIChatMesh.Server.Abstractions;
using AIChatMesh.Server.Models;
using Microsoft.Extensions.Logging;
using SuperSocket.Server.Abstractions.Middleware;
using SuperSocket.Server.Abstractions.Session;
using SuperSocket.WebSocket;
using SuperSocket.WebSocket.Server;

namespace AIChatMesh.Server.Services;

public class ChatMeshMiddleware : MiddlewareBase
{
    private readonly ITopicMessageProvider _topicMessageProvider;

    private readonly IAuthenticationService _authenticationService;

    private readonly ILogger<ChatMeshMiddleware> _logger;

    public ChatMeshMiddleware(ITopicMessageProvider topicMessageProvider, IAuthenticationService authenticationService, ILogger<ChatMeshMiddleware> logger)
    {
        _topicMessageProvider = topicMessageProvider;
        _authenticationService = authenticationService;
        _logger = logger;
    }

    public override async ValueTask<bool> RegisterSession(IAppSession session)
    {
        session.Connected += OnSesssionOpenedAsync;
        return true;
    }

    private async ValueTask OnSesssionOpenedAsync(object sender, EventArgs eventArgs)
    {
        var session = sender as WebSocketSession;

        if (session is null)
        {
            _logger.LogError("Connected session is not a WebSocketSession");
            return;
        }

        session.Connected -= OnSesssionOpenedAsync;

        var connection = (session as IAppSession).Connection;

        var path = session.Path ?? string.Empty;
        var queryParams = ParseQueryString(path);

        queryParams.TryGetValue("username", out var username);
        queryParams.TryGetValue("token", out var token);
        queryParams.TryGetValue("peerUsername", out var peerUsername);
        queryParams.TryGetValue("lastMessageId", out var lastMessageIdStr);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(peerUsername))
        {
            _logger.LogWarning("Connection rejected: missing credentials from {RemoteEndPoint}", session.RemoteEndPoint);
            await session.CloseAsync(CloseReason.ProtocolError, "Missing credentials");
            return;
        }

        var headerItems = GetHeaderItems(session.HttpHeader?.Items);
        var hostName = headerItems.TryGetValue("Host", out var hostHeader) ? hostHeader : string.Empty;
        var authenticationRequest = new AuthenticationRequest
        {
            Username = username,
            Token = token,
            HostName = hostName,
            Path = path,
            HeaderItems = headerItems
        };

        if (!await _authenticationService.AuthenticateAsync(authenticationRequest, connection.ConnectionToken))
        {
            _logger.LogWarning("Connection rejected: invalid token for user '{Username}'", username);
            await session.CloseAsync(CloseReason.ProtocolError, "Invalid credentials");
            return;
        }

        var sessionTopic = SessionTopic.CreatePeerSessionTopic(username, peerUsername);
        sessionTopic.LastMessageId = long.TryParse(lastMessageIdStr, out var lastMsgId) ? lastMsgId : null;

        session.DataContext = sessionTopic;

        await session.SendAsync(MessageSerializer.Serialize(new SystemMessagePayload
        {
            Content = $"Welcome to AIChatMesh, {username}!"
        }));

        // Notify the peer that this user joined
        await _topicMessageProvider.SaveMessageAsync(sessionTopic.TopicId, new UserJoinedPayload { Username = username }, connection.ConnectionToken);

        _ = StartSessionChatMeshAsync(session).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogError(t.Exception, "Error in session chat mesh loop for session {SessionId}", session.SessionID);
            }
        });
    }

    public override async ValueTask<bool> UnRegisterSession(IAppSession session)
    {
        if (session.DataContext is not SessionTopic sessionTopic)
        {
            _logger.LogWarning("Closed session {SessionId} has no session topic", session.SessionID);
            return true;
        }

        var username = sessionTopic.Username;
        var peerUsername = sessionTopic.PeerUsername;

        if (username is not null && peerUsername is not null)
        {
            await _topicMessageProvider.SaveMessageAsync(sessionTopic.TopicId, new UserLeftPayload { Username = username }, session.Connection.ConnectionToken);
        }

        return true;
    }

    internal async ValueTask HandlePackageAsync(WebSocketSession session, WebSocketPackage package)
    {
        if (package.OpCode != OpCode.Text || string.IsNullOrEmpty(package.Message))
            return;

        var sessionTopic = session.DataContext as SessionTopic;

        if (sessionTopic is null)
        {
            _logger.LogWarning("Message from unauthenticated session {SessionId}", session.SessionID);
            return;
        }

        MessagePayload? incoming;

        try
        {
            incoming = MessageSerializer.Deserialize(package.Message);
        }
        catch
        {
            _logger.LogWarning("Invalid message from session {SessionId}: {Message}", session.SessionID, package.Message);
            return;
        }

        if (incoming is ChatMessagePayload chatMsg)
        {
            var peerUsername = sessionTopic.PeerUsername;

            if (peerUsername is null)
                return;

            var outgoing = new ChatMessagePayload
            {
                Sender = sessionTopic.Username,
                Content = chatMsg.Content,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Route to the sender's peer, only if the peer has the sender as their peer
            await _topicMessageProvider.SaveMessageAsync(sessionTopic.TopicId, outgoing, (session as IAppSession).Connection.ConnectionToken);

            // Also echo back to sender
            await session.SendAsync(MessageSerializer.Serialize(outgoing));
        }
    }


    private async Task StartSessionChatMeshAsync(WebSocketSession session)
    {
        if (session.DataContext is not SessionTopic sessionTopic)
        {
            return;
        }

        await foreach (var message in _topicMessageProvider.GetMessageStreamAsync(sessionTopic.TopicId, sessionTopic.LastMessageId, cancellationToken: (session as IAppSession).Connection.ConnectionToken))
        {
            await session.SendAsync(MessageSerializer.Serialize(message));
        }
    }

    static Dictionary<string, string> ParseQueryString(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var queryIndex = path.IndexOf('?');

        if (queryIndex < 0 || queryIndex >= path.Length - 1)
            return result;

        var query = path[(queryIndex + 1)..];

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eqIndex = pair.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = Uri.UnescapeDataString(pair[..eqIndex]);
                var value = Uri.UnescapeDataString(pair[(eqIndex + 1)..]);
                result[key] = value;
            }
        }

        return result;
    }

    static IReadOnlyDictionary<string, string> GetHeaderItems(System.Collections.Specialized.NameValueCollection? items)
    {
        if (items is null || items.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in items.AllKeys)
        {
            if (string.IsNullOrEmpty(key))
                continue;

            headers[key] = items[key] ?? string.Empty;
        }

        return headers;
    }

}