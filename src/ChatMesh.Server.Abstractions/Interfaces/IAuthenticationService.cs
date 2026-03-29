namespace ChatMesh.Server.Abstractions;

public interface IAuthenticationService
{
    Task<bool> AuthenticateAsync(AuthenticationRequest request, CancellationToken cancellationToken = default);
}