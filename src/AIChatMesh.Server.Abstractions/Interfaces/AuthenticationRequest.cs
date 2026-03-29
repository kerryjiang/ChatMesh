namespace AIChatMesh.Server.Abstractions;

public sealed class AuthenticationRequest
{
    public string Username { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string HostName { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> HeaderItems { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}