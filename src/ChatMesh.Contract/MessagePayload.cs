namespace ChatMesh.Contract;

using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
[JsonDerivedType(typeof(ChatMessagePayload), "ChatMessage")]
[JsonDerivedType(typeof(UserJoinedPayload), "UserJoined")]
[JsonDerivedType(typeof(UserLeftPayload), "UserLeft")]
[JsonDerivedType(typeof(SystemMessagePayload), "SystemMessage")]
public abstract class MessagePayload
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}