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