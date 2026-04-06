using ChatMesh.Server;
using Microsoft.Extensions.Hosting;
using SuperSocket.Server;
using SuperSocket.Server.Host;
using SuperSocket.Server.Abstractions;
using SuperSocket.WebSocket.Server;

var host = Host.CreateDefaultBuilder(args)
    .AsWebSocketHostBuilder()
    .UseChatMeshServer()
    .ConfigureSuperSocket(options =>
    {
        options.Name = "ChatMesh";
        options.AddListener(new ListenOptions
        {
            Ip = "Any",
            Port = 4040
        });
    })
    .Build();

await host.RunAsync();