using System.Security.Cryptography;
using System.Text;
using AIChatMesh.Server.Abstractions;
using AIChatMesh.Server.Models;
using Microsoft.Extensions.Options;

namespace AIChatMesh.Server.Services;

public sealed class TokenService : ITokenService
{
    private readonly IReadOnlyDictionary<string, AuthUserConfig> _users;

    public TokenService(IOptions<AuthConfig> authConfig)
    {
        _users = authConfig.Value.AuthUsers
            .ToDictionary(u => u.Username, u => u, StringComparer.OrdinalIgnoreCase);
    }

    public bool ValidateToken(string username, string token)
    {
        if (!_users.TryGetValue(username, out var userConfig))
            return false;

        var saltBytes = Convert.FromBase64String(userConfig.Salt);
        var computedHash = HashToken(token, saltBytes);
        var storedHash = Convert.FromBase64String(userConfig.HashedToken);

        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
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
