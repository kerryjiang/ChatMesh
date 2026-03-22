using AIChatMesh.Server.Models;
using AIChatMesh.Server.Services;
using Microsoft.Extensions.Options;

namespace AIChatMesh.Server.Tests;

public class TokenServiceTests
{
    [Fact]
    public void ValidateToken_WithCorrectToken_ReturnsTrue()
    {
        var (salt, hash) = TokenService.CreateHash("my-secret-token");

        var config = Options.Create(new AuthConfig
        {
            AuthUsers =
            [
                new AuthUserConfig
                {
                    Username = "testuser",
                    Salt = salt,
                    HashedToken = hash
                }
            ]
        });

        var service = new TokenService(config);

        Assert.True(service.ValidateToken("testuser", "my-secret-token"));
    }

    [Fact]
    public void ValidateToken_WithWrongToken_ReturnsFalse()
    {
        var (salt, hash) = TokenService.CreateHash("my-secret-token");

        var config = Options.Create(new AuthConfig
        {
            AuthUsers =
            [
                new AuthUserConfig
                {
                    Username = "testuser",
                    Salt = salt,
                    HashedToken = hash
                }
            ]
        });

        var service = new TokenService(config);

        Assert.False(service.ValidateToken("testuser", "wrong-token"));
    }

    [Fact]
    public void ValidateToken_WithUnknownUser_ReturnsFalse()
    {
        var config = Options.Create(new AuthConfig
        {
            AuthUsers =
            [
                new AuthUserConfig
                {
                    Username = "alice",
                    Salt = Convert.ToBase64String(new byte[32]),
                    HashedToken = Convert.ToBase64String(new byte[32])
                }
            ]
        });

        var service = new TokenService(config);

        Assert.False(service.ValidateToken("unknown", "any-token"));
    }

    [Fact]
    public void ValidateToken_IsCaseInsensitive_ForUsername()
    {
        var (salt, hash) = TokenService.CreateHash("token123");

        var config = Options.Create(new AuthConfig
        {
            AuthUsers =
            [
                new AuthUserConfig
                {
                    Username = "Alice",
                    Salt = salt,
                    HashedToken = hash
                }
            ]
        });

        var service = new TokenService(config);

        Assert.True(service.ValidateToken("alice", "token123"));
        Assert.True(service.ValidateToken("ALICE", "token123"));
    }

    [Fact]
    public void CreateHash_ProducesDifferentSalts()
    {
        var (salt1, _) = TokenService.CreateHash("same-token");
        var (salt2, _) = TokenService.CreateHash("same-token");

        Assert.NotEqual(salt1, salt2);
    }
}

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
