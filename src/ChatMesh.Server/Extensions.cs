using ChatMesh.Server.Abstractions;
using ChatMesh.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SuperSocket.Server.Abstractions.Host;
using SuperSocket.WebSocket;

namespace ChatMesh.Server;

/// <summary>
/// Extension methods for setting up the ChatMesh server services and middleware in an <see cref="ISuperSocketHostBuilder{TPackage}"/>.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds the ChatMesh server middleware and related services to the specified <see cref="ISuperSocketHostBuilder{WebSocketPackage}"/>.
    /// </summary>
    /// <param name="builder">The <see cref="ISuperSocketHostBuilder{WebSocketPackage}"/> to configure.</param>
    /// <returns>The configured <see cref="ISuperSocketHostBuilder{WebSocketPackage}"/>.</returns>
    public static ISuperSocketHostBuilder<WebSocketPackage> UseChatMeshServer(this ISuperSocketHostBuilder<WebSocketPackage> builder)
    {
        builder
            .UseMiddleware<ChatMeshMiddleware>()
            .ConfigureServices((context, services) =>
            {
                services.Configure<AuthConfig>(context.Configuration.GetSection("Auth"));
                services.TryAddSingleton<IAuthenticationService, TokenService>();
                services.TryAddSingleton<ITopicMessageProvider, InMemoryTopicMessageProvider>();
            });
        return builder;
    }
}