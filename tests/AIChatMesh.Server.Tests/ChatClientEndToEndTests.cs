using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using AIChatMesh.Client;
using AIChatMesh.Contract;
using AIChatMesh.Server.Abstractions;
using AIChatMesh.Server.Models;
using AIChatMesh.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Middleware;
using SuperSocket.Server.Abstractions.Session;
using SuperSocket.Server.Host;
using SuperSocket.WebSocket;
using SuperSocket.WebSocket.Server;

namespace AIChatMesh.Server.Tests;

public class ChatClientEndToEndTests
{
    [Fact]
    public async Task ConnectAsync_ReceivesWelcomeAndJoinMessages()
    {
        await using var host = await ChatMeshTestHost.StartAsync();
        using var client = new ChatClient(NullLogger<ChatClient>.Instance);

        var welcomeTask = WaitForMessageAsync(
            client,
            static message => message is SystemMessagePayload { Content: var content } && content.Contains("Welcome to AIChatMesh, alice!", StringComparison.Ordinal),
            TimeSpan.FromSeconds(5));

        var joinedTask = WaitForMessageAsync(
            client,
            static message => message is UserJoinedPayload { Username: "alice" },
            TimeSpan.FromSeconds(5));

        await client.ConnectAsync(host.HostAddress, "alice", ChatMeshTestHost.AliceToken, "bob");

        var welcome = await welcomeTask;
        var joined = await joinedTask;

        Assert.True(client.IsConnected);
        Assert.Equal("alice", client.Username);
        Assert.Equal("bob", client.PeerUsername);
        Assert.IsType<SystemMessagePayload>(welcome);
        Assert.IsType<UserJoinedPayload>(joined);
    }

    [Fact]
    public async Task SendMessageAsync_EchoesToSender_AndDeliversToPeer()
    {
        await using var host = await ChatMeshTestHost.StartAsync();
        using var aliceClient = new ChatClient(NullLogger<ChatClient>.Instance);
        using var bobClient = new ChatClient(NullLogger<ChatClient>.Instance);
        const string content = "hello-from-alice";

        await aliceClient.ConnectAsync(host.HostAddress, "alice", ChatMeshTestHost.AliceToken, "bob");
        await bobClient.ConnectAsync(host.HostAddress, "bob", ChatMeshTestHost.BobToken, "alice");

        var aliceMessageTask = WaitForMessageAsync(
            aliceClient,
            static message => message is ChatMessagePayload { Sender: "alice", Content: content },
            TimeSpan.FromSeconds(5));

        var bobMessageTask = WaitForMessageAsync(
            bobClient,
            static message => message is ChatMessagePayload { Sender: "alice", Content: content },
            TimeSpan.FromSeconds(5));

        await aliceClient.SendMessageAsync(content);

        var aliceMessage = Assert.IsType<ChatMessagePayload>(await aliceMessageTask);
        var bobMessage = Assert.IsType<ChatMessagePayload>(await bobMessageTask);

        Assert.Equal(content, aliceMessage.Content);
        Assert.Equal(content, bobMessage.Content);
        Assert.Equal(aliceMessage.Id, bobMessage.Id);
        Assert.True(aliceMessage.Id > 0);
    }

    [Fact]
    public async Task DisconnectAsync_PeerReceivesUserLeftMessage()
    {
        await using var host = await ChatMeshTestHost.StartAsync();
        using var aliceClient = new ChatClient(NullLogger<ChatClient>.Instance);
        using var bobClient = new ChatClient(NullLogger<ChatClient>.Instance);

        await aliceClient.ConnectAsync(host.HostAddress, "alice", ChatMeshTestHost.AliceToken, "bob");
        await bobClient.ConnectAsync(host.HostAddress, "bob", ChatMeshTestHost.BobToken, "alice");

        var leftTask = WaitForMessageAsync(
            bobClient,
            static message => message is UserLeftPayload { Username: "alice" },
            TimeSpan.FromSeconds(5));

        await aliceClient.DisconnectAsync();

        var leftMessage = await leftTask;
        Assert.IsType<UserLeftPayload>(leftMessage);
    }

