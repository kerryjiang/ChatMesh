namespace AIChatMesh.Server.Abstractions;

public interface ITokenService
{
    bool ValidateToken(string username, string token);
}
