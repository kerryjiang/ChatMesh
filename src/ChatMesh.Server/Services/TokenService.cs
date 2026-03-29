using System.Security.Cryptography;
using System.Text;
using ChatMesh.Server.Abstractions;
using ChatMesh.Server.Models;
using Microsoft.Extensions.Options;

namespace ChatMesh.Server.Services;

public sealed class TokenService : IAuthenticationService
{
    private readonly IReadOnlyDictionary<string, AuthUserConfig> _users;

    public TokenService(IOptions<AuthConfig> authConfig)
    {
        _users = authConfig.Value.AuthUsers
            .ToDictionary(u => u.Username, u => u, StringComparer.OrdinalIgnoreCase);
    }

    public Task<bool> AuthenticateAsync(AuthenticationRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username;
        var token = request.Token;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token))
            return Task.FromResult(false);

        if (!_users.TryGetValue(username, out var userConfig))
            return Task.FromResult(false);

        var saltBytes = Convert.FromBase64String(userConfig.Salt);
        var computedHash = HashToken(token, saltBytes);
        var storedHash = Convert.FromBase64String(userConfig.HashedToken);

        return Task.FromResult(CryptographicOperations.FixedTimeEquals(computedHash, storedHash));
    }

    public static (string Salt, string HashedToken) CreateHash(string token)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(32);
        var hash = HashToken(token, saltBytes);
        return (Convert.ToBase64String(saltBytes), Convert.ToBase64String(hash));
    }

    private static byte[] HashToken(string token, byte[] salt)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var combined = new byte[salt.Length + tokenBytes.Length];
        salt.CopyTo(combined, 0);
        tokenBytes.CopyTo(combined, salt.Length);
        return SHA256.HashData(combined);
    }
}
