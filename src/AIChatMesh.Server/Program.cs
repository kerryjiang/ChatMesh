using AIChatMesh.Server.Models;
using AIChatMesh.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Session;
using SuperSocket.Server.Host;
using SuperSocket.WebSocket;
using SuperSocket.WebSocket.Server;

var host = Host.CreateDefaultBuilder(args)
    .AsWebSocketHostBuilder()
    .ConfigureSuperSocket(options =>
    {
        options.Name = "AIChatMesh";
        options.AddListener(new ListenOptions
        {
            Ip = "Any",
            Port = 4040
        });
    })
    .UseWebSocketMessageHandler(async (session, package) =>
    {
        if (package.OpCode != OpCode.Text || string.IsNullOrEmpty(package.Message))
            return;

        var services = (session as IAppSession)!.Server.ServiceProvider;
        var chatRoom = services.GetRequiredService<ChatRoomService>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        var username = chatRoom.GetUsername(session.SessionID);

        if (username is null)
        {
            logger.LogWarning("Message from unauthenticated session {SessionId}", session.SessionID);
            return;
        }

        MessagePayload? incoming;

        try
        {
            incoming = MessageSerializer.Deserialize(package.Message);
        }
        catch
        {
            logger.LogWarning("Invalid message from {Username}: {Message}", username, package.Message);
            return;
        }

        if (incoming is ChatMessagePayload chatMsg)
        {
            var peerUsername = chatRoom.GetPeerUsername(session.SessionID);

            if (peerUsername is null)
                return;

            var outgoing = new ChatMessagePayload
            {
                Sender = username,
                Content = chatMsg.Content,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Route to the sender's peer, only if the peer has the sender as their peer
            await chatRoom.SendToUserAsync(peerUsername, outgoing, senderUsername: username);

            // Also echo back to sender
            await chatRoom.SendToSessionAsync(session.SessionID, outgoing);
        }
    })
    .UseSessionHandler(
        onConnected: async session =>
        {
            var wsSession = (WebSocketSession)session;
            var services = session.Server.ServiceProvider;
            var tokenService = services.GetRequiredService<TokenService>();
            var chatRoom = services.GetRequiredService<ChatRoomService>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            var path = wsSession.HttpHeader?.Path ?? string.Empty;
            var queryParams = ParseQueryString(path);

            queryParams.TryGetValue("username", out var username);
            queryParams.TryGetValue("token", out var token);
            queryParams.TryGetValue("peerUsername", out var peerUsername);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(peerUsername))
            {
                logger.LogWarning("Connection rejected: missing credentials from {RemoteEndPoint}", session.RemoteEndPoint);
                await wsSession.CloseAsync(CloseReason.ProtocolError, "Missing credentials", CancellationToken.None);
                return;
            }

            if (!tokenService.ValidateToken(username, token))
            {
                logger.LogWarning("Connection rejected: invalid token for user '{Username}'", username);
                await wsSession.CloseAsync(CloseReason.ProtocolError, "Invalid credentials", CancellationToken.None);
                return;
            }

            chatRoom.AddSession(session.SessionID, wsSession, username, peerUsername);

            await chatRoom.SendToSessionAsync(session.SessionID, new SystemMessagePayload
            {
                Content = $"Welcome to AIChatMesh, {username}!"
            });

            // Notify the peer that this user joined
            await chatRoom.SendToUserAsync(peerUsername, new UserJoinedPayload { Username = username }, senderUsername: username);
        },
        onClosed: async (session, _) =>
        {
            var services = session.Server.ServiceProvider;
            var chatRoom = services.GetRequiredService<ChatRoomService>();
            var username = chatRoom.GetUsername(session.SessionID);

            var peerUsername = chatRoom.GetPeerUsername(session.SessionID);
            chatRoom.RemoveSession(session.SessionID);

            if (username is not null && peerUsername is not null)
            {
                await chatRoom.SendToUserAsync(peerUsername, new UserLeftPayload { Username = username }, senderUsername: username);
            }
        })
    .ConfigureServices((context, services) =>
    {
        services.Configure<AuthConfig>(context.Configuration.GetSection("Auth"));
        services.AddSingleton<TokenService>();
        services.AddSingleton<ChatRoomService>();
    })
    .Build();

await host.RunAsync();

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
