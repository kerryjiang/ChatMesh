using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using AIChatMesh.Contract;
using AIChatMesh.Server.Abstractions;

namespace AIChatMesh.Server.Services;

public class InMemoryTopicMessageProvider : ITopicMessageProvider
{
    private readonly ConcurrentDictionary<int, List<MessagePayload>> _messagesByTopic = new();

    private readonly ConcurrentDictionary<int, List<TaskCompletionSource<MessagePayload>>> _waitingClientsByTopic = new();

    private readonly ConcurrentDictionary<int, long> _lastMessageIdByTopic = new();

    private async Task StartMessageWaiting(int topicId, CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<MessagePayload>();
        _waitingClientsByTopic.AddOrUpdate(topicId, _ => new List<TaskCompletionSource<MessagePayload>> { taskCompletionSource }, (key, oldValue) => { oldValue.Add(taskCompletionSource); return oldValue; });
        await taskCompletionSource.Task.WaitAsync(cancellationToken);
    }

    public async IAsyncEnumerable<MessagePayload> GetMessageStreamAsync(int topicId, long? lastReceivedMessageId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var currentLastMessageId = lastReceivedMessageId;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_messagesByTopic.TryGetValue(topicId, out var messages) || messages is null || !messages.Any())
            {
                await StartMessageWaiting(topicId, cancellationToken);
                continue;
            }

            var newMessages = messages
                .Where(message => currentLastMessageId is null || message.Id > currentLastMessageId.Value)
                .ToArray();

            if (newMessages.Length == 0)
            {
                await StartMessageWaiting(topicId, cancellationToken);
                continue;
            }

            foreach (var message in newMessages)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                yield return message;
                currentLastMessageId = message.Id;
            }
        }
    }

    public Task SaveMessageAsync(int topicId, MessagePayload message, CancellationToken cancellationToken = default)
    {
        message.Id = _lastMessageIdByTopic.AddOrUpdate(topicId, 1, static (_, current) => current + 1);

        if (!_messagesByTopic.TryGetValue(topicId, out var messages))
        {
            _messagesByTopic.TryAdd(topicId, new List<MessagePayload>());
            messages = _messagesByTopic[topicId];
        }

        messages.Add(message);

        _waitingClientsByTopic.TryRemove(topicId, out var waitingClients);

        if (waitingClients != null)
        {
            foreach (var client in waitingClients)
            {
                client.TrySetResult(message);
            }
        }

        return Task.CompletedTask;
    }
}