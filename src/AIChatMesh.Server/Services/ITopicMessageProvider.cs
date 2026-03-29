using AIChatMesh.Contract;

namespace AIChatMesh.Server.Services;

public interface ITopicMessageProvider
{
    IAsyncEnumerable<MessagePayload> GetMessageStreamAsync(int topicId, long? lastReceivedMessageId, CancellationToken cancellationToken = default);

    Task SaveMessageAsync(int topicId, MessagePayload message, CancellationToken cancellationToken = default);
}