using ChatMesh.Contract;

namespace ChatMesh.Server.Tests;

public class InMemoryTopicMessageProviderTests
{
    [Fact]
    public async Task SaveMessageAsync_AssignsIncrementingIds_PerTopic()
    {
        var provider = new InMemoryTopicMessageProvider();
        var topicOneMessageOne = new ChatMessagePayload { Sender = "alice", Content = "one" };
        var topicOneMessageTwo = new ChatMessagePayload { Sender = "alice", Content = "two" };
        var topicTwoMessageOne = new ChatMessagePayload { Sender = "bob", Content = "other" };

        await provider.SaveMessageAsync(1, topicOneMessageOne);
        await provider.SaveMessageAsync(1, topicOneMessageTwo);
        await provider.SaveMessageAsync(2, topicTwoMessageOne);

        Assert.Equal(1, topicOneMessageOne.Id);
        Assert.Equal(2, topicOneMessageTwo.Id);
        Assert.Equal(1, topicTwoMessageOne.Id);
    }

    [Fact]
    public async Task GetMessageStreamAsync_WithLastReceivedMessageId_YieldsOnlyUnreadMessages()
    {
        var provider = new InMemoryTopicMessageProvider();

        await provider.SaveMessageAsync(7, new ChatMessagePayload { Sender = "alice", Content = "first" });
        await provider.SaveMessageAsync(7, new ChatMessagePayload { Sender = "alice", Content = "second" });
        await provider.SaveMessageAsync(7, new ChatMessagePayload { Sender = "alice", Content = "third" });

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var messages = await ReadMessagesAsync(
            provider.GetMessageStreamAsync(7, lastReceivedMessageId: 2, cancellationTokenSource.Token),
            count: 1,
            cancellationTokenSource.Token);

        var message = Assert.Single(messages);
        var chatMessage = Assert.IsType<ChatMessagePayload>(message);
        Assert.Equal(3, chatMessage.Id);
        Assert.Equal("third", chatMessage.Content);
    }

    [Fact]
    public async Task GetMessageStreamAsync_WaitsUntilNewMessageIsSaved()
    {
        var provider = new InMemoryTopicMessageProvider();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var readTask = ReadMessagesAsync(
            provider.GetMessageStreamAsync(11, lastReceivedMessageId: null, cancellationTokenSource.Token),
            count: 1,
            cancellationTokenSource.Token);

        await Task.Delay(100, cancellationTokenSource.Token);
        await provider.SaveMessageAsync(11, new ChatMessagePayload { Sender = "bob", Content = "hello" }, cancellationTokenSource.Token);

        var messages = await readTask;
        var message = Assert.Single(messages);
        var chatMessage = Assert.IsType<ChatMessagePayload>(message);
        Assert.Equal(1, chatMessage.Id);
        Assert.Equal("hello", chatMessage.Content);
    }

    private static async Task<List<MessagePayload>> ReadMessagesAsync(IAsyncEnumerable<MessagePayload> stream, int count, CancellationToken cancellationToken)
    {
        var messages = new List<MessagePayload>();

        await foreach (var message in stream.WithCancellation(cancellationToken))
        {
            messages.Add(message);

            if (messages.Count == count)
                break;
        }

        return messages;
    }
}