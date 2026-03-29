using ChatMesh.Contract;

namespace ChatMesh.Server.Tests;

public class MessageSerializerTests
{
    [Fact]
    public void RoundTrip_ChatMessage()
    {
        var original = new ChatMessagePayload
        {
            Sender = "alice",
            Content = "Hello!",
            Timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
        };

        var json = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(json);

        var chat = Assert.IsType<ChatMessagePayload>(deserialized);
        Assert.Equal("alice", chat.Sender);
        Assert.Equal("Hello!", chat.Content);
    }

    [Fact]
    public void RoundTrip_UserJoined()
    {
        var original = new UserJoinedPayload { Username = "bob" };

        var json = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(json);

        var joined = Assert.IsType<UserJoinedPayload>(deserialized);
        Assert.Equal("bob", joined.Username);
    }

    [Fact]
    public void RoundTrip_SystemMessage()
    {
        var original = new SystemMessagePayload { Content = "Welcome!" };

        var json = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(json);

        var sys = Assert.IsType<SystemMessagePayload>(deserialized);
        Assert.Equal("Welcome!", sys.Content);
    }

    [Fact]
    public void Serialize_IncludesTypeDiscriminator()
    {
        var msg = new ChatMessagePayload { Sender = "a", Content = "b" };
        var json = MessageSerializer.Serialize(msg);

        Assert.Contains("\"type\":", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ChatMessage", json);
    }
}