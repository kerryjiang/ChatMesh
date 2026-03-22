using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIChatMesh.Server.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
[JsonDerivedType(typeof(ChatMessagePayload), "ChatMessage")]
[JsonDerivedType(typeof(UserJoinedPayload), "UserJoined")]
[JsonDerivedType(typeof(UserLeftPayload), "UserLeft")]
[JsonDerivedType(typeof(SystemMessagePayload), "SystemMessage")]
public abstract class MessagePayload
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ChatMessagePayload : MessagePayload
{
    public string Sender { get; set; } = string.Empty;
    public required string Content { get; set; }
}

public sealed class UserJoinedPayload : MessagePayload
{
    public required string Username { get; set; }
}

public sealed class UserLeftPayload : MessagePayload
{
    public required string Username { get; set; }
}

public sealed class SystemMessagePayload : MessagePayload
{
    public required string Content { get; set; }
}

public static class MessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(MessagePayload message) =>
        JsonSerializer.Serialize(message, Options);

    public static MessagePayload? Deserialize(string json) =>
        JsonSerializer.Deserialize<MessagePayload>(json, Options);
}
