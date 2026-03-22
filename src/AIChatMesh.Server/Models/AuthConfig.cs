namespace AIChatMesh.Server.Models;

public sealed class AuthUserConfig
{
    public required string Username { get; set; }
    public required string Salt { get; set; }
    public required string HashedToken { get; set; }
}

public sealed class AuthConfig
{
    public List<AuthUserConfig> AuthUsers { get; set; } = [];
}
