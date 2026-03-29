using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using AIChatMesh.Contract;

namespace AIChatMesh.Server.Services;

public class InMemoryTopicMessageProvider : ITopicMessageProvider
{
    private readonly ConcurrentDictionary<int, List<MessagePayload>> _messagesByTopic = new();

    private readonly ConcurrentDictionary<int, List<TaskCompletionSource<MessagePayload>>> _waitingClientsByTopic = new();

    private async Task StartMessageWaiting(int topicId, CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<MessagePayload>();
        _waitingClientsByTopic.AddOrUpdate(topicId, _ => new List<TaskCompletionSource<MessagePayload>> { taskCompletionSource }, (key, oldValue) => { oldValue.Add(taskCompletionSource); return oldValue; });
        await taskCompletionSource.Task.WaitAsync(cancellationToken);
    }

    public async IAsyncEnumerable<MessagePayload> GetMessageStreamAsync(int topicId, long? lastReceivedMessageId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_messagesByTopic.TryGetValue(topicId, out var messages) || messages == null ||!messages.Any())
            {
                await StartMessageWaiting(topicId, cancellationToken);
            }

            if (lastReceivedMessageId == null)
            {
                foreach (var message in messages!)
                {
                    if (cancellationToken.IsCancellationRequested)
                        yield break;

                    yield return message;
                }

                await StartMessageWaiting(topicId, cancellationToken);
            }
            else
            {
                var fromIndex = -1;

                for (var i = messages!.Count - 1; i >= 0; i--)
                {
                    if (messages[i].Id <= lastReceivedMessageId.Value)
                    {
                        fromIndex = i;
                        break;
                    }
                }

                var newMessages = messages.Slice(fromIndex + 1, messages.Count - fromIndex - 1);

                foreach (var message in newMessages)
                {
                    yield return message;
                }

                await StartMessageWaiting(topicId, cancellationToken);
            }
        }
    }

    public Task SaveMessageAsync(int topicId, MessagePayload message, CancellationToken cancellationToken = default)
    {
        if (!_messagesByTopic.TryGetValue(topicId, out var messages))
        {
            _messagesByTopic.TryAdd(topicId, new List<MessagePayload>());
            messages = _messagesByTopic[topicId];
        }

        messages.Add(message);

        _waitingClientsByTopic.TryRemove(topicId, out var waitingClients);

        if (waitingClients != null)        {
            foreach (var client in waitingClients)            {
                client.TrySetResult(message);
            }
        }

        return Task.CompletedTask;
    }
}