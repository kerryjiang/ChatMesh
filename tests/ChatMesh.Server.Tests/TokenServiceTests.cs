using ChatMesh.Server.Abstractions;
using ChatMesh.Server.Models;
using Microsoft.Extensions.Options;

namespace ChatMesh.Server.Tests;

public class TokenServiceTests
{
    [Fact]
    public async Task AuthenticateAsync_WithCorrectToken_ReturnsTrue()
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

        var authenticated = await service.AuthenticateAsync(new AuthenticationRequest
        {
            Username = "testuser",
            Token = "my-secret-token",
            HostName = "localhost:4040",
            Path = "/?username=testuser",
            HeaderItems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = "localhost:4040"
            }
        });

        Assert.True(authenticated);
    }

    [Fact]
    public async Task AuthenticateAsync_WithWrongToken_ReturnsFalse()
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

        var authenticated = await service.AuthenticateAsync(new AuthenticationRequest
        {
            Username = "testuser",
            Token = "wrong-token",
            HostName = "localhost:4040",
            Path = "/?username=testuser",
            HeaderItems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = "localhost:4040"
            }
        });

        Assert.False(authenticated);
    }

    [Fact]
    public async Task AuthenticateAsync_WithUnknownUser_ReturnsFalse()
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

        var authenticated = await service.AuthenticateAsync(new AuthenticationRequest
        {
            Username = "unknown",
            Token = "any-token",
            HostName = "localhost:4040",
            Path = "/?username=unknown",
            HeaderItems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = "localhost:4040"
            }
        });

        Assert.False(authenticated);
    }

    [Fact]
    public async Task AuthenticateAsync_IsCaseInsensitive_ForUsername()
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

        var authenticatedLowercase = await service.AuthenticateAsync(new AuthenticationRequest
        {
            Username = "alice",
            Token = "token123",
            HostName = "localhost:4040",
            Path = "/?username=alice",
            HeaderItems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = "localhost:4040"
            }
        });

        var authenticatedUppercase = await service.AuthenticateAsync(new AuthenticationRequest
        {
            Username = "ALICE",
            Token = "token123",
            HostName = "localhost:4040",
            Path = "/?username=ALICE",
            HeaderItems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = "localhost:4040"
            }
        });

        Assert.True(authenticatedLowercase);
        Assert.True(authenticatedUppercase);
    }

    [Fact]
    public void CreateHash_ProducesDifferentSalts()
    {
        var (salt1, _) = TokenService.CreateHash("same-token");
        var (salt2, _) = TokenService.CreateHash("same-token");

        Assert.NotEqual(salt1, salt2);
    }
}