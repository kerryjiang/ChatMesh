using AIChatMesh.Contract;
using AIChatMesh.Server.Abstractions;
using AIChatMesh.Server.Models;
using AIChatMesh.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Middleware;
using SuperSocket.Server.Abstractions.Session;
using SuperSocket.Server.Host;
using SuperSocket.WebSocket;
using SuperSocket.WebSocket.Server;

var host = Host.CreateDefaultBuilder(args)
    .AsWebSocketHostBuilder()
    .UseMiddleware<ChatMeshMiddleware>()
    .ConfigureSuperSocket(options =>
    {
        options.Name = "AIChatMesh";
        options.AddListener(new ListenOptions
        {
            Ip = "Any",
            Port = 4040
        });
    })
    .UseWebSocketMessageHandler(async (session, package) =>
    {
        if (package.OpCode != OpCode.Text || string.IsNullOrEmpty(package.Message))
            return;

        var services = (session as IAppSession)!.Server.ServiceProvider;
        var middleware = services.GetServices<IMiddleware>().OfType<ChatMeshMiddleware>().First();
        await middleware.HandlePackageAsync(session, package);
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<AuthConfig>(context.Configuration.GetSection("Auth"));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ITopicMessageProvider, InMemoryTopicMessageProvider>();
    })
    .Build();

await host.RunAsync();