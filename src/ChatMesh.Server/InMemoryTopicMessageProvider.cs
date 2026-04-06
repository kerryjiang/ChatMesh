using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ChatMesh.Contract;
using ChatMesh.Server.Abstractions;

namespace ChatMesh.Server;

public class InMemoryTopicMessageProvider : ITopicMessageProvider
{
    private readonly ConcurrentDictionary<int, TopicState> _topics = new();

    public async IAsyncEnumerable<MessagePayload> GetMessageStreamAsync(int topicId, long? lastReceivedMessageId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var topicState = _topics.GetOrAdd(topicId, static _ => new TopicState());
        var currentLastMessageId = lastReceivedMessageId;

        while (!cancellationToken.IsCancellationRequested)
        {
            MessagePayload[] newMessages;
            TaskCompletionSource<bool>? waiter = null;

            lock (topicState.SyncRoot)
            {
                newMessages = topicState.Messages
                    .Where(message => currentLastMessageId is null || message.Id > currentLastMessageId.Value)
                    .ToArray();

                if (newMessages.Length == 0)
                {
                    waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    topicState.Waiters.Add(waiter);
                }
            }

            if (newMessages.Length == 0)
            {
                try
                {
                    await waiter!.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    lock (topicState.SyncRoot)
                    {
                        topicState.Waiters.Remove(waiter!);
                    }

                    yield break;
                }

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
        var topicState = _topics.GetOrAdd(topicId, static _ => new TopicState());
        TaskCompletionSource<bool>[] waiters;

        lock (topicState.SyncRoot)
        {
            message.Id = ++topicState.LastMessageId;
            topicState.Messages.Add(message);
            waiters = topicState.Waiters.ToArray();
            topicState.Waiters.Clear();
        }

        foreach (var waiter in waiters)
        {
            waiter.TrySetResult(true);
        }

        return Task.CompletedTask;
    }

    private sealed class TopicState
    {
        public object SyncRoot { get; } = new();

        public List<MessagePayload> Messages { get; } = [];

        public List<TaskCompletionSource<bool>> Waiters { get; } = [];

        public long LastMessageId { get; set; }
    }
}