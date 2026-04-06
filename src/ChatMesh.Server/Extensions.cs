using ChatMesh.Server.Abstractions;
using ChatMesh.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SuperSocket.Server.Abstractions.Host;
using SuperSocket.Server.Abstractions.Middleware;
using SuperSocket.WebSocket;
using SuperSocket.WebSocket.Server;

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
    /// <param name="configureServices">An optional action to further configure the services.</param>
    /// <returns>The configured <see cref="ISuperSocketHostBuilder{WebSocketPackage}"/>.</returns>
    public static ISuperSocketHostBuilder<WebSocketPackage> UseChatMeshServer(this ISuperSocketHostBuilder<WebSocketPackage> builder, Action<HostBuilderContext, IServiceCollection>? configureServices = null)
    {
        builder
            .UseMiddleware<ChatMeshMiddleware>()
            .ConfigureServices((context, services) =>
            {
                configureServices?.Invoke(context, services);

                services.Configure<AuthConfig>(context.Configuration.GetSection("Auth"));
                services.TryAddSingleton<IAuthenticationService, TokenService>();
                services.TryAddSingleton<ITopicMessageProvider, InMemoryTopicMessageProvider>();
                services.TryAddSingleton<IWebSocketCommandMiddleware>(sp => sp.GetServices<IMiddleware>().OfType<ChatMeshMiddleware>().First());
            });
        return builder;
    }
}