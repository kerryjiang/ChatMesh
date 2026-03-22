using System.Collections.Concurrent;
using AIChatMesh.Server.Models;
using Microsoft.Extensions.Logging;
using SuperSocket.WebSocket.Server;

namespace AIChatMesh.Server.Services;

public sealed class ChatRoomService
{
    private readonly ConcurrentDictionary<string, (WebSocketSession Session, string Username, string PeerUsername)> _sessions = new();
    private readonly ILogger<ChatRoomService> _logger;

    public ChatRoomService(ILogger<ChatRoomService> logger)
    {
        _logger = logger;
    }

    public void AddSession(string sessionId, WebSocketSession session, string username, string peerUsername)
    {
        _sessions[sessionId] = (session, username, peerUsername);
        _logger.LogInformation("User '{Username}' connected (session {SessionId}, peer '{PeerUsername}')", username, sessionId, peerUsername);
    }

    public void RemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var entry))
        {
            _logger.LogInformation("User '{Username}' disconnected (session {SessionId})", entry.Username, sessionId);
        }
    }

    public string? GetUsername(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var entry) ? entry.Username : null;
    }

    public string? GetPeerUsername(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var entry) ? entry.PeerUsername : null;
    }

    public async Task SendToUserAsync(string targetUsername, MessagePayload message, string? senderUsername = null)
    {
        var json = MessageSerializer.Serialize(message);

        foreach (var (_, entry) in _sessions)
        {
            if (!string.Equals(entry.Username, targetUsername, StringComparison.OrdinalIgnoreCase))
                continue;

            // Only deliver if sender matches the target's registered peer
            if (senderUsername is not null &&
                !string.Equals(entry.PeerUsername, senderUsername, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                await entry.Session.SendAsync(json, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send message to user '{Username}'", targetUsername);
            }
        }
    }

    public async Task SendToSessionAsync(string sessionId, MessagePayload message)
    {
        var json = MessageSerializer.Serialize(message);

        if (_sessions.TryGetValue(sessionId, out var entry))
        {
            await entry.Session.SendAsync(json, CancellationToken.None);
        }
    }
}
