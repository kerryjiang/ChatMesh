namespace ChatMesh.Server.Models;

public class SessionTopic
{
    public string Username { get; set; } = string.Empty;
    public string PeerUsername { get; set; } = string.Empty;
    public int TopicId { get; }
    public string Topic { get; }
    public long? LastMessageId { get; set; }
    
    public SessionTopic(string username, string peerUsername)
    {
        Username = username;
        PeerUsername = peerUsername;
        TopicId = Username.GetHashCode() ^ PeerUsername.GetHashCode();
        Topic = $"{Username}:{PeerUsername}";
    }

    public SessionTopic(string username, string peerUsername, string topic, int topicId)
    {
        Username = username;
        PeerUsername = peerUsername;
        TopicId = topicId;
        Topic = topic;
    }

    public static SessionTopic CreatePeerSessionTopic(string username, string peerUsername)
    {
        return new SessionTopic(username, peerUsername);
    }

    public static SessionTopic CreateSessionTopic(string username, string topic)
    {
        return new SessionTopic(username, string.Empty, topic, topic.GetHashCode());
    }
}