    [Fact]
    public async Task ReconnectAsync_DoesNotReplayAlreadyReadMessages()
    {
        await using var host = await ChatMeshTestHost.StartAsync();
        using var aliceClient = new ChatClient(NullLogger<ChatClient>.Instance);
        using var bobClient = new ChatClient(NullLogger<ChatClient>.Instance);
        var aliceChatContents = new ConcurrentQueue<string>();
        const string firstMessage = "first-message";
        const string secondMessage = "second-message";

        aliceClient.MessageReceived += message =>
        {
            if (message is ChatMessagePayload chatMessage)
            {
                aliceChatContents.Enqueue(chatMessage.Content);
            }
        };

        await aliceClient.ConnectAsync(host.HostAddress, "alice", ChatMeshTestHost.AliceToken, "bob");
        await bobClient.ConnectAsync(host.HostAddress, "bob", ChatMeshTestHost.BobToken, "alice");

        var firstMessageTask = WaitForMessageAsync(
            aliceClient,
            static message => message is ChatMessagePayload { Sender: "bob", Content: firstMessage },
            TimeSpan.FromSeconds(5));

        await bobClient.SendMessageAsync(firstMessage);
        _ = await firstMessageTask;

        await aliceClient.DisconnectAsync();

        await bobClient.SendMessageAsync(secondMessage);

        var secondMessageTask = WaitForMessageAsync(
            aliceClient,
            static message => message is ChatMessagePayload { Sender: "bob", Content: secondMessage },
            TimeSpan.FromSeconds(5));

        await aliceClient.ConnectAsync(host.HostAddress, "alice", ChatMeshTestHost.AliceToken, "bob");
        _ = await secondMessageTask;

        await Task.Delay(250);

        var chatContents = aliceChatContents.ToArray();
        Assert.Equal(1, chatContents.Count(content => content == firstMessage));
        Assert.Equal(1, chatContents.Count(content => content == secondMessage));
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidToken_IsRejectedByServer()
    {
        await using var host = await ChatMeshTestHost.StartAsync();
        using var client = new ChatClient(NullLogger<ChatClient>.Instance);
        var stateChanges = new ConcurrentQueue<(bool Connected, string Message)>();
        var disconnectedTask = WaitForConnectionStateAsync(
            client,
            static state => state == (false, "Server closed connection"),
            TimeSpan.FromSeconds(5));

        client.ConnectionStateChanged += (connected, message) => stateChanges.Enqueue((connected, message));

        await client.ConnectAsync(host.HostAddress, "alice", "not-the-right-token", "bob");

        var disconnected = await disconnectedTask;

        Assert.Equal((false, "Server closed connection"), disconnected);
        Assert.Contains(stateChanges, state => state == (true, "Connected"));
    }

    private static Task<MessagePayload> WaitForMessageAsync(ChatClient client, Func<MessagePayload, bool> predicate, TimeSpan timeout)
    {
        var taskCompletionSource = new TaskCompletionSource<MessagePayload>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(MessagePayload message)
        {
            if (!predicate(message))
                return;

            client.MessageReceived -= Handler;
            taskCompletionSource.TrySetResult(message);
        }

        client.MessageReceived += Handler;

        return WaitForTaskAsync(taskCompletionSource.Task, () => client.MessageReceived -= Handler, timeout);
    }

    private static Task<(bool Connected, string Message)> WaitForConnectionStateAsync(
        ChatClient client,
        Func<(bool Connected, string Message), bool> predicate,
        TimeSpan timeout)
    {
        var taskCompletionSource = new TaskCompletionSource<(bool Connected, string Message)>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(bool connected, string message)
        {
            var state = (connected, message);

            if (!predicate(state))
                return;

            client.ConnectionStateChanged -= Handler;
            taskCompletionSource.TrySetResult(state);
        }

        client.ConnectionStateChanged += Handler;

        return WaitForTaskAsync(taskCompletionSource.Task, () => client.ConnectionStateChanged -= Handler, timeout);
    }

    private static async Task<T> WaitForTaskAsync<T>(Task<T> task, Action cleanup, TimeSpan timeout)
    {
        try
        {
            return await task.WaitAsync(timeout);
        }
        finally
        {
            cleanup();
        }
    }

    private sealed class ChatMeshTestHost : IAsyncDisposable
    {
        public const string AliceToken = "alice-token";
        public const string BobToken = "bob-token";

        private readonly IHost _host;

        private ChatMeshTestHost(IHost host, int port)
        {
            _host = host;
            HostAddress = $"ws://127.0.0.1:{port}/chatmesh";
        }

        public string HostAddress { get; }

        public static async Task<ChatMeshTestHost> StartAsync()
        {
            var port = GetAvailablePort();
            var authConfig = CreateAuthConfig();

            var host = Host.CreateDefaultBuilder()
                .AsWebSocketHostBuilder()
                .UseMiddleware<ChatMeshMiddleware>()
                .ConfigureSuperSocket(options =>
                {
                    options.Name = "AIChatMesh.Tests";
                    options.AddListener(new ListenOptions
                    {
                        Ip = "Any",
                        Port = port
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
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IOptions<AuthConfig>>(Options.Create(authConfig));
                    services.AddSingleton<IAuthenticationService, TokenService>();
                    services.AddSingleton<ITopicMessageProvider, InMemoryTopicMessageProvider>();
                })
                .Build();

            await host.StartAsync();
            return new ChatMeshTestHost(host, port);
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        private static AuthConfig CreateAuthConfig()
        {
            var (aliceSalt, aliceHash) = TokenService.CreateHash(AliceToken);
            var (bobSalt, bobHash) = TokenService.CreateHash(BobToken);

            return new AuthConfig
            {
                AuthUsers =
                [
                    new AuthUserConfig
                    {
                        Username = "alice",
                        Salt = aliceSalt,
                        HashedToken = aliceHash
                    },
                    new AuthUserConfig
                    {
                        Username = "bob",
                        Salt = bobSalt,
                        HashedToken = bobHash
                    }
                ]
            };
        }

        private static int GetAvailablePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}